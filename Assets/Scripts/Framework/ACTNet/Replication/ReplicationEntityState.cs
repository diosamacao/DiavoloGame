using System;

/// <summary>表示 Server authoritative full set 中一个实体的完整复制状态。</summary>
public sealed class ReplicationEntityState
{
    readonly byte[] _payload;

    /// <summary>实体的稳定网络标识。</summary>
    public NetEntityId EntityId { get; }

    /// <summary>实体创建所需的原型标识。</summary>
    public NetArchetypeId ArchetypeId { get; }

    /// <summary>解释完整状态载荷的 Schema 标识。</summary>
    public ushort SchemaId { get; }

    /// <summary>返回完整状态载荷的独立副本。</summary>
    public byte[] Payload => Clone(_payload);

    /// <summary>验证并复制一份权威实体状态。</summary>
    public ReplicationEntityState(
        NetEntityId entityId,
        NetArchetypeId archetypeId,
        ushort schemaId,
        byte[] payload)
    {
        if (!entityId.IsValid)
            throw new ArgumentException("ReplicationEntityState EntityId 必须有效。", nameof(entityId));
        if (!archetypeId.IsValid)
            throw new ArgumentException(
                "ReplicationEntityState ArchetypeId 必须有效。",
                nameof(archetypeId));
        if (schemaId == 0)
            throw new ArgumentOutOfRangeException(nameof(schemaId), "SchemaId 不能为 0。");
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));

        EntityId = entityId;
        ArchetypeId = archetypeId;
        SchemaId = schemaId;
        _payload = Clone(payload);
    }

    /// <summary>供同程序集 Server 构帧时读取已隔离载荷。</summary>
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
