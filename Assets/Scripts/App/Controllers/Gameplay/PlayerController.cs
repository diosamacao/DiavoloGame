using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>玩家座位：Host 装配权威 Actor；Client 只采样输入并挂预测表现。</summary>
[DefaultExecutionOrder(-50)]
public class PlayerController : AppControllerBase, ILocalPlayer
{
    [Header("References")]
    [SerializeField] CharacterConfig characterConfig = null;

    [Header("Debug")]
    [Tooltip("Play 时在脚底画 wish（黄）与模型朝向（品红）实心箭头。")]
    [SerializeField] bool drawFacingDebugArrows = true;

    CharacterActor actor;
    CharacterHurtboxTarget hurtboxTarget;
    CharacterReactionService reactionService;
    SimulationHost simulationHost;
    SimActorRegistration simulationRegistration;
    CharacterFacingDebugVisualizer _facingDebugVisualizer;
    ILocalInputSampler _inputSampler;
    RemoteCharacterProxy _predictedView;
    bool _clientSeat;

    /// <summary>运行时角色 Actor；供 Debug HUD / Scene Gizmo 只读访问。</summary>
    public CharacterActor Actor => actor;

    /// <summary>装配用角色配置；幽灵预览复用同一套模型与动画。</summary>
    public CharacterConfig CharacterConfig => characterConfig;

    /// <summary>量化输入中枢；客机无 Actor，为 null。</summary>
    public InputManager Input => actor?.Input;

    /// <inheritdoc />
    public bool HasMoveIntent =>
        actor?.Input != null
            ? actor.Input.HasMoveIntent
            : _inputSampler != null && _inputSampler.HasMoveIntent;

    /// <inheritdoc />
    public bool IsPresentingAction =>
        actor != null
            ? actor.CurrentState != CharacterStateType.Locomotion
            : _predictedView != null && _predictedView.IsPresentingAction;

    /// <summary>本机设备采样；客机座位用它上行，权威座位走 Actor。</summary>
    public ILocalInputSampler InputSampler => _inputSampler;

    /// <summary>相机表现使用的本地渲染帧视角输入，不进入锁步输入帧。</summary>
    public Vector2 LookInput => _inputSampler != null
        ? _inputSampler.LookInput
        : actor?.LookInput ?? Vector2.zero;

    /// <summary>本渲染帧是否按下纯表现 CameraLock。</summary>
    public bool CameraLockPressedThisFrame => _inputSampler != null
        ? _inputSampler.CameraLockPressedThisFrame
        : actor != null && actor.CameraLockPressedThisFrame;

    /// <summary>玩家当前生命值；运行时未创建时为 0。</summary>
    public float CurrentHealth => actor != null ? actor.Vitality.CurrentHealth : 0f;

    /// <summary>相机应跟随的插值表现锚点；客机跟预测体，权威跟 Actor。</summary>
    public Transform PresentationRoot =>
        _predictedView?.PresentationRoot != null
            ? _predictedView.PresentationRoot
            : actor?.PresentationRoot != null
                ? actor.PresentationRoot
                : transform;

    /// <summary>权威根，供敌人感知与花名册使用。</summary>
    public Transform Root => transform;

    /// <summary>Listen Host 本地不预测；客机座位为 true。</summary>
    public bool IsLocalPredicted => _clientSeat;

    /// <summary>客机房间把预测表现体挂到本座位，供相机跟随。</summary>
    public void BindPredictedView(RemoteCharacterProxy proxy) => _predictedView = proxy;

    void Awake()
    {
        if (characterConfig == null)
        {
            Debug.LogError("PlayerController: 未绑定 CharacterConfig。", this);
            enabled = false;
            return;
        }

        if (!characterConfig.ValidateForPlayer(this))
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

        CombatWorldController combatWorld = EnsureCombatWorldController();
        if (combatWorld != null && !combatWorld.IsAuthority)
        {
            BuildClientSeat(inputActions);
            return;
        }

        var inputSource = new InputReader(inputActions);
        _inputSampler = inputSource;
        simulationHost = combatWorld.EnsureSimulationHost();

        actor = CharacterActorFactory.Create(
            gameObject,
            transform,
            characterConfig,
            characterConfig.Combat.TeamId,
            inputSource,
            () => SendQuery(new GetActiveTargetsQuery()),
            simulationHost.CombatHits,
            out ActionSim actionSim,
            out CharacterAnimationService animation,
            simulationHost.CollisionWorld);

        reactionService = new CharacterReactionService(
            actor.Vitality,
            actor,
            new CharacterReactionResolver(characterConfig.Combat.Reactions));
        hurtboxTarget = new CharacterHurtboxTarget(
            transform,
            transform,
            characterConfig.Combat.TeamId,
            characterConfig.Combat.Hurtbox,
            actor.Vitality,
            actionSim,
            () => actor?.SimulationId ?? SimActorId.Invalid,
            actor.MotorSim,
            id => simulationHost != null ? simulationHost.LookupNumeric(id) : null);

        GetSystem<CombatActorSystem>()?.Register(transform, actor, animation);
        GetSystem<TargetSystem>()?.Register(hurtboxTarget);
        GetSystem<LocalPlayerService>()?.Register(this, isLocalOwner: true);
        EnsureFacingDebugVisualizer();
    }

    void OnEnable()
    {
        if (_clientSeat)
        {
            _inputSampler?.Enable();
            GetSystem<LocalPlayerService>()?.Register(this, isLocalOwner: true);
            return;
        }

        actor?.Enable();
        if (actor != null && simulationHost != null && !simulationRegistration.IsValid)
        {
            simulationRegistration = simulationHost.RegisterPlayer(actor);
            simulationHost.RegisterNumeric(actor.SimulationId, actor.Numeric);
        }

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
        _facingDebugVisualizer.Bind(actor);
        _facingDebugVisualizer.SetDrawEnabled(drawFacingDebugArrows);
#endif
    }

    void OnDisable()
    {
        GetSystem<LocalPlayerService>()?.Unregister(this);
        if (_clientSeat)
        {
            _inputSampler?.Disable();
            return;
        }

        if (simulationHost != null)
            simulationHost.Unregister(simulationRegistration);
        simulationRegistration = SimActorRegistration.Invalid;
        actor?.Disable();
    }

    void OnDestroy()
    {
        GetSystem<LocalPlayerService>()?.Unregister(this);
        if (_clientSeat)
        {
            _inputSampler?.Disable();
            _predictedView = null;
            return;
        }

        if (simulationHost != null)
            simulationHost.Unregister(simulationRegistration);
        reactionService?.Dispose();
        GetSystem<TargetSystem>()?.Unregister(hurtboxTarget);
        GetSystem<CombatActorSystem>()?.Unregister(transform);
        actor?.Dispose();
        actor = null;
        hurtboxTarget = null;
        reactionService = null;
        simulationHost = null;
        simulationRegistration = SimActorRegistration.Invalid;
    }

    /// <summary>由 CameraManager 暂存 Orbit yaw，下一次采样写入 InputFrame。</summary>
    public void StageMoveReferenceYaw(float yawDegrees)
    {
        if (_inputSampler != null)
            _inputSampler.StageMoveReferenceYaw(yawDegrees);
        else
            actor?.StageMoveReferenceYaw(yawDegrees);
    }

    /// <summary>客机只采样输入，不创建会 Collect 的权威 Actor。</summary>
    void BuildClientSeat(InputActionAsset inputActions)
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
        GetSystem<LocalPlayerService>()?.Register(this, isLocalOwner: true);
    }

    /// <summary>玩家装配前确保场景存在统一战斗世界入口并返回该入口。</summary>
    CombatWorldController EnsureCombatWorldController()
    {
        CombatWorldController world = CombatWorldController.Current;
        if (world == null)
            world = FindObjectOfType<CombatWorldController>();
        if (world != null)
            return world;

        var worldObject = new GameObject("CombatWorldController");
        return worldObject.AddComponent<CombatWorldController>();
    }
}
