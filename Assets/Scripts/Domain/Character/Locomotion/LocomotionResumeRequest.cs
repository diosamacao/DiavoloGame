/// <summary>Action→Locomotion 边界的一次性恢复参数，不进入动作意图缓冲。</summary>
public readonly struct LocomotionResumeRequest
{
    /// <summary>创建恢复请求；RequireMoveIntent 可防止无输入时强制进入步态。</summary>
    public LocomotionResumeRequest(
        LocomotionGait initialGait,
        bool skipStart,
        bool requireMoveIntent)
    {
        InitialGait = initialGait;
        SkipStart = skipStart;
        RequireMoveIntent = requireMoveIntent;
        IsValid = true;
    }

    /// <summary>恢复后直接采用的稳态步态。</summary>
    public LocomotionGait InitialGait { get; }
    /// <summary>是否跳过 Start 相位。</summary>
    public bool SkipStart { get; }
    /// <summary>消费时是否必须仍存在移动输入。</summary>
    public bool RequireMoveIntent { get; }
    /// <summary>默认结构为无请求；显式构造后才有效。</summary>
    public bool IsValid { get; }

    /// <summary>Dodge 离开后持方向直接恢复 Sprint。</summary>
    public static LocomotionResumeRequest SprintAfterDodge =>
        new(LocomotionGait.Sprint, skipStart: true, requireMoveIntent: true);
}
