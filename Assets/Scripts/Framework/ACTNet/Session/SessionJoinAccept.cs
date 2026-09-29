using System;

/// <summary>服务端完成玩家与实体分配后的 Session 建立结果。</summary>
public readonly struct SessionJoinAccept
{
    /// <summary>创建已验证的 Join 成功消息。</summary>
    public SessionJoinAccept(
        NetPlayerId playerId,
        NetEntityId entityId,
        NetEntityId authorityEntityId,
        int contentVersion,
        NetTick authorityTick)
    {
        if (!playerId.IsValid)
            throw new ArgumentException("JoinAccept 必须包含有效 PlayerId。", nameof(playerId));
        if (!entityId.IsValid)
            throw new ArgumentException("JoinAccept 必须包含有效 EntityId。", nameof(entityId));
        if (!authorityTick.IsValid)
            throw new ArgumentException("JoinAccept 必须包含有效权威 Tick。", nameof(authorityTick));

        PlayerId = playerId;
        EntityId = entityId;
        AuthorityEntityId = authorityEntityId;
        ContentVersion = contentVersion;
        AuthorityTick = authorityTick;
    }

    /// <summary>Session 分配的玩家身份。</summary>
    public NetPlayerId PlayerId { get; }

    /// <summary>该客户端拥有的应用层实体。</summary>
    public NetEntityId EntityId { get; }

    /// <summary>Listen 本机权威实体；Dedicated 无房主时为 Invalid，客户端不得依赖此字段入房。</summary>
    public NetEntityId AuthorityEntityId { get; }

    /// <summary>服务端确认的内容版本。</summary>
    public int ContentVersion { get; }

    /// <summary>握手完成时的权威逻辑 Tick。</summary>
    public NetTick AuthorityTick { get; }
}
