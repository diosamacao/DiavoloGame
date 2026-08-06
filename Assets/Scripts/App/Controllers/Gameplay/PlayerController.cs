using UnityEngine;

/// <summary>玩家角色装配与位移入口；Scene 空物体只需挂本组件并指定 CharacterConfig。</summary>
[DefaultExecutionOrder(-50)]
public class PlayerController : AppControllerBase
{
    [Header("References")]
    [SerializeField] CharacterConfig characterConfig = null;
    [SerializeField] Transform cameraTransform;

    CharacterActor actor;
    CharacterHealth health;
    CharacterHurtboxTarget hurtboxTarget;
    CharacterReactionService reactionService;
    SimulationHost simulationHost;
    SimActorRegistration simulationRegistration;

    /// <summary>运行时角色 Actor；供 Debug HUD / Scene Gizmo 只读访问。</summary>
    public CharacterActor Actor => actor;

    /// <summary>玩家量化输入中枢，供调试与玩法查询。</summary>
    public InputManager Input => actor?.Input;

    /// <summary>相机表现使用的本地渲染帧视角输入，不进入锁步输入帧。</summary>
    public Vector2 LookInput => actor?.LookInput ?? Vector2.zero;

    /// <summary>玩家当前生命值；运行时未创建时为 0。</summary>
    public float CurrentHealth => health != null ? health.CurrentHealth : 0f;

    /// <summary>相机应跟随的插值表现锚点；Actor 尚未创建时回退权威根。</summary>
    public Transform PresentationRoot => actor?.PresentationRoot != null
        ? actor.PresentationRoot
        : transform;

    void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

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

        var inputSource = new InputReader(characterConfig.InputActions);
        CombatWorldController combatWorld = EnsureCombatWorldController();
        simulationHost = combatWorld.EnsureSimulationHost();

        actor = CharacterActorFactory.Create(
            gameObject,
            transform,
            characterConfig,
            characterConfig.Combat.TeamId,
            inputSource,
            cameraTransform,
            () => SendQuery(new GetActiveTargetsQuery()),
            simulationHost.CombatHits,
            out ActionSim actionSim,
            out CharacterAnimationService animation,
            simulationHost.CollisionWorld);

        health = new CharacterHealth(characterConfig.Combat.MaxHealth);
        actor.AttachHealth(health);
        reactionService = new CharacterReactionService(
            health,
            actor,
            new CharacterReactionResolver(characterConfig.Combat.Reactions));
        hurtboxTarget = new CharacterHurtboxTarget(
            transform,
            transform,
            characterConfig.Combat.TeamId,
            characterConfig.Combat.Hurtbox,
            health,
            () => actor?.SimulationId ?? SimActorId.Invalid,
            actor.MotorSim);

        GetSystem<CombatActorSystem>()?.Register(transform, actor, animation);
        GetSystem<TargetSystem>()?.Register(hurtboxTarget);
    }

    void OnEnable()
    {
        actor?.Enable();
        if (actor != null && simulationHost != null && !simulationRegistration.IsValid)
            simulationRegistration = simulationHost.RegisterPlayer(actor);
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
        health = null;
        hurtboxTarget = null;
        reactionService = null;
        simulationHost = null;
        simulationRegistration = SimActorRegistration.Invalid;
    }

    /// <summary>相机就绪或切换后刷新运行时使用的相机 Transform。</summary>
    public void SetCameraTransform(Transform targetCamera)
    {
        cameraTransform = targetCamera;
        actor?.SetCameraTransform(targetCamera);
    }

    /// <summary>由 CameraManager 每帧写入 Orbit 水平基，避免挤墙扭曲走位。</summary>
    public void SetCameraPlanarBasis(Vector3 planarForward, Vector3 planarRight)
    {
        actor?.SetCameraPlanarBasis(planarForward, planarRight);
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
