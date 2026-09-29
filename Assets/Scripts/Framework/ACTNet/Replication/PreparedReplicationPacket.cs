using System;

/// <summary>一个已编码但尚未提交的 V2 包。</summary>
public readonly struct PreparedReplicationPacket
{
    /// <summary>绑定类型、正文与提交令牌。</summary>
    public PreparedReplicationPacket(bool reliableLifecycle, byte[] body, ReplicationPacketCommitToken token)
    {
        ReliableLifecycle = reliableLifecycle;
        Body = body ?? throw new ArgumentNullException(nameof(body));
        Token = token;
    }
    /// <summary>true 表示走可靠有序生命周期通道。</summary>
    public bool ReliableLifecycle { get; }
    /// <summary>不含 Session/Mux 外壳的正文。</summary>
    public byte[] Body { get; }
    /// <summary>发送成功后提交，失败或背压时拒绝。</summary>
    public ReplicationPacketCommitToken Token { get; }
}
