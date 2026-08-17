using System;

/// <summary>控制 Session 容量、版本和保活窗口的不可变配置。</summary>
public readonly struct SessionConfig
{
    /// <summary>创建经过范围验证的 Session 配置。</summary>
    public SessionConfig(
        NetworkProtocolVersion protocolVersion,
        int contentVersion,
        int maxRemotePlayers,
        int idleTimeoutMs,
        int heartbeatIntervalMs,
        int firstPlayerId = 2)
    {
        if (protocolVersion.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(protocolVersion));
        if (maxRemotePlayers < 1)
            throw new ArgumentOutOfRangeException(nameof(maxRemotePlayers));
        if (idleTimeoutMs < 1)
            throw new ArgumentOutOfRangeException(nameof(idleTimeoutMs));
        if (heartbeatIntervalMs < 1 || heartbeatIntervalMs >= idleTimeoutMs)
            throw new ArgumentOutOfRangeException(nameof(heartbeatIntervalMs));
        if (firstPlayerId < 1)
            throw new ArgumentOutOfRangeException(nameof(firstPlayerId));

        ProtocolVersion = protocolVersion;
        ContentVersion = contentVersion;
        MaxRemotePlayers = maxRemotePlayers;
        IdleTimeoutMs = idleTimeoutMs;
        HeartbeatIntervalMs = heartbeatIntervalMs;
        FirstPlayerId = firstPlayerId;
    }

    /// <summary>握手要求的线协议版本。</summary>
    public NetworkProtocolVersion ProtocolVersion { get; }

    /// <summary>握手要求的应用内容版本。</summary>
    public int ContentVersion { get; }

    /// <summary>允许建立的远端玩家数量。</summary>
    public int MaxRemotePlayers { get; }

    /// <summary>任一方向无消息后的超时毫秒数。</summary>
    public int IdleTimeoutMs { get; }

    /// <summary>客户端自动发送心跳的间隔毫秒数。</summary>
    public int HeartbeatIntervalMs { get; }

    /// <summary>玩家 Id 分配起点；Listen Host 默认从 2 开始。</summary>
    public int FirstPlayerId { get; }
}
