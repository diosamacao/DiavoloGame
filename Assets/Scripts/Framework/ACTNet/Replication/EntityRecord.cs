using System;

/// <summary>描述一个已活动复制实体的 Schema 状态更新。</summary>
public sealed class EntityRecord
{
    readonly byte[] _payload;

    /// <summary>待更新实体的稳定网络标识。</summary>
    public NetEntityId EntityId { get; }

    /// <summary>解释更新载荷的 Schema 标识。</summary>
    public ushort SchemaId { get; }

    /// <summary>返回更新载荷的独立副本。</summary>
    public byte[] Payload => Clone(_payload);

    /// <summary>创建并复制一条有效的实体更新记录。</summary>
    public EntityRecord(NetEntityId entityId, ushort schemaId, byte[] payload)
    {
        if (!entityId.IsValid)
            throw new ArgumentException("Update EntityId 必须有效。", nameof(entityId));
        if (schemaId == 0)
            throw new ArgumentOutOfRangeException(nameof(schemaId), "SchemaId 不能为 0。");
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));

        EntityId = entityId;
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
