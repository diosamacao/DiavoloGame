using System.Collections.Generic;

/// <summary>
/// 动作解析服务：当前模式的 ActionGraph 负责多 Entry×Intent 起手、高优打断与 Cancel 解析。
/// </summary>
public sealed class ActionResolverService
{
    readonly CombatModeService _combatMode;

    /// <summary>创建解析服务；出招图由 CombatModeService.ActiveGraph 提供。</summary>
    public ActionResolverService(CombatModeService combatMode)
    {
        _combatMode = combatMode;
    }

    /// <summary>当前模式绑定的 ActionGraph。</summary>
    public ActionGraph ActiveGraph => _combatMode?.ActiveGraph;

    /// <summary>
    /// Graph Entry 解析：服务于 Locomotion 起手、Recovery 软重开与 Action 态 PriorityInterrupt。
    /// </summary>
    public bool TryResolveStart(
        in ActionRequest request,
        in ActionResolveContext context,
        out ActionResolveResult result)
    {
        result = default;
        ActionGraph graph = ActiveGraph;
        if (graph == null || !request.IsValid)
            return false;

        if (context.Origin != ActionResolveOrigin.LocomotionStart
            && context.Origin != ActionResolveOrigin.PriorityInterrupt
            && context.Origin != ActionResolveOrigin.RecoveryEntry)
            return false;

        return graph.TryResolveStart(in request, in context, out result);
    }

    /// <summary>按 Entry NodeId 起手解析（敌人 CombatRequest）；Origin 须为 LocomotionStart。</summary>
    public bool TryResolveEntry(
        string entryNodeId,
        in ActionResolveContext context,
        out ActionResolveResult result)
    {
        result = default;
        ActionGraph graph = ActiveGraph;
        if (graph == null || string.IsNullOrEmpty(entryNodeId))
            return false;
        if (context.Origin != ActionResolveOrigin.LocomotionStart)
            return false;

        return graph.TryResolveEntry(entryNodeId, in context, out result);
    }

    /// <summary>Cancel 下一招：使用 CurrentNodeId + CancelWindowType 在 ActiveGraph 上解析。</summary>
    public bool TryResolveNext(
        in ActionRequest request,
        in ActionResolveContext context,
        out ActionResolveResult result)
    {
        result = default;
        ActionGraph graph = ActiveGraph;
        if (graph == null || !request.IsValid)
            return false;

        // Cancel 必须在图游标内；无节点则无法派生。
        if (context.Origin != ActionResolveOrigin.CancelWindow)
            return false;

        return graph.TryResolveCancel(in request, in context, out result);
    }

    /// <summary>枚举当前出招图中的全部节点意图，供缓冲清理与 Cancel 候选收集。</summary>
    public IEnumerable<GameplayIntentType> EnumerateActiveIntents()
    {
        ActionGraph graph = ActiveGraph;
        if (graph == null)
            yield break;

        var set = new HashSet<GameplayIntentType>();
        graph.CollectIntents(set);
        foreach (GameplayIntentType intent in set)
            yield return intent;
    }
}
