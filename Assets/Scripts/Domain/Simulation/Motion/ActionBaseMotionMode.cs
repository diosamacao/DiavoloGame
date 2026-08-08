/// <summary>
/// 动作基础位移权威（Wave 2 完成态）。
/// 枚举值从 1 起：0 曾为已删除的 LegacyResolve，运行时按 None 处理并应审计报错。
/// </summary>
public enum ActionBaseMotionMode
{
    /// <summary>无动作位移（不含 Animator RM）。</summary>
    None = 1,

    /// <summary>唯一权威为烘焙运动表。</summary>
    BakedMotion = 2,

    /// <summary>唯一权威为 Timeline Movement 窗口。</summary>
    ScriptedTimeline = 3,
}
