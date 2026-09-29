using System;

/// <summary>客户端时间戳与服务端回显时间戳。</summary>
public readonly struct SessionHeartbeat
{
    /// <summary>创建心跳请求或回显。</summary>
    public SessionHeartbeat(long sendTimeMs, long echoTimeMs)
    {
        SendTimeMs = sendTimeMs;
        EchoTimeMs = echoTimeMs;
    }

    /// <summary>客户端发出请求的毫秒时刻。</summary>
    public long SendTimeMs { get; }

    /// <summary>服务端回显的客户端时刻；请求为 0。</summary>
    public long EchoTimeMs { get; }
}
