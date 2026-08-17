using System;

/// <summary>描述客户端必须显式创建的复制实体及其首份状态。</summary>
public sealed class SpawnRecord
{
    readonly byte[] _payload;

    /// <summary>待创建实体的稳定网络标识。</summary>
    public NetEntityId EntityId { get; }

    /// <summary>供后续业务 Factory 选择实体原型的标识。</summary>
    public NetArchetypeId ArchetypeId { get; }

    /// <summary>解释首份状态载荷的 Schema 标识。</summary>
    public ushort SchemaId { get; }

    /// <summary>返回首份状态载荷的独立副本。</summary>
    public byte[] Payload => Clone(_payload);

    /// <summary>创建并复制一条有效的 Spawn 记录。</summary>
    public SpawnRecord(
        NetEntityId entityId,
        NetArchetypeId archetypeId,
        ushort schemaId,
        byte[] payload)
    {
        if (!entityId.IsValid)
            throw new ArgumentException("Spawn EntityId 必须有效。", nameof(entityId));
        if (!archetypeId.IsValid)
            throw new ArgumentException("Spawn ArchetypeId 必须有效。", nameof(archetypeId));
        if (schemaId == 0)
            throw new ArgumentOutOfRangeException(nameof(schemaId), "SchemaId 不能为 0。");
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));

        EntityId = entityId;
        ArchetypeId = archetypeId;
        SchemaId = schemaId;
        _payload = Clone(payload);
    }

    /// <summary>供同程序集 Codec 使用已隔离的只读载荷，避免重复复制。</summary>
    internal byte[] PayloadBuffer => _payload;

    static byte[] Clone(byte[] value)
    {
        if (value.Length == 0)
            return Array.Empty<byte>();

        var copy = new byte[value.Length];
        Buffer.BlockCopy(value, 0, copy, 0, value.Length);
        return copy;
    }
}
