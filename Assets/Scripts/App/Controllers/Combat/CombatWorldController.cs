using System;
using System.IO;
using UnityEngine;

/// <summary>场景级战斗世界入口：Listen Host / Client 挂 Room；Dedicated 只移交 Bootstrap。</summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class CombatWorldController : AppControllerBase
{
    [Tooltip("可选：静态障碍烘焙资产。未绑定则使用空场地（地面 Y=0、无硬挡）。")]
    [SerializeField] StaticCollisionBake staticCollisionBake = null;

    [Header("NS5 Room")]
    [Tooltip("单机默认 Listen Host。ParrelSync 克隆自动当 Client；也可用 ACTGame/Room 菜单。")]
    [SerializeField] ReplicationRole role = ReplicationRole.ListenHost;
    [SerializeField] int listenPort = ReplicationRoomProtocol.DefaultPort;
    [SerializeField] string joinHost = "127.0.0.1";
    [SerializeField] int contentVersion = 1;

    ContentFingerprint _gameplayFingerprint;

    /// <summary>当前场景战斗世界；系统查询只把它作为生命周期锚点，不作为业务单例入口。</summary>
    public static CombatWorldController Current { get; private set; }

    /// <summary>当前战斗世界唯一固定帧宿主。</summary>
    public SimulationHost SimulationHost { get; private set; }

    /// <summary>本机房间角色；Awake 后已合并 EditorPrefs 覆盖。</summary>
    public ReplicationRole Role { get; private set; }

    /// <summary>是否由本机推进权威 SimulationWorld。</summary>
    public bool IsAuthority =>
        Role == ReplicationRole.ListenHost || Role == ReplicationRole.DedicatedServer;

    /// <summary>Session 远端容量；Listen 仍为 1 客，Dedicated 为 N。</summary>
    public int MaxRemotePlayers => Role == ReplicationRole.DedicatedServer ? 4 : 1;

    /// <summary>关卡内容版本；入房双方必须一致。</summary>
    public int ContentVersion => contentVersion;

    /// <summary>Host 监听 / Client 连接端口。</summary>
    public int ListenPort => listenPort;

    /// <summary>客机连接的权威地址。</summary>
    public string JoinHost => joinHost;

    /// <summary>房间 HUD 一行；由 Host/Client 刷新。</summary>
    public ReplicationRoomHudInfo RoomHud { get; set; }

    void Awake()
    {
        if (Current != null && Current != this)
        {
            Debug.LogWarning("CombatWorldController: 场景中存在多个实例，后创建的实例将被禁用。", this);
            enabled = false;
            return;
        }

        Current = this;
        ResolveRoleFromEditorPrefs();
        EnsureSimulationHost();
        ApplyStaticCollisionBake();
        ActContentRegistry roomContent = CreateRoomContent(out _gameplayFingerprint);
        if (Role == ReplicationRole.DedicatedServer)
        {
            EnsureDedicatedBootstrap(roomContent);
            return;
        }

        EnsureFeedbackController();
        EnsureRoomController();
    }

    void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    /// <summary>确保场景有统一反馈系统，避免多个角色各自挂卡肉控制器。</summary>
    void EnsureFeedbackController()
    {
        if (GetComponent<FeedbackController>() == null)
            gameObject.AddComponent<FeedbackController>();
    }

    /// <summary>确保固定帧宿主与本战斗世界同生命周期，并返回唯一实例。</summary>
    public SimulationHost EnsureSimulationHost()
    {
        if (SimulationHost == null)
            SimulationHost = GetComponent<SimulationHost>();
        if (SimulationHost == null)
            SimulationHost = gameObject.AddComponent<SimulationHost>();

        return SimulationHost;
    }

    /// <summary>把 Inspector 绑定的烘焙资产装入 Host；无资产则空场地。</summary>
    public void ApplyStaticCollisionBake()
    {
        SimulationHost host = EnsureSimulationHost();
        ISimCollisionWorld world = staticCollisionBake != null
            ? staticCollisionBake.CreateWorld()
            : OpenFieldSimCollisionWorld.Instance;
        host.SetCollisionWorld(world);
    }

    /// <summary>ParrelSync 克隆优先当 Client；否则读 EditorPrefs / 场景默认。</summary>
    void ResolveRoleFromEditorPrefs()
    {
        Role = role;
#if UNITY_EDITOR
        string resolvedHost = joinHost;
        int resolvedPort = listenPort;
        ReplicationRole resolvedRole = Role;
        ReplicationRoomLaunchSettings.ApplyEditorOverride(
            ref resolvedRole,
            ref resolvedHost,
            ref resolvedPort);
        Role = resolvedRole;
        joinHost = resolvedHost;
        listenPort = resolvedPort;
#endif
        string launchSource =
#if UNITY_EDITOR
            ReplicationRoomLaunchSettings.IsParrelSyncClone() ? "ParrelSyncClone" : "EditorPrefsOrScene";
#else
            "Scene";
#endif
        Debug.Log(
            $"CombatWorldController: 房间角色={Role} port={listenPort} host={joinHost} content={contentVersion} source={launchSource}。",
            this);
    }

    /// <summary>扫描场景玩法配置并计算 Gameplay 指纹；Listen 与 Dedicated 使用同一算法。</summary>
    ActContentRegistry CreateRoomContent(out ContentFingerprint fingerprint)
    {
        var content = new ActContentRegistry();
        ActServerContentProbe.PrefillFromScene(content);
        string bakeId = staticCollisionBake != null ? staticCollisionBake.name : string.Empty;
        fingerprint = ServerContentManifest.FromRegistry(content, contentVersion, bakeId).Fingerprint;
        return content;
    }

    /// <summary>Dedicated 只移交给 Bootstrap；启动覆盖 CLI &gt; Env &gt; File &gt; Inspector。Editor 强制不退出进程。</summary>
    void EnsureDedicatedBootstrap(ActContentRegistry content)
    {
        SimulationHost host = EnsureSimulationHost();
        var authority = new DedicatedAuthorityWorld(host, GetArchitecture(), content);
        ServerLaunchConfig defaults = ServerLaunchConfig.CreateDefault(
            listenPort,
            contentVersion,
            MaxRemotePlayers,
            _gameplayFingerprint,
            emptyLobbyTimeoutMs: 0,
            exitOnMatchEnd: !Application.isEditor);

        if (!ServerLaunchConfigResolver.TryResolve(
                defaults,
                Environment.GetEnvironmentVariable,
                Environment.GetCommandLineArgs(),
                TryReadLaunchConfigFile,
                out ServerLaunchConfig config,
                out ServerExitCode resolveExit))
        {
            Debug.LogError($"CombatWorldController: Dedicated 配置解析失败 exit={resolveExit}。", this);
            config = ServerLaunchConfigResolver.CreateInvalidSentinel();
        }

#if UNITY_EDITOR
        // Editor Play 必须能回到 Lobby 再入房；玩家构建才按 ExitOnMatchEnd 停进程。
        config = config.WithLifetimePolicy(emptyLobbyTimeoutMs: 0, exitOnMatchEnd: false);
#endif
        if (config.ContentVersion != contentVersion)
        {
            contentVersion = config.ContentVersion;
            _gameplayFingerprint = ServerContentManifest.FromRegistry(
                content,
                contentVersion,
                staticCollisionBake != null ? staticCollisionBake.name : string.Empty).Fingerprint;
            config = config.WithGameplayFingerprint(_gameplayFingerprint);
        }

        listenPort = config.BindPort;
        Debug.Log(
            $"CombatWorldController: Dedicated launch bind={config.BindHost} port={config.BindPort} "
            + $"max={config.MaxPlayers} content={config.ContentVersion} "
            + $"exitOnMatchEnd={config.ExitOnMatchEnd} emptyLobbyMs={config.EmptyLobbyTimeoutMs}。",
            this);

        DedicatedServerBootstrap bootstrap = GetComponent<DedicatedServerBootstrap>();
        if (bootstrap == null)
            bootstrap = gameObject.AddComponent<DedicatedServerBootstrap>();
        bootstrap.Configure(config, authority);
    }

    /// <summary>只读配置文件正文；缺失或读失败返回 null，由解析器记 ConfigFailed。不把正文打进日志。</summary>
    static string TryReadLaunchConfigFile(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            return File.ReadAllText(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>按角色挂 Host 或 Client；单机也是 Listen Host，不走旧旁路。</summary>
    void EnsureRoomController()
    {
        SessionConfig sessionConfig = CreateSessionConfig();
        if (Role == ReplicationRole.ListenHost)
        {
            ReplicationRoomHost host = GetComponent<ReplicationRoomHost>();
            if (host == null)
                host = gameObject.AddComponent<ReplicationRoomHost>();
            host.Configure(this, TryCreateServerSession(sessionConfig));
            return;
        }

        ReplicationRoomClient client = GetComponent<ReplicationRoomClient>();
        if (client == null)
            client = gameObject.AddComponent<ReplicationRoomClient>();
        client.Configure(this, TryCreateClientSession(sessionConfig));
    }

    /// <summary>把场景配置转换为纯 C# Session 参数；远端容量不再由 Room 常量控制。</summary>
    SessionConfig CreateSessionConfig() => new(
        new NetworkProtocolVersion(ReplicationRoomProtocol.ProtocolVersion),
        contentVersion,
        maxRemotePlayers: MaxRemotePlayers,
        idleTimeoutMs: ReplicationRoomProtocol.IdleTimeoutMs,
        heartbeatIntervalMs: ReplicationRoomProtocol.HeartbeatIntervalMs,
        gameplayFingerprint: _gameplayFingerprint);

    /// <summary>Composition Root 创建具体 UDP 服务端并处理绑定失败。</summary>
    ServerSession TryCreateServerSession(SessionConfig config)
    {
        var transport = new UdpTransport();
        try
        {
            var session = new ServerSession(
                transport,
                config,
                new NetEndpoint("0.0.0.0", listenPort, allowEphemeralPort: true));
            Debug.Log($"CombatWorldController: 监听 UDP {session.LocalEndpoint}。", this);
            return session;
        }
        catch (Exception ex)
        {
            transport.Dispose();
            Debug.LogError(
                $"CombatWorldController: 绑定端口 {listenPort} 失败，房间不可加入。{ex.Message}",
                this);
            return null;
        }
    }

    /// <summary>Composition Root 创建具体 UDP 客户端并立即发起 Session Join。</summary>
    ClientSession TryCreateClientSession(SessionConfig config)
    {
        var transport = new UdpTransport();
        ClientSession session = null;
        try
        {
            session = new ClientSession(transport, config);
            session.Start(
                new NetEndpoint(joinHost, listenPort),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Debug.Log($"CombatWorldController: 已请求加入 {joinHost}:{listenPort}。", this);
            return session;
        }
        catch (Exception ex)
        {
            if (session != null)
                session.Dispose();
            else
                transport.Dispose();
            Debug.LogError(
                $"CombatWorldController: 连接 {joinHost}:{listenPort} 失败。{ex.Message}",
                this);
            return null;
        }
    }
}
