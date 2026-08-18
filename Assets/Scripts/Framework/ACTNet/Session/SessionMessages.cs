using System;

/// <summary>客户端建立 Session 时提交的协议与内容版本。</summary>
public readonly struct SessionJoinRequest
{
    /// <summary>创建版本握手请求。</summary>
    public SessionJoinRequest(
        int contentVersion,
        NetworkProtocolVersion protocolVersion,
        ContentFingerprint gameplayFingerprint = default)
    {
        ContentVersion = contentVersion;
        ProtocolVersion = protocolVersion;
        GameplayFingerprint = gameplayFingerprint;
    }

    /// <summary>应用层内容版本。</summary>
    public int ContentVersion { get; }

    /// <summary>网络协议版本。</summary>
    public NetworkProtocolVersion ProtocolVersion { get; }

    /// <summary>Gameplay 内容指纹；Invalid 表示调用方未计算。</summary>
    public ContentFingerprint GameplayFingerprint { get; }
}

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

/// <summary>Session 向应用层交付的已拆信封消息。</summary>
public readonly struct SessionApplicationPacket
{
    /// <summary>创建应用消息。</summary>
    public SessionApplicationPacket(
        NetConnectionId connectionId,
        byte messageType,
        byte[] payload)
    {
        ConnectionId = connectionId;
        MessageType = messageType;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    /// <summary>消息所属连接。</summary>
    public NetConnectionId ConnectionId { get; }

    /// <summary>应用层自定义消息类型。</summary>
    public byte MessageType { get; }

    /// <summary>已移除 Session 信封的正文。</summary>
    public byte[] Payload { get; }
}

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
