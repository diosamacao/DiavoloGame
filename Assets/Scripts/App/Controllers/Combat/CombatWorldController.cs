using UnityEngine;

/// <summary>场景级战斗世界入口：Listen Host / Client 房间与固定帧宿主。</summary>
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

    /// <summary>当前场景战斗世界；系统查询只把它作为生命周期锚点，不作为业务单例入口。</summary>
    public static CombatWorldController Current { get; private set; }

    /// <summary>当前战斗世界唯一固定帧宿主。</summary>
    public SimulationHost SimulationHost { get; private set; }

    /// <summary>本机房间角色；Awake 后已合并 EditorPrefs 覆盖。</summary>
    public ReplicationRole Role { get; private set; }

    /// <summary>是否由本机推进权威 SimulationWorld。</summary>
    public bool IsAuthority => Role == ReplicationRole.ListenHost;

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

    /// <summary>按角色挂 Host 或 Client；单机也是 Listen Host，不走旧旁路。</summary>
    void EnsureRoomController()
    {
        if (IsAuthority)
        {
            ReplicationRoomHost host = GetComponent<ReplicationRoomHost>();
            if (host == null)
                host = gameObject.AddComponent<ReplicationRoomHost>();
            host.Configure(this);
            return;
        }

        ReplicationRoomClient client = GetComponent<ReplicationRoomClient>();
        if (client == null)
            client = gameObject.AddComponent<ReplicationRoomClient>();
        client.Configure(this);
    }
}
