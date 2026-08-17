/// <summary>保持既有一字节线格式的 Session 主动终止原因。</summary>
public enum SessionKickReason : byte
{
    /// <summary>连接超过空闲时限。</summary>
    IdleTimeout = 1,

    /// <summary>权威进程主动结束 Session。</summary>
    ServerEnded = 2,
}
