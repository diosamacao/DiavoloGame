using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>角色装配根配置；PlayerController 只引用该资产即可生成角色运行时。</summary>
[CreateAssetMenu(fileName = "CharacterConfig", menuName = "ACT/Character/Character Config")]
public class CharacterConfig : ScriptableObject
{
    [Header("Model")]
    [SerializeField] GameObject modelPrefab = null;
    [SerializeField] Vector3 modelLocalPosition = Vector3.zero;
    [SerializeField] Vector3 modelLocalEulerAngles = Vector3.zero;

    [Header("Animation")]
    [SerializeField] CharacterAnimationProfile defaultLocomotionProfile = null;

    [Header("Locomotion")]
    [Tooltip("相位阈值、落脚标记与脚步音；为空时运行时使用默认阈值实例。")]
    [SerializeField] CharacterLocomotionProfile locomotionProfile = null;

    [Header("Input")]
    [SerializeField] InputActionAsset inputActions = null;
    [Tooltip("物理输入到 GameplayIntentType 的唯一映射配置。")]
    [SerializeField] GameplayIntentProfile gameplayIntentProfile = null;

    [Header("Movement")]
    [SerializeField] CharacterMotorConfig motor = CharacterMotorConfig.Default;

    [Header("Combat")]
    [SerializeField] CombatModeProfile combatProfile = null;
    [SerializeField] CharacterCombatConfig combat = CharacterCombatConfig.Default;

    /// <summary>角色模型 Prefab；运行时会实例化为 PlayerController 子物体。</summary>
    public GameObject ModelPrefab => modelPrefab;

    /// <summary>模型实例本地位置。</summary>
    public Vector3 ModelLocalPosition => modelLocalPosition;

    /// <summary>模型实例本地旋转。</summary>
    public Quaternion ModelLocalRotation => Quaternion.Euler(modelLocalEulerAngles);

    /// <summary>默认 Locomotion 动画映射。</summary>
    public CharacterAnimationProfile DefaultLocomotionProfile => defaultLocomotionProfile;

    /// <summary>Locomotion 相位与落脚配置；可为空。</summary>
    public CharacterLocomotionProfile LocomotionProfile => locomotionProfile;

    /// <summary>玩家输入资产。</summary>
    public InputActionAsset InputActions => inputActions;

    /// <summary>设备输入到玩法语义的映射。</summary>
    public GameplayIntentProfile GameplayIntentProfile => gameplayIntentProfile;

    /// <summary>移动和 CharacterController 参数。</summary>
    public CharacterMotorConfig Motor => motor;

    /// <summary>战斗模式与技能表配置。</summary>
    public CombatModeProfile CombatProfile => combatProfile;

    /// <summary>战斗运行时装配参数。</summary>
    public CharacterCombatConfig Combat => combat;

    /// <summary>检查必需配置；失败时输出明确错误，避免运行时热路径反复判空。</summary>
    public bool ValidateForPlayer(UnityEngine.Object context)
    {
        bool valid = ValidateShared(context);
        if (inputActions == null)
        {
            Debug.LogError("CharacterConfig: InputActions 未配置。", context);
            valid = false;
        }

        return valid;
    }

    /// <summary>检查敌人角色配置；AI 复用语义意图配置，但不要求玩家 InputActionAsset。</summary>
    public bool ValidateForEnemy(UnityEngine.Object context)
    {
        return ValidateShared(context);
    }

    /// <summary>校验玩家与敌人共用的模型、动画、意图和战斗配置。</summary>
    bool ValidateShared(UnityEngine.Object context)
    {
        bool valid = true;
        if (modelPrefab == null)
        {
            Debug.LogError("CharacterConfig: ModelPrefab 未配置。", context);
            valid = false;
        }

        if (defaultLocomotionProfile == null)
        {
            Debug.LogError("CharacterConfig: DefaultLocomotionProfile 未配置。", context);
            valid = false;
        }
        else if (!defaultLocomotionProfile.ValidateClips(context))
        {
            valid = false;
        }

        if (gameplayIntentProfile == null)
        {
            Debug.LogError("CharacterConfig: GameplayIntentProfile 未配置。", context);
            valid = false;
        }

        if (combatProfile == null)
        {
            Debug.LogError("CharacterConfig: CombatProfile 未配置。", context);
            valid = false;
        }

        if (!Combat.Reactions.Validate(context))
            valid = false;

        return valid;
    }
}

/// <summary>角色移动与碰撞体配置，集中替代 PlayerController 上分散的移动字段。</summary>
[Serializable]
public struct CharacterMotorConfig
{
    [SerializeField] float walkSpeed;
    [SerializeField] float runSpeed;
    [SerializeField] float sprintSpeed;
    [SerializeField] float runThreshold;
    [SerializeField] float rotationSmoothTime;
    [SerializeField] float gravity;
    [SerializeField] float groundedGravity;
    [SerializeField] float controllerHeight;
    [SerializeField] float controllerRadius;
    [SerializeField] Vector3 controllerCenter;

    /// <summary>默认第三人称角色移动参数。</summary>
    public static CharacterMotorConfig Default => new()
    {
        walkSpeed = 4f,
        runSpeed = 7f,
        sprintSpeed = 9f,
        runThreshold = 0.6f,
        rotationSmoothTime = 0.12f,
        gravity = -20f,
        groundedGravity = -2f,
        controllerHeight = 1.7f,
        controllerRadius = 0.28f,
        controllerCenter = new Vector3(0f, 0.85f, 0f),
    };

    /// <summary>走速。</summary>
    public float WalkSpeed => walkSpeed;

    /// <summary>跑速。</summary>
    public float RunSpeed => runSpeed;

    /// <summary>冲刺速度（Run 持续后进入 Sprint）。</summary>
    public float SprintSpeed => sprintSpeed > 0f ? sprintSpeed : runSpeed;

    /// <summary>输入幅度超过该值视为跑（尚未满 Sprint 计时）。</summary>
    public float RunThreshold => runThreshold;

    /// <summary>移动转向平滑时间。</summary>
    public float RotationSmoothTime => rotationSmoothTime;

    /// <summary>空中重力加速度。</summary>
    public float Gravity => gravity;

    /// <summary>着地时保持贴地的纵向速度。</summary>
    public float GroundedGravity => groundedGravity;

    /// <summary>水平碰撞半径（米）；同步给 CharacterController 与 MotorSim。</summary>
    public float ControllerRadius =>
        controllerRadius > 0f ? controllerRadius : Default.controllerRadius;

    /// <summary>把配置应用到 CharacterController；只在初始化阶段调用。</summary>
    public void ApplyTo(CharacterController controller)
    {
        if (controller == null)
            return;

        controller.height = controllerHeight > 0f ? controllerHeight : Default.controllerHeight;
        controller.radius = controllerRadius > 0f ? controllerRadius : Default.controllerRadius;
        controller.center = controllerCenter;
    }
}

/// <summary>角色战斗侧装配参数，避免索敌和判定挂点散落在多个组件字段。</summary>
[Serializable]
public struct CharacterCombatConfig
{
    [SerializeField] int teamId;
    [SerializeField] string attachPointName;
    [SerializeField] string aimOriginName;
    [SerializeField] HurtboxDefinition hurtbox;
    [SerializeField] float maxHealth;
    [Tooltip("上层控制器用于选择受击与死亡表现动作的规则集。")]
    [SerializeField] CharacterReactionSet reactions;

    /// <summary>默认玩家阵营与空挂点名。</summary>
    public static CharacterCombatConfig Default => new()
    {
        teamId = 0,
        attachPointName = string.Empty,
        aimOriginName = string.Empty,
        hurtbox = new HurtboxDefinition(),
        maxHealth = 100f,
        reactions = new CharacterReactionSet(),
    };

    /// <summary>攻击者阵营 id；索敌会排除同阵营目标。</summary>
    public int TeamId => teamId;

    /// <summary>Hitbox/VFX 默认挂点名；为空时使用角色根。</summary>
    public string AttachPointName => attachPointName;

    /// <summary>索敌起点挂点名；为空时使用角色根。</summary>
    public string AimOriginName => aimOriginName;

    /// <summary>角色根节点上的默认受击框；旧配置缺失时使用标准人形 Box。</summary>
    public HurtboxDefinition Hurtbox => hurtbox ?? new HurtboxDefinition();

    /// <summary>玩家等未被上层 Definition 覆盖时使用的最大生命值。</summary>
    public float MaxHealth => maxHealth > 0f ? maxHealth : 100f;

    /// <summary>供玩家或敌人上层控制器解析受击、死亡表现的规则集。</summary>
    public CharacterReactionSet Reactions => reactions ?? new CharacterReactionSet();
}
