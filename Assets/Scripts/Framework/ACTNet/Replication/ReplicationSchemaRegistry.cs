using System;
using System.Collections.Generic;

/// <summary>按非零 SchemaId 保存通用复制载荷编解码器。</summary>
public sealed class ReplicationSchemaRegistry
{
    readonly Dictionary<ushort, IReplicationSchema> _schemas =
        new Dictionary<ushort, IReplicationSchema>();

    /// <summary>当前已注册 Schema 数量。</summary>
    public int Count => _schemas.Count;

    /// <summary>注册 Schema；拒绝 null、SchemaId=0 与重复标识。</summary>
    public void Register(IReplicationSchema schema)
    {
        if (schema == null)
            throw new ArgumentNullException(nameof(schema));
        if (schema.SchemaId == 0)
            throw new ArgumentOutOfRangeException(nameof(schema), "SchemaId 不能为 0。");
        if (_schemas.ContainsKey(schema.SchemaId))
            throw new InvalidOperationException($"SchemaId {schema.SchemaId} 已注册。");

        _schemas.Add(schema.SchemaId, schema);
    }

    /// <summary>尝试按 SchemaId 获取已注册实现。</summary>
    public bool TryGet(ushort schemaId, out IReplicationSchema schema)
    {
        if (schemaId == 0)
        {
            schema = null;
            return false;
        }

        return _schemas.TryGetValue(schemaId, out schema);
    }

    /// <summary>使用已注册 Schema 编码状态，并拒绝 null 返回值。</summary>
    public byte[] Encode(ushort schemaId, object state)
    {
        IReplicationSchema schema = GetRequired(schemaId);
        byte[] payload = schema.Encode(state);
        if (payload == null)
            throw new InvalidOperationException($"Schema {schemaId} Encode 返回了 null。");
        return Clone(payload);
    }

    /// <summary>复制输入载荷后交给已注册 Schema 严格解码。</summary>
    public object Decode(ushort schemaId, byte[] payload)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));
        return GetRequired(schemaId).Decode(Clone(payload));
    }

    // 统一所有调用点对零 Id 与未注册 Schema 的失败语义。
    IReplicationSchema GetRequired(ushort schemaId)
    {
        if (schemaId == 0)
            throw new ArgumentOutOfRangeException(nameof(schemaId), "SchemaId 不能为 0。");
        if (!_schemas.TryGetValue(schemaId, out IReplicationSchema schema))
            throw new KeyNotFoundException($"SchemaId {schemaId} 未注册。");
        return schema;
    }

    static byte[] Clone(byte[] value)
    {
        if (value.Length == 0)
            return Array.Empty<byte>();

        var copy = new byte[value.Length];
        Buffer.BlockCopy(value, 0, copy, 0, value.Length);
        return copy;
    }
}
