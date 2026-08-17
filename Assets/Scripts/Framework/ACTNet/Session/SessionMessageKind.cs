/// <summary>Session 保留的控制消息类型；其余类型作为应用层消息透传。</summary>
public enum SessionMessageKind : byte
{
    /// <summary>客户端请求建立 Session。</summary>
    JoinRequest = 1,

    /// <summary>服务端完成身份与实体分配。</summary>
    JoinAccept = 2,

    /// <summary>服务端拒绝建立 Session。</summary>
    JoinReject = 3,

    /// <summary>双向保活与 RTT 回显。</summary>
    Heartbeat = 4,

    /// <summary>服务端主动终止 Session。</summary>
    Kick = 7,
}
