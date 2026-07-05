using UnityEngine;

/// <summary>动作解析来源：区分 Locomotion 起手与 Action Cancel 窗口，供方向类 Resolver 复刻差异行为。</summary>
public enum ActionResolveOrigin
{
    /// <summary>从 Locomotion 起手解析（当前无激活招式）。</summary>
    LocomotionStart = 0,

    /// <summary>从当前招式的 CancelWindow 内解析下一招。</summary>
    CancelWindow = 1,
}

/// <summary>动作解析上下文：描述"世界/状态侧"信息，Resolver 据此选择最终 ActionDefinition。</summary>
public readonly struct ActionResolveContext
{
    /// <summary>构造解析上下文；currentAction 在 Locomotion 起手时为 null。</summary>
    public ActionResolveContext(
        ActionResolveOrigin origin,
        ActionDefinition currentAction,
        Transform actorRoot,
        IActionStartContext startContext)
    {
        Origin = origin;
        CurrentAction = currentAction;
        ActorRoot = actorRoot;
        StartContext = startContext;
    }

    /// <summary>解析来源。</summary>
    public ActionResolveOrigin Origin { get; }

    /// <summary>当前正在播放的招式；Locomotion 起手时为 null，连段进位时为当前招。</summary>
    public ActionDefinition CurrentAction { get; }

    /// <summary>角色根节点，方向 Resolver 据此读取平面朝向。</summary>
    public Transform ActorRoot { get; }

    /// <summary>招式起手副作用上下文：读取闪避意图方向、修正朝向。</summary>
    public IActionStartContext StartContext { get; }
}
