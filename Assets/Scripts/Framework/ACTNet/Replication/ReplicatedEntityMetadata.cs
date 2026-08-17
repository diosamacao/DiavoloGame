using System;

/// <summary>保存活动复制实体不可隐式改变的 Archetype 与 Schema 元数据。</summary>
public readonly struct ReplicatedEntityMetadata
{
    /// <summary>活动实体的稳定网络标识。</summary>
    public NetEntityId EntityId { get; }

    /// <summary>活动实体创建时选定的原型标识。</summary>
    public NetArchetypeId ArchetypeId { get; }

    /// <summary>活动实体当前接受的状态 Schema 标识。</summary>
    public ushort SchemaId { get; }

    /// <summary>验证并创建一份活动实体元数据。</summary>
    public ReplicatedEntityMetadata(
        NetEntityId entityId,
        NetArchetypeId archetypeId,
        ushort schemaId)
    {
        if (!entityId.IsValid)
            throw new ArgumentException("Metadata EntityId 必须有效。", nameof(entityId));
        if (!archetypeId.IsValid)
            throw new ArgumentException("Metadata ArchetypeId 必须有效。", nameof(archetypeId));
        if (schemaId == 0)
            throw new ArgumentOutOfRangeException(nameof(schemaId), "SchemaId 不能为 0。");

        EntityId = entityId;
        ArchetypeId = archetypeId;
        SchemaId = schemaId;
    }
}
