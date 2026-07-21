/// <summary>本帧水平旋转策略。</summary>
public enum LocomotionRotationMode
{
    /// <summary>保持当前朝向。</summary>
    Hold = 0,
    /// <summary>平滑转向当前移动输入方向（可用 RotationSmoothTimeOverride）。</summary>
    FollowInput = 1,
    /// <summary>转向显式 Pivot 目标；提供 RotationSmoothTimeOverride 时使用平滑旋转。</summary>
    PivotTarget = 2,
}
