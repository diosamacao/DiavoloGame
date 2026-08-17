using System;
using System.Collections.Generic;

/// <summary>把每帧 authoritative full set 差分为显式 Spawn、Update 与 Despawn。</summary>
public sealed class ReplicationServer
{
    readonly ReplicatedEntityRegistry _registry = new ReplicatedEntityRegistry();
    NetSequence _nextSequence = new NetSequence(0);

    /// <summary>返回 Server 已提交上一帧活动实体注册表的独立快照。</summary>
    public ReplicatedEntityRegistry Registry => _registry.Clone();

    /// <summary>下一次成功构帧将使用的连接内序列号。</summary>
    public NetSequence NextSequence => _nextSequence;

    /// <summary>
    /// 从 authoritative full set 构建一帧；仅这里把上一帧存在而本帧缺失解释为 Despawn。
    /// </summary>
    public ReplicationFrame BuildFrame(
        NetTick tick,
        IEnumerable<ReplicationEntityState> fullSet,
        byte[] applicationPayload)
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
        var updates = new List<EntityRecord>();
        var despawns = new List<DespawnRecord>();
        ReplicatedEntityRegistry nextRegistry = _registry.Clone();

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
            updates.Add(new EntityRecord(state.EntityId, state.SchemaId, state.PayloadBuffer));
        }

        ReplicatedEntityMetadata[] previousEntities = _registry.GetAll();
        for (int i = 0; i < previousEntities.Length; i++)
        {
            NetEntityId entityId = previousEntities[i].EntityId;
            if (seen.Contains(entityId))
                continue;

            EnsureSuccess(nextRegistry.TryDespawn(entityId), entityId, "Despawn");
            despawns.Add(new DespawnRecord(entityId));
        }

        var frame = new ReplicationFrame(
            tick,
            _nextSequence,
            spawns.ToArray(),
            updates.ToArray(),
            despawns.ToArray(),
            applicationPayload);

        // 只有完整构帧成功后才提交 Registry 与 Sequence，异常不会留下半帧状态。
        _registry.ReplaceWith(nextRegistry);
        _nextSequence = _nextSequence.Next();
        return frame;
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
