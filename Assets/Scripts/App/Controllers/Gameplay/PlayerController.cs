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

    /// <summary>玩家输入中枢，供 CameraManager 读取视角输入。</summary>
    public InputManager Input => actor?.Input;

    /// <summary>玩家当前生命值；运行时未创建时为 0。</summary>
    public float CurrentHealth => health != null ? health.CurrentHealth : 0f;

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
        EnsureCombatWorldController();

        actor = CharacterActorFactory.Create(
            gameObject,
            transform,
            characterConfig,
            characterConfig.Combat.TeamId,
            inputSource,
            cameraTransform,
            () => SendQuery(new GetActiveTargetsQuery()),
            ApplyDetectedHit,
            out ActionExecutor actionExecutor,
            out CharacterAnimationService animation);

        health = new CharacterHealth(characterConfig.Combat.MaxHealth);
        hurtboxTarget = new CharacterHurtboxTarget(
            transform,
            transform,
            characterConfig.Combat.TeamId,
            characterConfig.Combat.Hurtbox,
            health);
        health.HitReceived += OnHitReceived;
        health.Died += OnDied;

        GetSystem<CombatActorSystem>()?.Register(transform, actor, actionExecutor, animation);
        GetSystem<TargetSystem>()?.Register(hurtboxTarget);
    }

    void OnEnable()
    {
        actor?.Enable();
    }

    void OnDisable()
    {
        actor?.Disable();
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.HitReceived -= OnHitReceived;
            health.Died -= OnDied;
        }

        GetSystem<TargetSystem>()?.Unregister(hurtboxTarget);
        GetSystem<CombatActorSystem>()?.Unregister(transform);
        actor?.Dispose();
        actor = null;
        health = null;
        hurtboxTarget = null;
    }

    void Update()
    {
        actor?.Tick(Time.deltaTime);
    }

    /// <summary>相机就绪或切换后刷新运行时使用的相机 Transform。</summary>
    public void SetCameraTransform(Transform targetCamera)
    {
        cameraTransform = targetCamera;
        actor?.SetCameraTransform(targetCamera);
    }

    /// <summary>把纯 Domain 命中检测结果转交给架构 Command 处理跨系统结算。</summary>
    void ApplyDetectedHit(
        ActionHitContext context,
        IHurtboxTarget target,
        IActionHitReceiver hitReceiver,
        Transform targetTransform)
    {
        SendCommand(new ApplyHitCommand(context, target, hitReceiver, targetTransform));
    }

    /// <summary>玩家收到非致命命中时进入正式 Hit 状态，反应不依赖实际扣血量。</summary>
    void OnHitReceived(ActionHitContext context)
    {
        actor?.EnterHit(
            characterConfig.Combat.HitStunSeconds,
            characterConfig.Combat.HitStunAction);
    }

    /// <summary>玩家生命值归零时进入 Death 终态。</summary>
    void OnDied(ActionHitContext context, float damage)
    {
        actor?.EnterDeath(characterConfig.Combat.DeathAction);
    }

    /// <summary>玩家装配前确保场景存在统一战斗世界入口。</summary>
    void EnsureCombatWorldController()
    {
        if (CombatWorldController.Current != null || FindObjectOfType<CombatWorldController>() != null)
            return;

        var world = new GameObject("CombatWorldController");
        world.AddComponent<CombatWorldController>();
    }
}
