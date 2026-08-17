/// <summary>定义业务状态对象与纯字节复制载荷之间的版本化边界。</summary>
public interface IReplicationSchema
{
    /// <summary>返回非零且在注册表内唯一的 Schema 标识。</summary>
    ushort SchemaId { get; }

    /// <summary>把业务状态编码为独立字节载荷；不得返回 null。</summary>
    byte[] Encode(object state);

    /// <summary>验证并解码完整载荷；非法载荷应抛出明确异常。</summary>
    object Decode(byte[] payload);
}
