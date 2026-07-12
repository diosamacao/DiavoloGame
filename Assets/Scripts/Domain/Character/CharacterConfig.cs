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

    [Header("Input")]
    [SerializeField] InputActionAsset inputActions = null;

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

    /// <summary>玩家输入资产。</summary>
    public InputActionAsset InputActions => inputActions;

    /// <summary>移动和 CharacterController 参数。</summary>
    public CharacterMotorConfig Motor => motor;

    /// <summary>战斗模式与技能表配置。</summary>
    public CombatModeProfile CombatProfile => combatProfile;

    /// <summary>战斗运行时装配参数。</summary>
    public CharacterCombatConfig Combat => combat;

    /// <summary>检查必需配置；失败时输出明确错误，避免运行时热路径反复判空。</summary>
    public bool ValidateForPlayer(UnityEngine.Object context)
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

        if (inputActions == null)
        {
            Debug.LogError("CharacterConfig: InputActions 未配置。", context);
            valid = false;
        }

        if (combatProfile == null)
        {
            Debug.LogError("CharacterConfig: CombatProfile 未配置。", context);
            valid = false;
        }

        return valid;
    }
}

/// <summary>角色移动与碰撞体配置，集中替代 PlayerController 上分散的移动字段。</summary>
[Serializable]
public struct CharacterMotorConfig
{
    [SerializeField] float walkSpeed;
    [SerializeField] float runSpeed;
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

    /// <summary>输入幅度超过该值视为跑。</summary>
    public float RunThreshold => runThreshold;

    /// <summary>移动转向平滑时间。</summary>
    public float RotationSmoothTime => rotationSmoothTime;

    /// <summary>空中重力加速度。</summary>
    public float Gravity => gravity;

    /// <summary>着地时保持贴地的纵向速度。</summary>
    public float GroundedGravity => groundedGravity;

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

    /// <summary>默认玩家阵营与空挂点名。</summary>
    public static CharacterCombatConfig Default => new()
    {
        teamId = 0,
        attachPointName = string.Empty,
        aimOriginName = string.Empty,
    };

    /// <summary>攻击者阵营 id；索敌会排除同阵营目标。</summary>
    public int TeamId => teamId;

    /// <summary>Hitbox/VFX 默认挂点名；为空时使用角色根。</summary>
    public string AttachPointName => attachPointName;

    /// <summary>索敌起点挂点名；为空时使用角色根。</summary>
    public string AimOriginName => aimOriginName;
}
