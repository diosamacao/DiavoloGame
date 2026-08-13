using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>玩家角色装配与位移入口；Scene 空物体只需挂本组件并指定 CharacterConfig。</summary>
[DefaultExecutionOrder(-50)]
public class PlayerController : AppControllerBase
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

    /// <summary>运行时角色 Actor；供 Debug HUD / Scene Gizmo 只读访问。</summary>
    public CharacterActor Actor => actor;

    /// <summary>玩家量化输入中枢，供调试与玩法查询。</summary>
    public InputManager Input => actor?.Input;

    /// <summary>相机表现使用的本地渲染帧视角输入，不进入锁步输入帧。</summary>
    public Vector2 LookInput => actor?.LookInput ?? Vector2.zero;

    /// <summary>本渲染帧是否按下纯表现 CameraLock。</summary>
    public bool CameraLockPressedThisFrame => actor != null && actor.CameraLockPressedThisFrame;

    /// <summary>玩家当前生命值；运行时未创建时为 0。</summary>
    public float CurrentHealth => actor != null ? actor.Vitality.CurrentHealth : 0f;

    /// <summary>相机应跟随的插值表现锚点；Actor 尚未创建时回退权威根。</summary>
    public Transform PresentationRoot => actor?.PresentationRoot != null
        ? actor.PresentationRoot
        : transform;

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

        var inputSource = new InputReader(inputActions);
        CombatWorldController combatWorld = EnsureCombatWorldController();
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
        EnsureFacingDebugVisualizer();
    }

    void OnEnable()
    {
        actor?.Enable();
        if (actor != null && simulationHost != null && !simulationRegistration.IsValid)
        {
            simulationRegistration = simulationHost.RegisterPlayer(actor);
            simulationHost.RegisterNumeric(actor.SimulationId, actor.Numeric);
        }

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
        _facingDebugVisualizer.Bind(this);
        _facingDebugVisualizer.SetDrawEnabled(drawFacingDebugArrows);
#endif
    }

    void OnDisable()
    {
        if (simulationHost != null)
            simulationHost.Unregister(simulationRegistration);
        simulationRegistration = SimActorRegistration.Invalid;
        actor?.Disable();
    }

    void OnDestroy()
    {
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
    public void StageMoveReferenceYaw(float yawDegrees) =>
        actor?.StageMoveReferenceYaw(yawDegrees);

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
