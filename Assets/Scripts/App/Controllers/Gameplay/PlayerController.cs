using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>玩家座位：Listen/Client 装配 Autonomous Actor（不进 World）；Dedicated 禁用本机座位。</summary>
[DefaultExecutionOrder(-50)]
public class PlayerController : AppControllerBase, ILocalPlayer
{
    [Header("References")]
    [SerializeField] PartyLoadout partyLoadout = null;

    [Header("Debug")]
    [Tooltip("Play 时在脚底画 wish（黄）与模型朝向（品红）实心箭头。")]
    [SerializeField] bool drawFacingDebugArrows = true;

    PlayerPartyRuntime _partyRuntime;
    CharacterFacingDebugVisualizer _facingDebugVisualizer;
    ILocalInputSampler _inputSampler;
    bool _clientSeat;

    /// <summary>当前 Active 角色 Actor；供预测、相机、HUD 与 Scene Gizmo 只读访问。</summary>
    public CharacterActor Actor => _partyRuntime?.ActiveActor;

    /// <summary>本机阵容预测与权威同步的唯一入口。</summary>
    public PlayerPartyRuntime Party => _partyRuntime;

    /// <summary>本座位声明的 1～3 人出战阵容。</summary>
    public PartyLoadout PartyLoadout => partyLoadout;

    /// <summary>
    /// 当前 Active 角色配置；初始化协调器前回退到开局槽。
    /// </summary>
    public CharacterConfig CharacterConfig =>
        ActiveCharacterDefinition?.CharacterConfig;

    /// <summary>当前 Active 角色定义；初始化前返回 Loadout 开局角色。</summary>
    public CharacterDefinition ActiveCharacterDefinition
    {
        get
        {
            if (partyLoadout == null)
                return null;
            if (_partyRuntime != null)
                return _partyRuntime.ActiveDefinition;
            int startingSlot = partyLoadout.StartingSlot;
            return startingSlot >= 0 && startingSlot < partyLoadout.Members.Count
                ? partyLoadout.Members[startingSlot]
                : null;
        }
    }

    /// <summary>量化输入中枢；Autonomous 座位都有 Actor。</summary>
    public InputManager Input => Actor?.Input;

    /// <inheritdoc />
    public bool HasMoveIntent =>
        Actor?.Input != null
            ? Actor.Input.HasMoveIntent
            : _inputSampler != null && _inputSampler.HasMoveIntent;

    /// <inheritdoc />
    public bool IsPresentingAction =>
        Actor != null && Actor.CurrentState != CharacterStateType.Locomotion;

    /// <summary>本机设备采样；上行命令读它，不写权威 World。</summary>
    public ILocalInputSampler InputSampler => _inputSampler;

    /// <summary>相机表现使用的本地渲染帧视角输入，不进入锁步输入帧。</summary>
    public Vector2 LookInput => _inputSampler != null
        ? _inputSampler.LookInput
        : Actor?.LookInput ?? Vector2.zero;

    /// <summary>相机相对移动轴；跟朝向只认本机设备采样，不读权威 InputFrame。</summary>
    public Vector2 MoveInput => _inputSampler != null
        ? _inputSampler.MoveInput
        : Actor?.Input != null ? Actor.Input.MoveIntent : Vector2.zero;

    /// <summary>本渲染帧是否按下纯表现 CameraLock。</summary>
    public bool CameraLockPressedThisFrame => _inputSampler != null
        ? _inputSampler.CameraLockPressedThisFrame
        : Actor != null && Actor.CameraLockPressedThisFrame;

    /// <summary>玩家当前生命值；运行时未创建时为 0。</summary>
    public float CurrentHealth => Actor != null ? Actor.Vitality.CurrentHealth : 0f;

    /// <summary>相机应跟随的插值表现锚点；两端都跟 Actor。</summary>
    public Transform PresentationRoot =>
        Actor?.PresentationRoot != null ? Actor.PresentationRoot : transform;

    /// <summary>表现根；敌人感知应走权威 RemotePlayerSeat，不读本机预测根。</summary>
    public Transform Root =>
        _partyRuntime?.ActiveRoot != null ? _partyRuntime.ActiveRoot : transform;

    /// <summary>Listen / Client 本机座位恒为 true；Dedicated 不装配本机玩家。</summary>
    public bool IsLocalPredicted => _clientSeat;

    void Awake()
    {
        if (partyLoadout == null)
        {
            Debug.LogError("PlayerController: 未绑定 PartyLoadout。", this);
            enabled = false;
            return;
        }

        if (!partyLoadout.Validate(this))
        {
            enabled = false;
            return;
        }

        if (!ValidatePartyForPlayer())
        {
            enabled = false;
            return;
        }

        InputActionAsset inputActions = GameInputSettings.Active;
        if (inputActions == null)
        {
            Debug.LogError("PlayerController: 全局 InputActionAsset 未就绪（GameInputSettings）。", this);
            enabled = false;
            return;
        }

        CombatWorldController combatWorld = ResolveCombatWorldController();
        if (combatWorld == null)
        {
            enabled = false;
            return;
        }
        // Dedicated 进程禁止装配本机玩家座位；权威角色只由远端 Join 创建。
        if (combatWorld.Role == ReplicationRole.DedicatedServer)
        {
            enabled = false;
            return;
        }

        BuildClientSeat(inputActions, combatWorld);
    }

    /// <summary>装配前校验阵容中每个非空角色，避免后台槽直到切出时才暴露缺失配置。</summary>
    bool ValidatePartyForPlayer()
    {
        bool valid = true;
        IReadOnlyList<CharacterDefinition> members = partyLoadout.Members;
        for (int i = 0; i < members.Count; i++)
        {
            CharacterConfig config = members[i]?.CharacterConfig;
            if (config != null && !config.ValidateForPlayer(this))
                valid = false;
        }

        return valid;
    }

    void OnEnable()
    {
        if (!_clientSeat)
            return;

        _inputSampler?.Enable();
        GetSystem<LocalPlayerService>()?.Register(this, isLocalOwner: true);
        EnsureFacingDebugVisualizer();
    }

    void LateUpdate()
    {
        if (_facingDebugVisualizer != null)
            _facingDebugVisualizer.SetDrawEnabled(drawFacingDebugArrows);
    }

    /// <summary>开发构建下挂载脚底朝向调试箭头（wish / 模型）。</summary>
    void EnsureFacingDebugVisualizer()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_facingDebugVisualizer == null)
            _facingDebugVisualizer = GetComponent<CharacterFacingDebugVisualizer>();
        if (_facingDebugVisualizer == null)
            _facingDebugVisualizer = gameObject.AddComponent<CharacterFacingDebugVisualizer>();
        _facingDebugVisualizer.Bind(Actor);
        _facingDebugVisualizer.SetDrawEnabled(drawFacingDebugArrows);
#endif
    }

    void OnDisable()
    {
        GetSystem<LocalPlayerService>()?.Unregister(this);
        if (!_clientSeat)
            return;

        _inputSampler?.Disable();
    }

    void OnDestroy()
    {
        GetSystem<LocalPlayerService>()?.Unregister(this);
        if (!_clientSeat)
            return;

        _inputSampler?.Disable();
        if (_partyRuntime != null)
        {
            _partyRuntime.ActiveActorChanged -= OnActiveActorChanged;
            _partyRuntime.Dispose();
            _partyRuntime = null;
        }
    }

    /// <summary>由 CameraManager 暂存 Orbit yaw，下一次采样写入 InputFrame。</summary>
    public void StageMoveReferenceYaw(float yawDegrees)
    {
        if (_inputSampler != null)
            _inputSampler.StageMoveReferenceYaw(yawDegrees);
        else
            Actor?.StageMoveReferenceYaw(yawDegrees);
    }

    /// <summary>
    /// 本机按 Loadout 装配最多三个 Autonomous Actor；均不进 World、不挂 Hurtbox。
    /// 开局槽可见，其余槽保留独立 Numeric/Action 状态但隐藏。
    /// </summary>
    void BuildClientSeat(
        InputActionAsset inputActions,
        CombatWorldController combatWorld)
    {
        _clientSeat = true;
        var reader = new InputReader(inputActions);
        GameplayIntentProfile intentProfile = GameplayIntentSettings.Active;
        if (intentProfile == null)
        {
            Debug.LogError("PlayerController: 全局 GameplayIntentProfile 未就绪。", this);
            enabled = false;
            return;
        }

        reader.ConfigureDiscreteInputs(intentProfile.CollectInputReferences());
        _inputSampler = reader;
        SimulationHost simulationHost = combatWorld.EnsureSimulationHost();
        _partyRuntime = new PlayerPartyRuntime(
            partyLoadout,
            transform,
            reader,
            () => SendQuery(new GetActiveTargetsQuery()),
            simulationHost);
        _partyRuntime.ActiveActorChanged += OnActiveActorChanged;

        GetSystem<LocalPlayerService>()?.Register(this, isLocalOwner: true);
        EnsureFacingDebugVisualizer();
    }

    /// <summary>Active Actor 改变后只刷新 Controller 持有的本地调试表现绑定。</summary>
    void OnActiveActorChanged(CharacterActor actor) =>
        _facingDebugVisualizer?.Bind(actor);

    /// <summary>解析由更早执行的场景 Composition Root；缺失时明确失败，禁止自行扫描或创建第二个 World。</summary>
    CombatWorldController ResolveCombatWorldController()
    {
        CombatWorldController world = CombatWorldController.Current;
        if (world != null)
            return world;
        Debug.LogError(
            "PlayerController: 场景缺少先行初始化的 CombatWorldController Composition Root。",
            this);
        return null;
    }
}
