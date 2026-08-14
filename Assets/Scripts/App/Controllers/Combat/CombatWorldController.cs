using UnityEngine;

/// <summary>场景级战斗世界入口：固定帧宿主、反馈，以及 NS2 同机幽灵预览（可选）。</summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class CombatWorldController : AppControllerBase
{
    [Tooltip("可选：静态障碍烘焙资产。未绑定则使用空场地（地面 Y=0、无硬挡）。")]
    [SerializeField] StaticCollisionBake staticCollisionBake = null;

    [Header("NS2 Ghost Preview")]
    [Tooltip("Editor 默认开启：同机第二视图跟本机玩家 Snapshot。不进花名册、不跑命中。")]
    [SerializeField] bool previewRemoteGhost =
#if UNITY_EDITOR
        true;
#else
        false;
#endif
    [SerializeField] Vector3 remoteGhostWorldOffset = new Vector3(2f, 0f, 0f);
    [SerializeField] int remoteGhostLatencyMs = 100;

    [Header("NS3 Predicted Preview")]
    [Tooltip("Editor 默认开启：左侧预测视图立即跟输入，并对延迟权威 Tick 纠偏。不替换 Host 本地玩家。")]
    [SerializeField] bool previewPredictedClient =
#if UNITY_EDITOR
        true;
#else
        false;
#endif
    [SerializeField] Vector3 predictedClientWorldOffset = new Vector3(-2f, 0f, 0f);
    [SerializeField] int predictedClientLatencyMs = 100;

    /// <summary>当前场景战斗世界；系统查询只把它作为生命周期锚点，不作为业务单例入口。</summary>
    public static CombatWorldController Current { get; private set; }

    /// <summary>当前战斗世界唯一固定帧宿主。</summary>
    public SimulationHost SimulationHost { get; private set; }

    void Awake()
    {
        if (Current != null && Current != this)
        {
            Debug.LogWarning("CombatWorldController: 场景中存在多个实例，后创建的实例将被禁用。", this);
            enabled = false;
            return;
        }

        Current = this;
        EnsureSimulationHost();
        ApplyStaticCollisionBake();
        EnsureFeedbackController();
    }

    void Start()
    {
        if (previewRemoteGhost)
            EnsureRemoteGhostPreview();
        if (previewPredictedClient)
            EnsurePredictedClientPreview();
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

    /// <summary>运行时挂上同机幽灵预览；不改 Prefab，现有场景用脚本默认值即可。</summary>
    void EnsureRemoteGhostPreview()
    {
        RemoteGhostViewController ghost = GetComponent<RemoteGhostViewController>();
        if (ghost == null)
            ghost = gameObject.AddComponent<RemoteGhostViewController>();

        ILocalPlayer local = SendQuery(new GetLocalPlayerQuery());
        CharacterConfig config = local is PlayerController player ? player.CharacterConfig : null;
        ghost.Configure(
            EnsureSimulationHost(),
            config,
            remoteGhostWorldOffset,
            remoteGhostLatencyMs);
    }

    /// <summary>运行时挂上同机预测预览；Listen Host 本地玩家仍走权威，不改 IsLocalPredicted。</summary>
    void EnsurePredictedClientPreview()
    {
        PredictedClientPreviewController preview = GetComponent<PredictedClientPreviewController>();
        if (preview == null)
            preview = gameObject.AddComponent<PredictedClientPreviewController>();

        ILocalPlayer local = SendQuery(new GetLocalPlayerQuery());
        CharacterConfig config = local is PlayerController player ? player.CharacterConfig : null;
        preview.Configure(
            EnsureSimulationHost(),
            config,
            predictedClientWorldOffset,
            predictedClientLatencyMs);
    }
}
