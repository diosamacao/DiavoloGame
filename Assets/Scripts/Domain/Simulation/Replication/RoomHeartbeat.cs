/// <summary>房间心跳；客机填 SendTimeMs，权威回显同一值到 EchoTimeMs。</summary>
public readonly struct RoomHeartbeat
{
    /// <summary>创建心跳。</summary>
    public RoomHeartbeat(long sendTimeMs, long echoTimeMs)
    {
        SendTimeMs = sendTimeMs;
        EchoTimeMs = echoTimeMs;
    }

    /// <summary>客机发送时的单调毫秒。</summary>
    public long SendTimeMs { get; }

    /// <summary>权威回显的客机发送时刻；请求包为 0。</summary>
    public long EchoTimeMs { get; }
}
