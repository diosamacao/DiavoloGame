using System.Collections.Generic;

/// <summary>
/// 动作解析服务：当前模式的 ActionGraph 负责起手 / 高优打断（多 Entry × Trigger）与 Cancel 边解析。
/// 输入身份来自 ActionDefinition.Trigger。
/// </summary>
public sealed class ActionResolverService
{
    readonly CombatModeService _combatMode;

    /// <summary>创建解析服务；出招表由 CombatModeService.ActiveActionSet 提供。</summary>
    public ActionResolverService(CombatModeService combatMode)
    {
        _combatMode = combatMode;
    }

    /// <summary>当前模式绑定的 ActionGraph。</summary>
    public ActionGraph ActiveGraph => _combatMode?.ActiveActionSet?.ActionGraph;

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

    /// <summary>Cancel 下一招：使用上下文中的图游标（CurrentNodeId + CancelSlotId）在 ActiveGraph 上解析。</summary>
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

    /// <summary>枚举当前出招图表中的全部 Trigger 意图，供缓冲清理与无槽边时的候选。</summary>
    public IEnumerable<GameplayIntentType> EnumerateActiveIntents()
    {
        PlayerActionSet actionSet = _combatMode?.ActiveActionSet;
        if (actionSet == null)
            yield break;

        foreach (GameplayIntentType intent in actionSet.EnumerateTriggerIntents())
            yield return intent;
    }
}
