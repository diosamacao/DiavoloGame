using UnityEngine;

/// <summary>Locomotion 内层状态下发给 CharacterMotor 的单帧执行命令。</summary>
public readonly struct LocomotionMotorCommand
{
    public LocomotionMotorCommand(
        bool applyHorizontalMove,
        LocomotionRotationMode rotationMode,
        LocomotionGait gait,
        float? rotationSmoothTimeOverride = null)
    {
        ApplyHorizontalMove = applyHorizontalMove;
        RotationMode = rotationMode;
        Gait = gait;
        RotationSmoothTimeOverride = rotationSmoothTimeOverride;
    }

    /// <summary>是否按当前输入做水平位移（首版无加减速曲线）。</summary>
    public bool ApplyHorizontalMove { get; }

    public LocomotionRotationMode RotationMode { get; }

    /// <summary>用于选取 walk/run/sprint 速度。</summary>
    public LocomotionGait Gait { get; }

    /// <summary>覆盖 Motor 默认转向平滑时间；null 表示用 CharacterMotorConfig.rotationSmoothTime。</summary>
    public float? RotationSmoothTimeOverride { get; }
}
