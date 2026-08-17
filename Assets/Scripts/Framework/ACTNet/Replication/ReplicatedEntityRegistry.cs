using System.Collections.Generic;

/// <summary>以 EntityId 为键维护显式 Spawn/Update/Despawn 的活动实体元数据。</summary>
public sealed class ReplicatedEntityRegistry
{
    readonly Dictionary<NetEntityId, ReplicatedEntityMetadata> _entities =
        new Dictionary<NetEntityId, ReplicatedEntityMetadata>();

    /// <summary>当前活动实体数量。</summary>
    public int Count => _entities.Count;

    /// <summary>尝试显式创建实体，并返回重复或字段非法等明确结果。</summary>
    public ReplicatedEntityOperationResult TrySpawn(
        NetEntityId entityId,
        NetArchetypeId archetypeId,
        ushort schemaId)
    {
        if (!entityId.IsValid)
            return ReplicatedEntityOperationResult.InvalidEntityId;
        if (!archetypeId.IsValid)
            return ReplicatedEntityOperationResult.InvalidArchetypeId;
        if (schemaId == 0)
            return ReplicatedEntityOperationResult.InvalidSchemaId;
        if (_entities.ContainsKey(entityId))
            return ReplicatedEntityOperationResult.DuplicateSpawn;

        _entities.Add(
            entityId,
            new ReplicatedEntityMetadata(entityId, archetypeId, schemaId));
        return ReplicatedEntityOperationResult.Success;
    }

    /// <summary>验证实体存在且 Schema 匹配；不会为未知实体隐式创建元数据。</summary>
    public ReplicatedEntityOperationResult TryUpdate(NetEntityId entityId, ushort schemaId)
    {
        if (!entityId.IsValid)
            return ReplicatedEntityOperationResult.InvalidEntityId;
        if (schemaId == 0)
            return ReplicatedEntityOperationResult.InvalidSchemaId;
        if (!_entities.TryGetValue(entityId, out ReplicatedEntityMetadata metadata))
            return ReplicatedEntityOperationResult.UnknownEntity;
        if (metadata.SchemaId != schemaId)
            return ReplicatedEntityOperationResult.SchemaMismatch;
        return ReplicatedEntityOperationResult.Success;
    }

    /// <summary>尝试显式移除实体；未知实体不会被静默忽略。</summary>
    public ReplicatedEntityOperationResult TryDespawn(NetEntityId entityId)
    {
        if (!entityId.IsValid)
            return ReplicatedEntityOperationResult.InvalidEntityId;
        if (!_entities.Remove(entityId))
            return ReplicatedEntityOperationResult.UnknownEntity;
        return ReplicatedEntityOperationResult.Success;
    }

    /// <summary>尝试读取活动实体元数据。</summary>
    public bool TryGet(NetEntityId entityId, out ReplicatedEntityMetadata metadata) =>
        _entities.TryGetValue(entityId, out metadata);

    /// <summary>返回按 EntityId 稳定排序的活动实体元数据快照。</summary>
    public ReplicatedEntityMetadata[] GetAll()
    {
        var result = new ReplicatedEntityMetadata[_entities.Count];
        int index = 0;
        foreach (ReplicatedEntityMetadata metadata in _entities.Values)
            result[index++] = metadata;

        System.Array.Sort(
            result,
            (left, right) => left.EntityId.CompareTo(right.EntityId));
        return result;
    }

    /// <summary>创建独立注册表副本，供帧应用在提交前完成原子验证。</summary>
    public ReplicatedEntityRegistry Clone()
    {
        var clone = new ReplicatedEntityRegistry();
        foreach (KeyValuePair<NetEntityId, ReplicatedEntityMetadata> pair in _entities)
            clone._entities.Add(pair.Key, pair.Value);
        return clone;
    }

    /// <summary>用另一个注册表的完整快照替换当前内容。</summary>
    public void ReplaceWith(ReplicatedEntityRegistry source)
    {
        if (source == null)
            throw new System.ArgumentNullException(nameof(source));

        _entities.Clear();
        foreach (KeyValuePair<NetEntityId, ReplicatedEntityMetadata> pair in source._entities)
            _entities.Add(pair.Key, pair.Value);
    }
}
