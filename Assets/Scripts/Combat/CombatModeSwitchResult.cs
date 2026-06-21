/// <summary>战斗模式切换结果；调用方根据结果决定是否 Stop 当前招式。</summary>
public enum CombatModeSwitchResult
{
    /// <summary>profile 无效或目标 mode 未配置。</summary>
    Failed = 0,

    /// <summary>已切换或目标与当前相同。</summary>
    Applied = 1,

    /// <summary>OnNextLocomotion：招式中挂起，回到 Locomotion 后由 ApplyPendingModeIfReady 应用。</summary>
    PendingUntilLocomotion = 2,

    /// <summary>StopCurrentAction：调用方应先 Stop 再以 isActionPlaying=false 重试。</summary>
    RequiresStopCurrentAction = 3,
}
