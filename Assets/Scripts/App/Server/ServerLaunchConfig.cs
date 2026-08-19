using System;

/// <summary>Dedicated 启动不可变配置；容量与绑定由本结构提供，不再写死 Guest=2。</summary>
public readonly struct ServerLaunchConfig
{
    /// <summary>创建启动配置；调用方须先检查 <see cref="Validate"/>。</summary>
    public ServerLaunchConfig(
        string bindHost,
        int bindPort,
        int contentVersion,
        int maxPlayers,
        int idleTimeoutMs,
        int heartbeatIntervalMs,
        NetworkProtocolVersion protocolVersion,
        NetArchetypeId playerArchetypeId,
        ContentFingerprint gameplayFingerprint = default,
        int emptyLobbyTimeoutMs = 0,
        bool exitOnMatchEnd = false)
    {
        BindHost = bindHost ?? string.Empty;
        BindPort = bindPort;
        ContentVersion = contentVersion;
        MaxPlayers = maxPlayers;
        IdleTimeoutMs = idleTimeoutMs;
        HeartbeatIntervalMs = heartbeatIntervalMs;
        ProtocolVersion = protocolVersion;
        PlayerArchetypeId = playerArchetypeId;
        GameplayFingerprint = gameplayFingerprint;
        EmptyLobbyTimeoutMs = emptyLobbyTimeoutMs;
        ExitOnMatchEnd = exitOnMatchEnd;
    }

    /// <summary>按房间默认协议创建 LAN Dedicated 配置；默认不因空房或对局结束退出进程。</summary>
    public static ServerLaunchConfig CreateDefault(
        int bindPort,
        int contentVersion,
        int maxPlayers = 4,
        ContentFingerprint gameplayFingerprint = default,
        int emptyLobbyTimeoutMs = 0,
        bool exitOnMatchEnd = false) =>
        new(
            "0.0.0.0",
            bindPort,
            contentVersion,
            maxPlayers,
            ReplicationRoomProtocol.IdleTimeoutMs,
            ReplicationRoomProtocol.HeartbeatIntervalMs,
            new NetworkProtocolVersion(ReplicationRoomProtocol.ProtocolVersion),
            playerArchetypeId: default,
            gameplayFingerprint,
            emptyLobbyTimeoutMs,
            exitOnMatchEnd);

    /// <summary>监听主机；Dedicated 通常为 0.0.0.0。</summary>
    public string BindHost { get; }

    /// <summary>监听端口。</summary>
    public int BindPort { get; }

    /// <summary>Join 要求的内容版本。</summary>
    public int ContentVersion { get; }

    /// <summary>远端玩家容量，同时作为 Session MaxRemotePlayers。</summary>
    public int MaxPlayers { get; }

    /// <summary>空闲超时毫秒。</summary>
    public int IdleTimeoutMs { get; }

    /// <summary>心跳间隔毫秒。</summary>
    public int HeartbeatIntervalMs { get; }

    /// <summary>线协议版本。</summary>
    public NetworkProtocolVersion ProtocolVersion { get; }

    /// <summary>Match 为加入玩家选择的默认角色原型；未指定时由后续 Content 填充。</summary>
    public NetArchetypeId PlayerArchetypeId { get; }

    /// <summary>Gameplay 指纹；Valid 时 Join 必须一致。</summary>
    public ContentFingerprint GameplayFingerprint { get; }

    /// <summary>无人加入时的 Lobby 等待上限；0 表示不因空房超时退出。</summary>
    public int EmptyLobbyTimeoutMs { get; }

    /// <summary>对局结束或 Playing 空房后是否请求进程退出；Editor 入口必须为 false。</summary>
    public bool ExitOnMatchEnd { get; }

    /// <summary>只改进程生命周期策略，其它绑定参数保持不变。</summary>
    public ServerLaunchConfig WithLifetimePolicy(int emptyLobbyTimeoutMs, bool exitOnMatchEnd) =>
        new(
            BindHost,
            BindPort,
            ContentVersion,
            MaxPlayers,
            IdleTimeoutMs,
            HeartbeatIntervalMs,
            ProtocolVersion,
            PlayerArchetypeId,
            GameplayFingerprint,
            emptyLobbyTimeoutMs,
            exitOnMatchEnd);

    /// <summary>Join 指纹与场景扫描结果对齐；内容版本被 CLI 覆盖后必须重算再写入。</summary>
    public ServerLaunchConfig WithGameplayFingerprint(ContentFingerprint gameplayFingerprint) =>
        new(
            BindHost,
            BindPort,
            ContentVersion,
            MaxPlayers,
            IdleTimeoutMs,
            HeartbeatIntervalMs,
            ProtocolVersion,
            PlayerArchetypeId,
            gameplayFingerprint,
            EmptyLobbyTimeoutMs,
            ExitOnMatchEnd);

    /// <summary>校验启动参数；失败时写出退出码。</summary>
    public bool Validate(out ServerExitCode exitCode)
    {
        if (string.IsNullOrEmpty(BindHost)
            || BindPort < 0
            || BindPort > 65535
            || ContentVersion < 1
            || MaxPlayers < 1
            || IdleTimeoutMs < 1
            || HeartbeatIntervalMs < 1
            || HeartbeatIntervalMs >= IdleTimeoutMs
            || ProtocolVersion.Value <= 0
            || EmptyLobbyTimeoutMs < 0)
        {
            exitCode = ServerExitCode.ConfigFailed;
            return false;
        }

        exitCode = ServerExitCode.Success;
        return true;
    }

    /// <summary>转换为 Session 配置；远端 Id 从 1 起，不预留房主号。</summary>
    public SessionConfig CreateSessionConfig() =>
        new(
            ProtocolVersion,
            ContentVersion,
            MaxPlayers,
            IdleTimeoutMs,
            HeartbeatIntervalMs,
            firstPlayerId: 1,
            gameplayFingerprint: GameplayFingerprint);

    /// <summary>Transport 绑定端点。</summary>
    public NetEndpoint BindEndpoint => new(BindHost, BindPort, allowEphemeralPort: BindPort == 0);
}
