using System;
using UnityEngine;

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
