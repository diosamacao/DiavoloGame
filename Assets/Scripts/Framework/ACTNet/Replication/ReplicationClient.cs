using System;
using System.Collections.Generic;

/// <summary>V2 客户端生命周期屏障、幂等注册表与有界快照缓冲。</summary>
public sealed class ReplicationClient
{
    /// <summary>屏障等待允许保留的最大 Tick 数。</summary>
    public const int MaxBufferedTicks = 8;
    /// <summary>屏障等待允许保留的最大实体数。</summary>
    public const int MaxBufferedEntities = 256;
    /// <summary>超过该 Tick 距离仍未越过屏障时只触发一次恢复。</summary>
    public const int BarrierTimeoutTicks = 30;

    readonly ReplicationSchemaRegistry _schemas;
    readonly ReplicatedEntityRegistry _registry = new ReplicatedEntityRegistry();
    readonly Dictionary<int, BufferedUpdate> _bufferedByEntity = new();
    readonly SortedDictionary<long, BufferedMetadata> _bufferedMetadata = new();
    readonly Dictionary<int, long> _latestEntityTicks = new();
    long _latestLifecycleSequence;
    long _latestSnapshotTick = -1;
    byte[] _latestMetadata = Array.Empty<byte>();
    bool _recoveryRaised;

    /// <summary>创建使用指定 Schema 校验完整状态的客户端。</summary>
    public ReplicationClient(ReplicationSchemaRegistry schemas) => _schemas = schemas ?? throw new ArgumentNullException(nameof(schemas));
    /// <summary>成功提交的最新生命周期序列。</summary>
    public long LatestLifecycleSequence => _latestLifecycleSequence;
    /// <summary>成功应用的最新快照 Tick。</summary>
    public long LatestSnapshotTick => _latestSnapshotTick;
    /// <summary>返回当前活动实体注册表副本。</summary>
    public ReplicatedEntityRegistry Registry => _registry.Clone();
    /// <summary>恢复请求是否已被闩定，直到 ResetForRecovery。</summary>
    public bool RecoveryRequested => _recoveryRaised;
    /// <summary>当前按实体合并后的屏障缓冲数量，永不超过 MaxBufferedEntities。</summary>
    public int BufferedEntityCount => _bufferedByEntity.Count;
    /// <summary>当前屏障 Meta Tick 数，永不超过 MaxBufferedTicks。</summary>
    public int BufferedTickCount => _bufferedMetadata.Count;

    /// <summary>生命周期实际创建实体后通知。</summary>
    public event Action<SpawnRecord> Spawned;
    /// <summary>生命周期实际删除实体后通知。</summary>
    public event Action<DespawnRecord> Despawned;
    /// <summary>快照完整状态实际应用后通知。</summary>
    public event Action<EntityRecord, long> Updated;
    /// <summary>快照 Meta 越过屏障后通知。</summary>
    public event Action<byte[], long> MetadataApplied;
    /// <summary>缓冲超限、超时或协议冲突时单次通知上层请求恢复。</summary>
    public event Action<string> RecoveryRequired;

    /// <summary>可靠有序应用生命周期；重复 Spawn/Despawn 按 V2 幂等规则处理。</summary>
    public bool ApplyLifecycle(ReplicationLifecycle lifecycle)
    {
        if (lifecycle == null) throw new ArgumentNullException(nameof(lifecycle));
        if (lifecycle.LifecycleSequence <= _latestLifecycleSequence) return true;
        if (lifecycle.LifecycleSequence != _latestLifecycleSequence + 1)
        {
            RequestRecovery($"生命周期缺口 {_latestLifecycleSequence}->{lifecycle.LifecycleSequence}。");
            return false;
        }

        ReplicatedEntityRegistry next = _registry.Clone();
        var publishedSpawns = new List<SpawnRecord>();
        var publishedDespawns = new List<DespawnRecord>();
        SpawnRecord[] spawns = lifecycle.SpawnBuffer;
        for (int i = 0; i < spawns.Length; i++)
        {
            SpawnRecord spawn = spawns[i];
            ValidatePayload(spawn.SchemaId, spawn.PayloadBuffer);
            if (next.TryGet(spawn.EntityId, out ReplicatedEntityMetadata existing))
            {
                if (existing.ArchetypeId != spawn.ArchetypeId || existing.SchemaId != spawn.SchemaId)
                {
                    RequestRecovery($"重复 Spawn {spawn.EntityId} 的原型或 Schema 冲突。");
                    return false;
                }
                continue;
            }
            if (next.TrySpawn(spawn.EntityId, spawn.ArchetypeId, spawn.SchemaId) != ReplicatedEntityOperationResult.Success)
            {
                RequestRecovery($"Spawn {spawn.EntityId} 被注册表拒绝。");
                return false;
            }
            publishedSpawns.Add(spawn);
        }

        DespawnRecord[] despawns = lifecycle.DespawnBuffer;
        for (int i = 0; i < despawns.Length; i++)
        {
            DespawnRecord despawn = despawns[i];
            if (!next.TryGet(despawn.EntityId, out _)) continue;
            next.TryDespawn(despawn.EntityId);
            publishedDespawns.Add(despawn);
        }
        _registry.ReplaceWith(next);
        _latestLifecycleSequence = lifecycle.LifecycleSequence;
        for (int i = 0; i < publishedSpawns.Count; i++) Spawned?.Invoke(publishedSpawns[i]);
        for (int i = 0; i < publishedDespawns.Count; i++)
        {
            DespawnRecord despawn = publishedDespawns[i];
            _latestEntityTicks.Remove(despawn.EntityId.Value);
            _bufferedByEntity.Remove(despawn.EntityId.Value);
            Despawned?.Invoke(despawn);
        }
        DrainBuffered();
        return true;
    }

    /// <summary>应用或按 requiredLifecycleSequence 缓冲不可靠时序快照。</summary>
    public ReplicationSnapshotApplyResult ApplySnapshot(ReplicationSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.RequiredLifecycleSequence > _latestLifecycleSequence)
        {
            Buffer(snapshot);
            CheckBarrierTimeout(snapshot.Tick.Value);
            return ReplicationSnapshotApplyResult.Buffered;
        }
        ApplyReadySnapshot(snapshot);
        return ReplicationSnapshotApplyResult.Applied;
    }

    /// <summary>清空注册表、屏障与恢复闩，等待权威重新可靠 Spawn。</summary>
    public void ResetForRecovery()
    {
        _registry.ReplaceWith(new ReplicatedEntityRegistry());
        _bufferedByEntity.Clear();
        _bufferedMetadata.Clear();
        _latestEntityTicks.Clear();
        _latestSnapshotTick = -1;
        _latestMetadata = Array.Empty<byte>();
        _recoveryRaised = false;
    }

    void ApplyReadySnapshot(ReplicationSnapshot snapshot)
    {
        EntityRecord[] updates = snapshot.UpdateBuffer;
        // 整批先验证再发布，避免坏记录之前的 Meta/Update 已产生不可回滚副作用。
        for (int i = 0; i < updates.Length; i++)
        {
            EntityRecord update = updates[i];
            if (!_registry.TryGet(update.EntityId, out ReplicatedEntityMetadata metadata) || metadata.SchemaId != update.SchemaId)
            {
                RequestRecovery($"Update {update.EntityId} 缺少匹配生命周期。");
                return;
            }
            ValidatePayload(update.SchemaId, update.PayloadBuffer);
        }
        if (snapshot.Tick.Value == _latestSnapshotTick
            && !BytesEqual(_latestMetadata, snapshot.MetadataBuffer))
        {
            RequestRecovery($"同 Tick {snapshot.Tick.Value} 的 Snapshot Meta 不一致。");
            return;
        }

        if (snapshot.Tick.Value > _latestSnapshotTick)
        {
            _latestSnapshotTick = snapshot.Tick.Value;
            _latestMetadata = (byte[])snapshot.MetadataBuffer.Clone();
            MetadataApplied?.Invoke((byte[])_latestMetadata.Clone(), snapshot.Tick.Value);
        }
        for (int i = 0; i < updates.Length; i++)
        {
            EntityRecord update = updates[i];
            if (_latestEntityTicks.TryGetValue(update.EntityId.Value, out long oldTick) && oldTick >= snapshot.Tick.Value)
                continue;
            _latestEntityTicks[update.EntityId.Value] = snapshot.Tick.Value;
            Updated?.Invoke(update, snapshot.Tick.Value);
        }
    }

    // 每实体只留最新 Tick；Meta 每 Tick 只留最新副本，避免乱序流量无界增长。
    void Buffer(ReplicationSnapshot snapshot)
    {
        EntityRecord[] updates = snapshot.UpdateBuffer;
        for (int i = 0; i < updates.Length; i++)
        {
            EntityRecord update = updates[i];
            if (!_bufferedByEntity.TryGetValue(update.EntityId.Value, out BufferedUpdate old) || old.Tick < snapshot.Tick.Value)
            {
                _bufferedByEntity[update.EntityId.Value] = new BufferedUpdate(
                    snapshot.Tick.Value,
                    snapshot.RequiredLifecycleSequence,
                    update,
                    snapshot.MetadataBuffer);
            }
        }
        _bufferedMetadata[snapshot.Tick.Value] = new BufferedMetadata(
            snapshot.RequiredLifecycleSequence,
            (byte[])snapshot.MetadataBuffer.Clone());
        while (_bufferedMetadata.Count > MaxBufferedTicks) _bufferedMetadata.Remove(FirstKey(_bufferedMetadata));
        if (_bufferedByEntity.Count > MaxBufferedEntities)
        {
            RequestRecovery("快照屏障缓冲实体数超限。");
            // 超限后仍保持硬上界；按最旧 Tick、再按 EntityId 淘汰。
            while (_bufferedByEntity.Count > MaxBufferedEntities)
                _bufferedByEntity.Remove(FindOldestBufferedEntity());
        }
    }

    // 生命周期推进后，按 Tick 稳定顺序释放已经满足屏障的最新实体状态。
    void DrainBuffered()
    {
        var ready = new List<BufferedUpdate>();
        foreach (BufferedUpdate item in _bufferedByEntity.Values)
            if (item.RequiredSequence <= _latestLifecycleSequence) ready.Add(item);
        ready.Sort((left, right) => left.Tick != right.Tick ? left.Tick.CompareTo(right.Tick) : left.Record.EntityId.CompareTo(right.Record.EntityId));
        for (int i = 0; i < ready.Count; i++)
        {
            BufferedUpdate item = ready[i];
            _bufferedByEntity.Remove(item.Record.EntityId.Value);
            ApplyReadySnapshot(new ReplicationSnapshot(
                new NetTick(item.Tick),
                item.RequiredSequence,
                0,
                1,
                new[] { item.Record },
                item.Metadata));
        }
        foreach (KeyValuePair<long, BufferedMetadata> pair in _bufferedMetadata)
        {
            if (pair.Value.RequiredSequence > _latestLifecycleSequence || pair.Key <= _latestSnapshotTick)
                continue;
            _latestSnapshotTick = pair.Key;
            MetadataApplied?.Invoke((byte[])pair.Value.Bytes.Clone(), pair.Key);
        }
        var remove = new List<long>();
        foreach (KeyValuePair<long, BufferedMetadata> pair in _bufferedMetadata)
            if (pair.Key <= _latestSnapshotTick) remove.Add(pair.Key);
        for (int i = 0; i < remove.Count; i++) _bufferedMetadata.Remove(remove[i]);
    }

    void CheckBarrierTimeout(long incomingTick)
    {
        if (_bufferedMetadata.Count == 0) return;
        long oldest = FirstKey(_bufferedMetadata);
        if (incomingTick - oldest >= BarrierTimeoutTicks) RequestRecovery("生命周期屏障等待超时。");
    }

    void ValidatePayload(ushort schemaId, byte[] payload) => _schemas.Decode(schemaId, payload);

    void RequestRecovery(string reason)
    {
        if (_recoveryRaised) return;
        _recoveryRaised = true;
        RecoveryRequired?.Invoke(reason);
    }

    static long FirstKey(SortedDictionary<long, BufferedMetadata> source)
    {
        foreach (long key in source.Keys) return key;
        return 0;
    }

    static int CompareBuffered(BufferedUpdate left, BufferedUpdate right)
    {
        int byTick = left.Tick.CompareTo(right.Tick);
        return byTick != 0 ? byTick : left.Record.EntityId.CompareTo(right.Record.EntityId);
    }

    static bool BytesEqual(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
            return false;
        for (int i = 0; i < left.Length; i++)
            if (left[i] != right[i]) return false;
        return true;
    }

    /// <summary>选择最旧 Tick、再按最小 EntityId 的缓冲项用于确定性淘汰。</summary>
    int FindOldestBufferedEntity()
    {
        bool found = false;
        int oldestId = 0;
        BufferedUpdate oldest = default;
        foreach (KeyValuePair<int, BufferedUpdate> pair in _bufferedByEntity)
        {
            if (!found || CompareBuffered(pair.Value, oldest) < 0)
            {
                found = true;
                oldestId = pair.Key;
                oldest = pair.Value;
            }
        }
        return oldestId;
    }

    readonly struct BufferedUpdate
    {
        /// <summary>保存实体最新状态及同批 Meta，避免 Meta Tick 窗淘汰后无法释放实体。</summary>
        public BufferedUpdate(long tick, long requiredSequence, EntityRecord record, byte[] metadata)
        {
            Tick = tick;
            RequiredSequence = requiredSequence;
            Record = record;
            Metadata = (byte[])metadata.Clone();
        }
        public long Tick { get; }
        public long RequiredSequence { get; }
        public EntityRecord Record { get; }
        public byte[] Metadata { get; }
    }

    readonly struct BufferedMetadata
    {
        public BufferedMetadata(long requiredSequence, byte[] bytes) { RequiredSequence = requiredSequence; Bytes = bytes; }
        public long RequiredSequence { get; }
        public byte[] Bytes { get; }
    }
}

/// <summary>快照已立即应用或正在等待生命周期屏障。</summary>
public enum ReplicationSnapshotApplyResult : byte
{
    /// <summary>记录已越过屏障。</summary>
    Applied = 0,
    /// <summary>记录已进入有界最新值缓冲。</summary>
    Buffered = 1,
}
