/// <summary>
/// 动作基础位移权威（Wave 1）。
/// LegacyResolve 仅迁移窗口保留；Wave 2 出口后删除并去掉 useRootMotion 回退。
/// </summary>
public enum ActionBaseMotionMode
{
    /// <summary>未迁移资产：沿用旧 UseRootMotion + 表优先策略。</summary>
    LegacyResolve = 0,

    /// <summary>无动作位移（不含 Animator RM）。</summary>
    None = 1,

    /// <summary>唯一权威为烘焙运动表。</summary>
    BakedMotion = 2,

    /// <summary>唯一权威为 Timeline Movement 窗口。</summary>
    ScriptedTimeline = 3,
}
