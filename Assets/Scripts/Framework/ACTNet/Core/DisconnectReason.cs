/// <summary>跨 Transport / Session 共用的稳定断开原因。</summary>
public enum DisconnectReason : ushort
{
    /// <summary>未发生断开。</summary>
    None = 0,

    /// <summary>本端主动结束连接。</summary>
    Requested = 1,

    /// <summary>线协议版本不匹配。</summary>
    ProtocolMismatch = 2,

    /// <summary>Gameplay Content 指纹不匹配。</summary>
    ContentMismatch = 3,

    /// <summary>Session 已达到容量上限。</summary>
    ServerFull = 4,

    /// <summary>连接或心跳超时。</summary>
    Timeout = 5,

    /// <summary>底层传输失败。</summary>
    TransportError = 6,

    /// <summary>载荷损坏、越界或不符合消息契约。</summary>
    MalformedPacket = 7,

    /// <summary>认证或所有权校验失败。</summary>
    Unauthorized = 8,

    /// <summary>服务器主动停服或结束 Match。</summary>
    ServerShutdown = 9,

    /// <summary>未分类的内部错误。</summary>
    InternalError = 10,
}
