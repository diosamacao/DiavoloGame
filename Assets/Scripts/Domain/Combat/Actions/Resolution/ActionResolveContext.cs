using UnityEngine;

/// <summary>动作解析上下文：描述"世界/状态侧"信息，Resolver 据此选择最终动作（含图游标字段）。</summary>
public readonly struct ActionResolveContext
{
    /// <summary>构造解析上下文；currentAction / nodeId 在 Locomotion 起手时为空。</summary>
    public ActionResolveContext(
        ActionResolveOrigin origin,
        ActionDefinition currentAction,
        Transform actorRoot,
        IActionStartContext startContext,
        CancelType cancelType = CancelType.Combo,
        string currentNodeId = null,
        string cancelSlotId = null)
    {
        Origin = origin;
        CurrentAction = currentAction;
        ActorRoot = actorRoot;
        StartContext = startContext;
        CancelType = cancelType;
        CurrentNodeId = currentNodeId;
        CancelSlotId = cancelSlotId;
    }

    /// <summary>解析来源（Locomotion 起手、显式 Cancel、Recovery 软重开或高优打断）。</summary>
    public ActionResolveOrigin Origin { get; }

    /// <summary>当前正在播放的招式；Locomotion 起手时为 null。</summary>
    public ActionDefinition CurrentAction { get; }

    /// <summary>角色根节点，方向 Resolver 据此读取平面朝向。</summary>
    public Transform ActorRoot { get; }

    /// <summary>招式起手副作用上下文：读取闪避意图方向、修正朝向。</summary>
    public IActionStartContext StartContext { get; }

    /// <summary>触发本次解析的 Cancel 窗类型；非 CancelWindow 来源时无意义。</summary>
    public CancelType CancelType { get; }

    /// <summary>当前连招图节点 id；不在图内时为 null。</summary>
    public string CurrentNodeId { get; }

    /// <summary>本次 Cancel 命中的槽 id；非 Cancel 解析时为 null。</summary>
    public string CancelSlotId { get; }
}

/// <summary>动作解析来源：区分起手、显式 Cancel、Recovery Entry 与高优硬打断。</summary>
public enum ActionResolveOrigin
{
    /// <summary>从 Locomotion 起手解析（当前无激活招式）。</summary>
    LocomotionStart = 0,

    /// <summary>从当前招式的 CancelWindow 内解析下一招。</summary>
    CancelWindow = 1,

    /// <summary>Action 态高优硬打断：按 Graph Entry 解析候选招。</summary>
    PriorityInterrupt = 2,

    /// <summary>当前招式处于 Recovery Phase：按 Graph Entry 软切换，不要求显式图边。</summary>
    RecoveryEntry = 3,
}
