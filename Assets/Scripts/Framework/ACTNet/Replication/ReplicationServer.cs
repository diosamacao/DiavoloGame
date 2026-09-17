using System;
using System.Collections.Generic;

/// <summary>为单连接纯准备 V2 增量，并仅在发送成功提交相应基线。</summary>
public sealed class ReplicationServer
{
    /// <summary>即使完整状态不变也周期重发，修复不可靠快照永久丢失。</summary>
    public const int MaxSilenceTicks = 30;

    /// <summary>非 Urgent 状态最短发送间隔；60Hz 模拟下明确按 30Hz 提交变化。</summary>
    public const int NonUrgentSendIntervalTicks = 2;

    readonly ReplicatedEntityRegistry _registry = new ReplicatedEntityRegistry();
    readonly Dictionary<int, byte[]> _lastCommittedPayloads = new();
    readonly Dictionary<int, long> _lastCommittedTicks = new();
    long _nextLifecycleSequence = 1;
    int _fairCursor;

    /// <summary>返回仅包含已提交生命周期的注册表副本。</summary>
    public ReplicatedEntityRegistry Registry => _registry.Clone();
    /// <summary>纯准备一个 Tick；不修改 registry、快照 cache 或公平游标。</summary>
    public ReplicationTickDelta PrepareTickDelta(
        NetTick tick,
        IEnumerable<ReplicationEntityState> fullSet,
        byte[] metadata,
        int bodyBudgetBytes,
        NetEntityId ownerEntity,
        bool forceFull = false)
    {
        if (!tick.IsValid) throw new ArgumentException("Tick 必须有效。", nameof(tick));
        if (fullSet == null) throw new ArgumentNullException(nameof(fullSet));
        if (metadata == null) throw new ArgumentNullException(nameof(metadata));
        if (bodyBudgetBytes < ReplicationProtocolV2Codec.SnapshotHeaderBytes + metadata.Length)
            throw new ArgumentOutOfRangeException(nameof(bodyBudgetBytes), "正文预算容不下 V2 Snapshot 头与 Meta。");

        ReplicationEntityState[] states = Materialize(fullSet);
        var currentIds = new HashSet<int>();
        var spawns = new List<ReplicationEntityState>();
        var despawns = new List<NetEntityId>();
        var updates = new List<ReplicationEntityState>();
        for (int i = 0; i < states.Length; i++)
        {
            ReplicationEntityState state = states[i];
            if (!currentIds.Add(state.EntityId.Value)) throw new InvalidOperationException($"重复 EntityId {state.EntityId}。");
            if (_registry.TryGet(state.EntityId, out ReplicatedEntityMetadata old))
            {
                if (old.ArchetypeId != state.ArchetypeId || old.SchemaId != state.SchemaId)
                    throw new InvalidOperationException($"Entity {state.EntityId} 生命周期内改变原型或 Schema。");
                if (forceFull)
                    spawns.Add(state);
            }
            else
            {
                spawns.Add(state);
            }

            if (forceFull || IsSnapshotDue(state, tick.Value)) updates.Add(state);
        }

        ReplicatedEntityMetadata[] previous = _registry.GetAll();
        for (int i = 0; i < previous.Length; i++)
        {
            if (!currentIds.Contains(previous[i].EntityId.Value)) despawns.Add(previous[i].EntityId);
        }

        var packets = new List<PreparedReplicationPacket>();
        long finalRequiredSequence = _nextLifecycleSequence - 1;
        PrepareLifecyclePackets(tick, spawns, despawns, bodyBudgetBytes, packets, ref finalRequiredSequence);
        PrepareSnapshotPackets(tick, finalRequiredSequence, updates, metadata, bodyBudgetBytes, ownerEntity, packets);
        return new ReplicationTickDelta(packets.ToArray());
    }

    /// <summary>提交一个已成功交给传输层的包；重复或外部 token 会明确失败。</summary>
    public void Commit(ReplicationPacketCommitToken token)
    {
        ReplicationCommitTicket ticket = token.Ticket;
        if (ticket == null)
            throw new InvalidOperationException("未知、外部或已结束的 replication commit token。");
        ticket.Finish(this, commit: true);
    }

    /// <summary>拒绝异常或背压包，不改变已提交基线，使下 Tick 自动重试。</summary>
    public void Reject(ReplicationPacketCommitToken token)
    {
        ReplicationCommitTicket ticket = token.Ticket;
        if (ticket == null)
            throw new InvalidOperationException("未知、外部或已结束的 replication commit token。");
        ticket.Finish(this, commit: false);
    }

    void PrepareLifecyclePackets(
        NetTick tick,
        List<ReplicationEntityState> spawns,
        List<NetEntityId> despawns,
        int budget,
        List<PreparedReplicationPacket> packets,
        ref long finalSequence)
    {
        if (spawns.Count == 0 && despawns.Count == 0) return;
        var groups = new List<LifecycleGroup>();
        var current = new LifecycleGroup();
        int bytes = ReplicationProtocolV2Codec.LifecycleHeaderBytes;
        for (int i = 0; i < spawns.Count; i++)
        {
            int cost = ReplicationProtocolV2Codec.SpawnRecordHeaderBytes + spawns[i].PayloadBuffer.Length;
            if (cost + ReplicationProtocolV2Codec.LifecycleHeaderBytes > budget) throw new NetBufferException("单条 Spawn 超出正文预算。");
            if (bytes + cost > budget && current.Count > 0) { groups.Add(current); current = new LifecycleGroup(); bytes = ReplicationProtocolV2Codec.LifecycleHeaderBytes; }
            current.Spawns.Add(spawns[i]); bytes += cost;
        }
        for (int i = 0; i < despawns.Count; i++)
        {
            const int cost = 4;
            if (bytes + cost > budget && current.Count > 0) { groups.Add(current); current = new LifecycleGroup(); bytes = ReplicationProtocolV2Codec.LifecycleHeaderBytes; }
            current.Despawns.Add(despawns[i]); bytes += cost;
        }
        if (current.Count > 0) groups.Add(current);

        long preparedBaseSequence = _nextLifecycleSequence;
        for (int i = 0; i < groups.Count; i++)
        {
            long sequence = preparedBaseSequence + i;
            LifecycleGroup group = groups[i];
            var spawnRecords = new SpawnRecord[group.Spawns.Count];
            for (int j = 0; j < spawnRecords.Length; j++)
            {
                ReplicationEntityState state = group.Spawns[j];
                spawnRecords[j] = new SpawnRecord(state.EntityId, state.ArchetypeId, state.SchemaId, state.PayloadBuffer);
            }
            var despawnRecords = new DespawnRecord[group.Despawns.Count];
            for (int j = 0; j < despawnRecords.Length; j++) despawnRecords[j] = new DespawnRecord(group.Despawns[j]);
            var message = new ReplicationLifecycle(sequence, tick, (ushort)i, (ushort)groups.Count, spawnRecords, despawnRecords);
            AddPreparedPacket(true, ReplicationProtocolV2Codec.EncodeLifecycle(message, budget), new LifecycleCommit(sequence, group), packets);
            finalSequence = sequence;
        }
    }

    void PrepareSnapshotPackets(
        NetTick tick,
        long requiredSequence,
        List<ReplicationEntityState> updates,
        byte[] metadata,
        int budget,
        NetEntityId owner,
        List<PreparedReplicationPacket> packets)
    {
        if (updates.Count == 0 && metadata.Length == 0) return;
        SortSnapshotPriority(updates, owner);
        var groups = new List<List<ReplicationEntityState>>();
        var current = new List<ReplicationEntityState>();
        int bytes = ReplicationProtocolV2Codec.SnapshotHeaderBytes + metadata.Length;
        for (int i = 0; i < updates.Count; i++)
        {
            int cost = ReplicationProtocolV2Codec.UpdateRecordHeaderBytes + updates[i].PayloadBuffer.Length;
            if (cost + ReplicationProtocolV2Codec.SnapshotHeaderBytes + metadata.Length > budget) throw new NetBufferException("单条 Update 超出正文预算。");
            if (bytes + cost > budget && current.Count > 0) { groups.Add(current); current = new List<ReplicationEntityState>(); bytes = ReplicationProtocolV2Codec.SnapshotHeaderBytes + metadata.Length; }
            current.Add(updates[i]); bytes += cost;
        }
        if (current.Count > 0 || groups.Count == 0) groups.Add(current);
        for (int i = 0; i < groups.Count; i++)
        {
            List<ReplicationEntityState> group = groups[i];
            var records = new EntityRecord[group.Count];
            for (int j = 0; j < records.Length; j++) records[j] = new EntityRecord(group[j].EntityId, group[j].SchemaId, group[j].PayloadBuffer);
            var message = new ReplicationSnapshot(tick, requiredSequence, (ushort)i, (ushort)groups.Count, records, metadata);
            AddPreparedPacket(false, ReplicationProtocolV2Codec.EncodeSnapshot(message, budget), new SnapshotCommit(tick.Value, group), packets);
        }
    }

    // 票据只随返回包存在；Prepare 不向服务端登记任何 pending 状态。
    void AddPreparedPacket(bool lifecycle, byte[] body, PendingCommit commit, List<PreparedReplicationPacket> packets)
    {
        var ticket = new ReplicationCommitTicket(this, commit.Apply);
        var token = new ReplicationPacketCommitToken(Guid.NewGuid(), ticket);
        packets.Add(new PreparedReplicationPacket(lifecycle, body, token));
    }

    /// <summary>Urgent 立即发送；非 Urgent 变化遵守节拍，未变状态由 MaxSilence 保底。</summary>
    bool IsSnapshotDue(ReplicationEntityState state, long tick)
    {
        if (state.Urgent || !_lastCommittedPayloads.TryGetValue(state.EntityId.Value, out byte[] previous)) return true;
        if (!_lastCommittedTicks.TryGetValue(state.EntityId.Value, out long last)) return true;
        long elapsed = tick - last;
        if (!BytesEqual(previous, state.PayloadBuffer))
            return elapsed >= NonUrgentSendIntervalTicks;
        return elapsed >= MaxSilenceTicks;
    }

    void SortSnapshotPriority(List<ReplicationEntityState> updates, NetEntityId owner)
    {
        updates.Sort((left, right) =>
        {
            int lp = owner.IsValid && left.EntityId == owner ? 0 : left.Urgent ? 1 : 2;
            int rp = owner.IsValid && right.EntityId == owner ? 0 : right.Urgent ? 1 : 2;
            if (lp != rp) return lp.CompareTo(rp);
            if (lp < 2) return left.EntityId.CompareTo(right.EntityId);
            int lc = (left.EntityId.Value - _fairCursor) & int.MaxValue;
            int rc = (right.EntityId.Value - _fairCursor) & int.MaxValue;
            return lc != rc ? lc.CompareTo(rc) : left.EntityId.CompareTo(right.EntityId);
        });
    }

    static ReplicationEntityState[] Materialize(IEnumerable<ReplicationEntityState> source)
    {
        var list = new List<ReplicationEntityState>();
        foreach (ReplicationEntityState state in source) list.Add(state ?? throw new ArgumentException("Full set 不能包含 null。", nameof(source)));
        list.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        return list.ToArray();
    }

    static bool BytesEqual(byte[] left, byte[] right)
    {
        if (left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
        return true;
    }

    abstract class PendingCommit { public abstract void Apply(ReplicationServer server); }

    sealed class LifecycleCommit : PendingCommit
    {
        readonly long _sequence;
        readonly LifecycleGroup _group;
        public LifecycleCommit(long sequence, LifecycleGroup group) { _sequence = sequence; _group = group; }
        public long Sequence => _sequence;
        public override void Apply(ReplicationServer server)
        {
            if (_sequence != server._nextLifecycleSequence)
                throw new InvalidOperationException(
                    $"生命周期提交必须连续：expected={server._nextLifecycleSequence}, actual={_sequence}。");
            for (int i = 0; i < _group.Spawns.Count; i++)
            {
                ReplicationEntityState state = _group.Spawns[i];
                if (!server._registry.TryGet(state.EntityId, out _)) server._registry.TrySpawn(state.EntityId, state.ArchetypeId, state.SchemaId);
            }
            for (int i = 0; i < _group.Despawns.Count; i++)
            {
                NetEntityId id = _group.Despawns[i];
                server._registry.TryDespawn(id);
                server._lastCommittedPayloads.Remove(id.Value);
                server._lastCommittedTicks.Remove(id.Value);
            }
            server._nextLifecycleSequence = _sequence + 1;
        }
    }

    sealed class SnapshotCommit : PendingCommit
    {
        readonly long _tick;
        readonly List<ReplicationEntityState> _states;
        public SnapshotCommit(long tick, List<ReplicationEntityState> states) { _tick = tick; _states = states; }
        public override void Apply(ReplicationServer server)
        {
            for (int i = 0; i < _states.Count; i++)
            {
                ReplicationEntityState state = _states[i];
                server._lastCommittedPayloads[state.EntityId.Value] = (byte[])state.PayloadBuffer.Clone();
                server._lastCommittedTicks[state.EntityId.Value] = _tick;
            }
            if (_states.Count > 0) server._fairCursor = _states[_states.Count - 1].EntityId.Value + 1;
        }
    }

    sealed class LifecycleGroup
    {
        public readonly List<ReplicationEntityState> Spawns = new();
        public readonly List<NetEntityId> Despawns = new();
        public int Count => Spawns.Count + Despawns.Count;
    }
}
