using System;

/// <summary>已通过版本校验、等待 Gameplay 分配实体的玩家请求。</summary>
public readonly struct SessionPlayerRequest
{
    /// <summary>创建待 Gameplay 接纳的玩家请求。</summary>
    public SessionPlayerRequest(NetConnectionId connectionId, NetPlayerId playerId)
    {
        ConnectionId = connectionId;
        PlayerId = playerId;
    }

    /// <summary>对应 Transport 连接。</summary>
    public NetConnectionId ConnectionId { get; }

    /// <summary>Session 预留的玩家 Id。</summary>
    public NetPlayerId PlayerId { get; }
}
