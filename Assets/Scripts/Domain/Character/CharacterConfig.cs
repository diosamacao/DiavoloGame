using System;
using UnityEngine;

/// <summary>
/// 角色装配根配置。Locomotion（含 Clip）在 CombatMode；输入/意图为项目全局；
/// 本资产只保留模型、Motor、战斗身体与资源。
/// </summary>
[CreateAssetMenu(fileName = "CharacterConfig", menuName = "ACT/Character/Character Config")]
public class CharacterConfig : ScriptableObject
{
    [Header("Model")]
    [SerializeField] GameObject modelPrefab = null;
    [SerializeField] Vector3 modelLocalPosition = Vector3.zero;
    [SerializeField] Vector3 modelLocalEulerAngles = Vector3.zero;

    [Header("Movement")]
    [SerializeField] CharacterMotorConfig motor = CharacterMotorConfig.Default;

    [Header("Combat")]
    [Tooltip("mode → ActionGraph + LocomotionProfile（内含 AnimationProfile）。")]
    [SerializeField] CombatModeProfile combatProfile = null;
    [SerializeField] CharacterCombatConfig combat = CharacterCombatConfig.Default;

    [Header("Resources")]
    [Tooltip("Energy / Decibel / Dodge；嵌本配置，禁止另开 Profile 双轨。")]
    [SerializeField] CharacterResourceConfig resources = null;

    /// <summary>角色模型 Prefab；运行时会实例化为 PlayerController 子物体。</summary>
    public GameObject ModelPrefab => modelPrefab;

    /// <summary>模型实例本地位置。</summary>
    public Vector3 ModelLocalPosition => modelLocalPosition;

    /// <summary>模型实例本地旋转。</summary>
    public Quaternion ModelLocalRotation => Quaternion.Euler(modelLocalEulerAngles);

    /// <summary>移动和 CharacterController 参数。</summary>
    public CharacterMotorConfig Motor => motor;

    /// <summary>战斗模式与出招图 / Clip 映射。</summary>
    public CombatModeProfile CombatProfile => combatProfile;

    /// <summary>战斗运行时装配参数。</summary>
    public CharacterCombatConfig Combat => combat;

    /// <summary>玩法资源上限与回复；未序列化时用默认骨架值。</summary>
    public CharacterResourceConfig Resources => resources ?? CharacterResourceConfig.Default;

    /// <summary>检查玩家必需配置（含全局 Input）。</summary>
    public bool ValidateForPlayer(UnityEngine.Object context)
    {
        bool valid = ValidateShared(context);
        if (GameInputSettings.Active == null)
        {
            Debug.LogError(
                "CharacterConfig: 全局 InputActionAsset 未就绪（GameInputSettings）。",
                context);
            valid = false;
        }

        return valid;
    }

    /// <summary>检查敌人角色配置；输入走全局，不要求本资产挂 InputActions。</summary>
    public bool ValidateForEnemy(UnityEngine.Object context)
    {
        return ValidateShared(context);
    }

    /// <summary>校验模型、Locomotion 参数、全局意图与 CombatMode。</summary>
    bool ValidateShared(UnityEngine.Object context)
    {
        bool valid = true;
        if (modelPrefab == null)
        {
            Debug.LogError("CharacterConfig: ModelPrefab 未配置。", context);
            valid = false;
        }

        if (GameplayIntentSettings.Active == null)
        {
            Debug.LogError(
                "CharacterConfig: 全局 GameplayIntentProfile 未就绪（GameplayIntentSettings）。",
                context);
            valid = false;
        }

        if (combatProfile == null)
        {
            Debug.LogError("CharacterConfig: CombatProfile 未配置。", context);
            valid = false;
        }
        else if (!combatProfile.Validate(context))
        {
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
    [Tooltip("FollowInput 转向平滑时间（秒，越大越慢）。同时决定朝向追 wish 与水平位移沿朝向拐弯的时长；W→WD 只调这一项。")]
    [SerializeField] float rotationSmoothTime;
    [SerializeField] float gravity;
    [SerializeField] float groundedGravity;
    [SerializeField] float controllerHeight;
    [SerializeField] float controllerRadius;
    [SerializeField] Vector3 controllerCenter;
    [Tooltip("软弹开相对质量；越大越难被推开。与 SoftBodyImmovable 二选一语义。")]
    [SerializeField] int softBodyMass;
    [Tooltip("勾选后软弹开推力全给对方，自身像墙（大体型 Boss 用）。")]
    [SerializeField] bool softBodyImmovable;

    /// <summary>默认第三人称角色移动参数。</summary>
    public static CharacterMotorConfig Default => new()
    {
        walkSpeed = 4f,
        runSpeed = 7f,
        sprintSpeed = 9f,
        runThreshold = 0.6f,
        // 略加大：配合 L-DIR4 倾身窗口；已序列化资产仍用各自 Inspector 值
        rotationSmoothTime = 0.2f,
        gravity = -20f,
        groundedGravity = -2f,
        controllerHeight = 1.7f,
        controllerRadius = 0.28f,
        controllerCenter = new Vector3(0f, 0.85f, 0f),
        softBodyMass = CharacterMotorSim.DefaultSoftBodyMass,
        softBodyImmovable = false,
    };

    /// <summary>走速。</summary>
    public float WalkSpeed => walkSpeed;

    /// <summary>跑速。</summary>
    public float RunSpeed => runSpeed;

    /// <summary>冲刺速度（Run 持续后进入 Sprint）。</summary>
    public float SprintSpeed => sprintSpeed > 0f ? sprintSpeed : runSpeed;

    /// <summary>输入幅度超过该值视为跑（尚未满 Sprint 计时）。</summary>
    public float RunThreshold => runThreshold;

    /// <summary>FollowInput 转向/沿朝向位移共用的平滑时间（秒）。</summary>
    public float RotationSmoothTime => rotationSmoothTime;

    /// <summary>空中重力加速度（m/s²）；量化进 MotorSim，不再经 CC.Move。</summary>
    public float Gravity => gravity;

    /// <summary>着地时保持贴地的纵向速度（m/s）；量化进 MotorSim。</summary>
    public float GroundedGravity => groundedGravity;

    /// <summary>水平碰撞半径（米）；同步给 CharacterController 与 MotorSim。</summary>
    public float ControllerRadius =>
        controllerRadius > 0f ? controllerRadius : Default.controllerRadius;

    /// <summary>软弹开质量；未配置时用默认 100。</summary>
    public int SoftBodyMass =>
        softBodyMass > 0 ? softBodyMass : CharacterMotorSim.DefaultSoftBodyMass;

    /// <summary>为 true 时软弹开中自身不位移，对方承担全部推力。</summary>
    public bool SoftBodyImmovable => softBodyImmovable;

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
    [SerializeField] HurtboxDefinition hurtbox;
    [SerializeField] float maxHealth;
    [Tooltip("唯一 SelectedTarget 的自动选中/显式切换范围（米）。")]
    [SerializeField] float targetAcquireRangeMeters;
    [Tooltip("当前 SelectedTarget 的保持范围（米），须不小于选中范围。")]
    [SerializeField] float targetRetainRangeMeters;
    [Tooltip("上层控制器用于选择受击与死亡表现动作的规则集。")]
    [SerializeField] CharacterReactionSet reactions;

    /// <summary>默认玩家阵营与空挂点名。</summary>
    public static CharacterCombatConfig Default => new()
    {
        teamId = 0,
        attachPointName = string.Empty,
        hurtbox = new HurtboxDefinition(),
        maxHealth = 100f,
        targetAcquireRangeMeters = 12f,
        targetRetainRangeMeters = 13.5f,
        reactions = new CharacterReactionSet(),
    };

    /// <summary>攻击者阵营 id；索敌会排除同阵营目标。</summary>
    public int TeamId => teamId;

    /// <summary>Hitbox/VFX 默认挂点名；为空时使用角色根。</summary>
    public string AttachPointName => attachPointName;

    /// <summary>角色根节点上的默认受击框；旧配置缺失时使用标准人形 Box。</summary>
    public HurtboxDefinition Hurtbox => hurtbox ?? new HurtboxDefinition();

    /// <summary>玩家等未被上层 Definition 覆盖时使用的最大生命值。</summary>
    public float MaxHealth => maxHealth > 0f ? maxHealth : 100f;

    /// <summary>自动选中与 TargetSwitch 可使用的范围；旧资产缺失字段时回退 12 米。</summary>
    public float TargetAcquireRangeMeters =>
        targetAcquireRangeMeters > 0f ? targetAcquireRangeMeters : 12f;

    /// <summary>已选目标保持范围；旧资产或非法值时回退为 Acquire + 1.5 米。</summary>
    public float TargetRetainRangeMeters =>
        targetRetainRangeMeters >= TargetAcquireRangeMeters
            ? targetRetainRangeMeters
            : TargetAcquireRangeMeters + 1.5f;

    /// <summary>供玩家或敌人上层控制器解析受击、死亡表现的规则集。</summary>
    public CharacterReactionSet Reactions => reactions ?? new CharacterReactionSet();
}
