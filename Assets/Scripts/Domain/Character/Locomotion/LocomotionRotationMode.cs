/// <summary>本帧水平旋转策略。</summary>
public enum LocomotionRotationMode
{
    /// <summary>保持当前朝向。</summary>
    Hold = 0,
    /// <summary>平滑转向当前移动输入方向（可用 RotationSmoothTimeOverride）。</summary>
    FollowInput = 1,
    /// <summary>立即对齐进入 Pivot 时锁定的目标方向。</summary>
    PivotTarget = 2,
}
