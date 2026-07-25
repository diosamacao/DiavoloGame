using System;
using UnityEngine;

/// <summary>
/// 图级共享路由：用一条规则表达多个来源节点共用的普通或 Perfect Cancel 去向。
/// 显式边始终优先；SourceTrigger=None 表示不限制来源 Action.Trigger。
/// </summary>
[Serializable]
public class ActionGraphSharedRoute
{
    [Tooltip("限制来源 Action.Trigger；None 表示任意来源。")]
    [SerializeField] GameplayIntentType sourceTrigger = GameplayIntentType.None;
    [Tooltip("匹配普通或 Perfect Cancel 通道。")]
    [SerializeField] CancelWindowType routeKind = CancelWindowType.Normal;
    [Tooltip("匹配已缓冲的玩法意图。")]
    [SerializeField] GameplayIntentType intent = GameplayIntentType.None;
    [Tooltip("共享路由目标节点。")]
    [SerializeField] string toNodeId = string.Empty;

    /// <summary>来源 Trigger 过滤；None 表示任意。</summary>
    public GameplayIntentType SourceTrigger => sourceTrigger;

    /// <summary>匹配的普通或 Perfect Cancel 通道。</summary>
    public CancelWindowType RouteKind => routeKind;

    /// <summary>匹配的玩法意图。</summary>
    public GameplayIntentType Intent => intent;

    /// <summary>目标逻辑节点 Id。</summary>
    public string ToNodeId => toNodeId;
}
