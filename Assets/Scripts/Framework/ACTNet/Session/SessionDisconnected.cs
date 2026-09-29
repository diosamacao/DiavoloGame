using System;

/// <summary>Session 断开后供 Gameplay 清理实体的通知。</summary>
public readonly struct SessionDisconnected
{
    /// <summary>创建断开通知。</summary>
    public SessionDisconnected(
        NetConnectionId connectionId,
        NetPlayerId playerId,
        DisconnectReason reason)
    {
        ConnectionId = connectionId;
        PlayerId = playerId;
        Reason = reason;
    }

    /// <summary>已断开的连接。</summary>
    public NetConnectionId ConnectionId { get; }

    /// <summary>连接曾分配的玩家；未完成 Join 时可能无效。</summary>
    public NetPlayerId PlayerId { get; }

    /// <summary>通用断开原因。</summary>
    public DisconnectReason Reason { get; }
}
