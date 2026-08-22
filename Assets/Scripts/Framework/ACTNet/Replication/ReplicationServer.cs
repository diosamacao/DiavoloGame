using System;
using System.Collections.Generic;

/// <summary>
/// 把 authoritative full set 差分为 Spawn / Update / Despawn。
/// 未变载荷可跳过 Update；节拍与字节预算只约束 Update，不漏 Spawn/Despawn。
/// </summary>
public sealed class ReplicationServer
{
    readonly ReplicatedEntityRegistry _registry = new ReplicatedEntityRegistry();
    readonly Dictionary<int, byte[]> _lastSentPayloads = new();
    NetSequence _nextSequence = new NetSequence(0);

    /// <summary>返回 Server 已提交上一帧活动实体注册表的独立快照。</summary>
    public ReplicatedEntityRegistry Registry => _registry.Clone();

    /// <summary>下一次成功构帧将使用的连接内序列号。</summary>
    public NetSequence NextSequence => _nextSequence;

    /// <summary>上一帧跳过的未变 Update 数，供预算/带宽测试读取。</summary>
    public int LastSkippedUnchanged { get; private set; }

    /// <summary>上一帧实际发出的 Update 条数。</summary>
    public int LastEmittedUpdates { get; private set; }

    /// <summary>上一帧计入预算的 Update 字节（含记录头估算）。</summary>
    public int LastUpdateBytes { get; private set; }

    /// <summary>
    /// 从 authoritative full set 构建一帧；仅这里把上一帧存在而本帧缺失解释为 Despawn。
    /// </summary>
    public ReplicationFrame BuildFrame(
        NetTick tick,
        IEnumerable<ReplicationEntityState> fullSet,
        byte[] applicationPayload) =>
        BuildFrame(tick, fullSet, applicationPayload, ReplicationBuildOptions.Compatible);

    /// <summary>按选项构帧。ForceFull 前应先 <see cref="ResetBaseline"/> 才能重新 Spawn。</summary>
    public ReplicationFrame BuildFrame(
        NetTick tick,
        IEnumerable<ReplicationEntityState> fullSet,
        byte[] applicationPayload,
        ReplicationBuildOptions options)
    {
        if (!tick.IsValid)
            throw new ArgumentException("Server 构帧 Tick 必须有效。", nameof(tick));
        if (fullSet == null)
            throw new ArgumentNullException(nameof(fullSet));
        if (applicationPayload == null)
            throw new ArgumentNullException(nameof(applicationPayload));

        ReplicationEntityState[] current = MaterializeAndSort(fullSet);
        var seen = new HashSet<NetEntityId>();
        var spawns = new List<SpawnRecord>();
        var pendingUpdates = new List<ReplicationEntityState>();
        var despawns = new List<DespawnRecord>();
        ReplicatedEntityRegistry nextRegistry = _registry.Clone();
        var nextPayloads = ClonePayloads(_lastSentPayloads);
        int skipped = 0;

        for (int i = 0; i < current.Length; i++)
        {
            ReplicationEntityState state = current[i];
            if (!seen.Add(state.EntityId))
                throw new InvalidOperationException($"Full set 包含重复 EntityId {state.EntityId}。");

            if (!_registry.TryGet(state.EntityId, out ReplicatedEntityMetadata previous))
            {
                EnsureSuccess(
                    nextRegistry.TrySpawn(state.EntityId, state.ArchetypeId, state.SchemaId),
                    state.EntityId,
                    "Spawn");
                spawns.Add(new SpawnRecord(
                    state.EntityId,
                    state.ArchetypeId,
                    state.SchemaId,
                    state.PayloadBuffer));
                nextPayloads[state.EntityId.Value] = CloneBytes(state.PayloadBuffer);
                continue;
            }

            // 活动实体的原型或 Schema 不能在 Update 中偷换，必须由显式 Despawn/Spawn 建新生命周期。
            if (previous.ArchetypeId != state.ArchetypeId)
            {
                throw new InvalidOperationException(
                    $"Entity {state.EntityId} 的 Archetype 从 {previous.ArchetypeId} 变为 {state.ArchetypeId}。");
            }

            EnsureSuccess(
                nextRegistry.TryUpdate(state.EntityId, state.SchemaId),
                state.EntityId,
                "Update");

            bool dirty = options.ForceFull
                || !options.SkipUnchanged
                || !HasSamePayload(nextPayloads, state);
            if (!dirty)
            {
                skipped++;
                continue;
            }

            bool preferred = options.PreferredEntity.IsValid
                && state.EntityId.Equals(options.PreferredEntity);
            // Urgent：出招/受击必须当步发出，否则奇数 Tick 会丢掉受击边沿。
            bool due = options.ForceFull
                || preferred
                || state.Urgent
                || tick.Value % options.SnapshotIntervalTicks == 0;
            if (!due)
                continue;

            pendingUpdates.Add(state);
        }

        ReplicatedEntityMetadata[] previousEntities = _registry.GetAll();
        for (int i = 0; i < previousEntities.Length; i++)
        {
            NetEntityId entityId = previousEntities[i].EntityId;
            if (seen.Contains(entityId))
                continue;

            EnsureSuccess(nextRegistry.TryDespawn(entityId), entityId, "Despawn");
            despawns.Add(new DespawnRecord(entityId));
            nextPayloads.Remove(entityId.Value);
        }

        List<EntityRecord> updates = PackUpdates(pendingUpdates, options, nextPayloads, out int updateBytes);

        var frame = new ReplicationFrame(
            tick,
            _nextSequence,
            spawns.ToArray(),
            updates.ToArray(),
            despawns.ToArray(),
            applicationPayload);

        // 只有完整构帧成功后才提交 Registry 与 Sequence，异常不会留下半帧状态。
        _registry.ReplaceWith(nextRegistry);
        ReplacePayloads(nextPayloads);
        _nextSequence = _nextSequence.Next();
        LastSkippedUnchanged = skipped;
        LastEmittedUpdates = updates.Count;
        LastUpdateBytes = updateBytes;
        return frame;
    }

    /// <summary>清空已发送基线，使下一帧把仍在 full set 的实体重新 Spawn。</summary>
    public void ResetBaseline()
    {
        _registry.ReplaceWith(new ReplicatedEntityRegistry());
        _lastSentPayloads.Clear();
    }

    /// <summary>按 Owner 优先再按 EntityId 填满 Update 预算；装不下的保持旧基线以便下帧重试。</summary>
    static List<EntityRecord> PackUpdates(
        List<ReplicationEntityState> pending,
        ReplicationBuildOptions options,
        Dictionary<int, byte[]> nextPayloads,
        out int updateBytes)
    {
        pending.Sort((left, right) => CompareUpdatePriority(left, right, options.PreferredEntity));
        var updates = new List<EntityRecord>(pending.Count);
        updateBytes = 0;
        int budget = options.MaxUpdateBytes;
        for (int i = 0; i < pending.Count; i++)
        {
            ReplicationEntityState state = pending[i];
            int cost = EstimateUpdateBytes(state.PayloadBuffer.Length);
            if (budget > 0 && updateBytes + cost > budget && updates.Count > 0)
                continue;

            updates.Add(new EntityRecord(state.EntityId, state.SchemaId, state.PayloadBuffer));
            nextPayloads[state.EntityId.Value] = CloneBytes(state.PayloadBuffer);
            updateBytes += cost;
        }

        return updates;
    }

    static int CompareUpdatePriority(
        ReplicationEntityState left,
        ReplicationEntityState right,
        NetEntityId preferred)
    {
        bool leftPreferred = preferred.IsValid && left.EntityId.Equals(preferred);
        bool rightPreferred = preferred.IsValid && right.EntityId.Equals(preferred);
        if (leftPreferred != rightPreferred)
            return leftPreferred ? -1 : 1;
        return left.EntityId.CompareTo(right.EntityId);
    }

    /// <summary>与 ReplicationFrameCodec Update 记录头大致对齐：id + schema + 长度 + payload。</summary>
    static int EstimateUpdateBytes(int payloadLength) => 10 + payloadLength;

    static bool HasSamePayload(Dictionary<int, byte[]> payloads, ReplicationEntityState state)
    {
        if (!payloads.TryGetValue(state.EntityId.Value, out byte[] previous) || previous == null)
            return false;
        return BytesEqual(previous, state.PayloadBuffer);
    }

    static bool BytesEqual(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
            return false;
        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
                return false;
        }

        return true;
    }

    static Dictionary<int, byte[]> ClonePayloads(Dictionary<int, byte[]> source)
    {
        var copy = new Dictionary<int, byte[]>(source.Count);
        foreach (KeyValuePair<int, byte[]> pair in source)
            copy[pair.Key] = pair.Value;
        return copy;
    }

    void ReplacePayloads(Dictionary<int, byte[]> next)
    {
        _lastSentPayloads.Clear();
        foreach (KeyValuePair<int, byte[]> pair in next)
            _lastSentPayloads[pair.Key] = pair.Value;
    }

    static byte[] CloneBytes(byte[] value)
    {
        if (value == null || value.Length == 0)
            return Array.Empty<byte>();
        var copy = new byte[value.Length];
        Buffer.BlockCopy(value, 0, copy, 0, value.Length);
        return copy;
    }

    // 一次枚举 full set 并拒绝 null，随后按 EntityId 固定后续差分顺序。
    static ReplicationEntityState[] MaterializeAndSort(
        IEnumerable<ReplicationEntityState> fullSet)
    {
        var states = new List<ReplicationEntityState>();
        foreach (ReplicationEntityState state in fullSet)
        {
            if (state == null)
                throw new ArgumentException("Full set 不能包含 null。", nameof(fullSet));
            states.Add(state);
        }

        ReplicationEntityState[] result = states.ToArray();
        Array.Sort(result, (left, right) => left.EntityId.CompareTo(right.EntityId));
        return result;
    }

    static void EnsureSuccess(
        ReplicatedEntityOperationResult result,
        NetEntityId entityId,
        string operation)
    {
        if (result != ReplicatedEntityOperationResult.Success)
            throw new InvalidOperationException($"{operation} Entity {entityId} 失败：{result}。");
    }
}
