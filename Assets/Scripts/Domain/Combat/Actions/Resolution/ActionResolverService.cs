using System.Collections.Generic;

/// <summary>动作解析服务：按当前战斗模式出招表把输入请求路由到对应 Resolver；起手与 Cancel 共用一条路由。</summary>
public sealed class ActionResolverService
{
    readonly CombatModeService _combatMode;

    /// <summary>创建解析服务；出招表由 CombatModeService.ActiveActionSet 提供。</summary>
    public ActionResolverService(CombatModeService combatMode)
    {
        _combatMode = combatMode;
    }

    /// <summary>Locomotion 起手解析：按 request.InputId 找 Entry 并调用其 Resolver。</summary>
    public bool TryResolveStart(
        in ActionRequest request,
        in ActionResolveContext context,
        out ActionDefinition action)
        => TryResolve(in request, in context, out action);

    /// <summary>Cancel 窗口下一招解析：与起手同路由，差异由 context.Origin / CurrentAction 表达。</summary>
    public bool TryResolveNext(
        in ActionRequest request,
        in ActionResolveContext context,
        out ActionDefinition action)
        => TryResolve(in request, in context, out action);

    /// <summary>枚举当前出招表中的全部有效离散输入 id，供输入缓冲清理使用。</summary>
    public IEnumerable<string> EnumerateActiveInputIds()
    {
        PlayerActionSet actionSet = _combatMode?.ActiveActionSet;
        if (actionSet == null)
            yield break;

        foreach (ActionEntry entry in actionSet.Entries)
        {
            if (entry.IsValid)
                yield return entry.InputId;
        }
    }

    /// <summary>按输入 id 找 Entry 的 Resolver 并解析；无出招表 / 无匹配 Entry / 无 Resolver 时失败。</summary>
    bool TryResolve(
        in ActionRequest request,
        in ActionResolveContext context,
        out ActionDefinition action)
    {
        action = null;
        PlayerActionSet actionSet = _combatMode?.ActiveActionSet;
        if (actionSet == null || !request.IsValid)
            return false;

        if (!actionSet.TryGetResolver(request.InputId, out ActionResolver resolver))
            return false;

        return resolver.TryResolve(in request, in context, out action);
    }
}
