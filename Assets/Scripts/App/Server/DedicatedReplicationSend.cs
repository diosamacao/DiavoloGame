/// <summary>权威步内为单连接编好的 ReplicationFrame 正文，由 Runtime 发送。</summary>
public readonly struct DedicatedReplicationSend
{
    /// <summary>绑定目标连接与已编码正文；body 不得为 null。</summary>
    public DedicatedReplicationSend(NetConnectionId connectionId, byte[] body)
    {
        ConnectionId = connectionId;
        Body = body ?? System.Array.Empty<byte>();
    }

    /// <summary>该帧要发给的连接。</summary>
    public NetConnectionId ConnectionId { get; }

    /// <summary>ReplicationFrameCodec 编码结果，不含 Session 信封。</summary>
    public byte[] Body { get; }
}
