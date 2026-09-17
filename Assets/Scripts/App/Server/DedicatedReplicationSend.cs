/// <summary>权威步内为单连接准备的 V2 包；Runtime 必须 Commit 或 Reject。</summary>
public readonly struct DedicatedReplicationSend
{
    /// <summary>绑定目标连接与已编码正文；body 不得为 null。</summary>
    public DedicatedReplicationSend(NetConnectionId connectionId, bool lifecycle, byte[] body, ReplicationPacketCommitToken token)
    {
        ConnectionId = connectionId;
        IsLifecycle = lifecycle;
        Body = body ?? System.Array.Empty<byte>();
        Token = token;
    }

    /// <summary>该帧要发给的连接。</summary>
    public NetConnectionId ConnectionId { get; }

    /// <summary>true 走可靠生命周期，否则走不可靠时序快照。</summary>
    public bool IsLifecycle { get; }

    /// <summary>V2 Codec 编码结果，不含 Session 信封。</summary>
    public byte[] Body { get; }

    /// <summary>发送成功后交回 Authority Commit，异常/背压时 Reject。</summary>
    public ReplicationPacketCommitToken Token { get; }
}
