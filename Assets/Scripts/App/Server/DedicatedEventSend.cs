/// <summary>权威步内为单连接编好的可靠事件正文，由 Runtime 走 Event 通道发送。</summary>
public readonly struct DedicatedEventSend
{
    /// <summary>绑定目标连接与已编码正文；body 不得为 null。</summary>
    public DedicatedEventSend(NetConnectionId connectionId, byte[] body)
    {
        ConnectionId = connectionId;
        Body = body ?? System.Array.Empty<byte>();
    }

    /// <summary>该事件要发给的连接。</summary>
    public NetConnectionId ConnectionId { get; }

    /// <summary>ActReplicationEventCodec 编码结果，不含 Session 信封。</summary>
    public byte[] Body { get; }
}
