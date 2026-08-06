using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>把 Unity 侧选招服务与角色上下文接入纯 ActionSim 解析契约。</summary>
public sealed class ActionSimResolverBridge : IActionSimResolver
{
    readonly ActionResolverService _resolverService;
    readonly Transform _actorRoot;
    readonly IActionStartContext _startContext;
    readonly Func<IActionSimContent, bool> _canAfford;

    /// <summary>创建单角色解析桥；桥只转换请求和结果，不持有动作状态。</summary>
    public ActionSimResolverBridge(
        ActionResolverService resolverService,
        Transform actorRoot,
        IActionStartContext startContext,
        IActionResourceGate resourceGate = null)
    {
        _resolverService = resolverService;
        _actorRoot = actorRoot;
        _startContext = startContext;
        _canAfford = resourceGate != null
            ? content => resourceGate.CanAfford(content)
            : null;
    }

    /// <summary>枚举当前战斗模式出招图可消费的全部意图。</summary>
    public IEnumerable<GameplayIntentType> EnumerateActiveIntents() =>
        _resolverService != null
            ? _resolverService.EnumerateActiveIntents()
            : Array.Empty<GameplayIntentType>();

    /// <summary>把纯快照转换为 Cancel 解析上下文并返回纯结果。</summary>
    public bool TryResolveNext(
        GameplayIntentType intent,
        CancelWindowType windowType,
        in ActionSimSnapshot snapshot,
        out ActionSimResolveResult result)
    {
        result = default;
        if (_resolverService == null || snapshot.Content is not ActionDefinition currentAction)
            return false;

        var request = new ActionRequest(intent);
        var context = new ActionResolveContext(
            ActionResolveOrigin.CancelWindow,
            currentAction,
            _actorRoot,
            _startContext,
            windowType,
            snapshot.NodeId,
            hasCancelRoute: true,
            canAfford: _canAfford);
        if (!_resolverService.TryResolveNext(in request, in context, out ActionResolveResult resolved)
            || !resolved.IsValid)
        {
            return false;
        }

        result = resolved.ToSimResult();
        return result.IsValid;
    }

    /// <summary>把纯快照转换为 Recovery Entry 解析上下文并返回纯结果。</summary>
    public bool TryResolveRecoveryStart(
        GameplayIntentType intent,
        in ActionSimSnapshot snapshot,
        out ActionSimResolveResult result)
    {
        result = default;
        if (_resolverService == null || snapshot.Content is not ActionDefinition currentAction)
            return false;

        var request = new ActionRequest(intent);
        var context = new ActionResolveContext(
            ActionResolveOrigin.RecoveryEntry,
            currentAction,
            _actorRoot,
            _startContext,
            currentNodeId: snapshot.NodeId,
            canAfford: _canAfford);
        if (!_resolverService.TryResolveStart(in request, in context, out ActionResolveResult resolved)
            || !resolved.IsValid)
        {
            return false;
        }

        result = resolved.ToSimResult();
        return result.IsValid;
    }
}
