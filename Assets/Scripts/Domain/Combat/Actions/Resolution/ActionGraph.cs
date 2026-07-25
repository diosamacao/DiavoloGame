using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 连招图资产：节点引用 ActionDefinition，边从 Cancel / PerfectCancel 通道派生到目标节点。
/// 支持多个 Locomotion 起手入口（按目标招 Trigger 匹配，可同时含攻击/闪避等）。
/// </summary>
[CreateAssetMenu(fileName = "ActionGraph", menuName = "ACT/Combat/Action Graph")]
public class ActionGraph : ScriptableObject
{
    [SerializeField] ActionGraphNode[] nodes = Array.Empty<ActionGraphNode>();
    [SerializeField] ActionGraphEdge[] edges = Array.Empty<ActionGraphEdge>();
    [Tooltip("共享路由在当前节点没有匹配显式边时生效，用一条规则替代同通道、同意图的大量重复连线。")]
    [SerializeField] ActionGraphSharedRoute[] sharedRoutes = Array.Empty<ActionGraphSharedRoute>();
    [Tooltip("Graph Editor 顺序组；保存时将相邻子节点生成为普通 Cancel 边。")]
    [SerializeField] ActionGraphNodeGroup[] nodeGroups = Array.Empty<ActionGraphNodeGroup>();

    /// <summary>图节点列表。</summary>
    public IReadOnlyList<ActionGraphNode> Nodes => nodes ?? Array.Empty<ActionGraphNode>();

    /// <summary>图边列表。</summary>
    public IReadOnlyList<ActionGraphEdge> Edges => edges ?? Array.Empty<ActionGraphEdge>();

    /// <summary>图级共享路由；仅作显式边未命中时的回退。</summary>
    public IReadOnlyList<ActionGraphSharedRoute> SharedRoutes =>
        sharedRoutes ?? Array.Empty<ActionGraphSharedRoute>();

    /// <summary>有序节点组；运行时仍执行生成后的具体节点边。</summary>
    public IReadOnlyList<ActionGraphNodeGroup> NodeGroups =>
        nodeGroups ?? Array.Empty<ActionGraphNodeGroup>();

    /// <summary>按 nodeId 查找节点；未找到返回 false。</summary>
    public bool TryGetNode(string nodeId, out ActionGraphNode node)
    {
        node = null;
        if (string.IsNullOrEmpty(nodeId) || nodes == null)
            return false;

        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] != null && nodes[i].NodeId == nodeId)
            {
                node = nodes[i];
                return node.Action != null;
            }
        }

        return false;
    }

    /// <summary>按 ActionDefinition 引用查找节点（方向闪避变体落点用）。</summary>
    public bool TryFindNodeByAction(ActionDefinition action, out ActionGraphNode node)
    {
        node = null;
        if (action == null || nodes == null)
            return false;

        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] != null && nodes[i].Action == action)
            {
                node = nodes[i];
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Locomotion 起手：在标记为 Entry 的节点中，按 Action.Trigger 匹配 request；
    /// 若节点配置了 VariantResolver（如 Directional），则解析实际播放变体并保持逻辑节点不变。
    /// </summary>
    public bool TryResolveStart(
        in ActionRequest request,
        in ActionResolveContext context,
        out ActionResolveResult result)
    {
        result = default;
        if (nodes == null || !request.IsValid)
            return false;

        for (int i = 0; i < nodes.Length; i++)
        {
            ActionGraphNode node = nodes[i];
            if (node == null || !node.IsEntry || node.Action == null)
                continue;

            GameplayIntentType trigger = node.Action.Trigger;
            if (trigger == GameplayIntentType.None || trigger != request.Intent)
                continue;

            return FinalizeNodeResolve(node, in request, in context, out result);
        }

        return false;
    }

    /// <summary>Cancel：按 (CurrentNodeId, CancelWindowType) 出边，目标 Trigger 匹配 request。</summary>
    public bool TryResolveCancel(
        in ActionRequest request,
        in ActionResolveContext context,
        out ActionResolveResult result)
    {
        result = default;
        if (string.IsNullOrEmpty(context.CurrentNodeId) || !context.HasCancelRoute)
            return false;

        var edgeBuffer = new List<ActionGraphEdge>(8);
        CollectEdges(context.CurrentNodeId, context.CancelWindowType, edgeBuffer);

        for (int i = 0; i < edgeBuffer.Count; i++)
        {
            ActionGraphEdge edge = edgeBuffer[i];
            if (!TryGetNode(edge.ToNodeId, out ActionGraphNode toNode))
                continue;

            GameplayIntentType trigger = toNode.Action.Trigger;
            if (trigger == GameplayIntentType.None || trigger != request.Intent)
                continue;

            return FinalizeNodeResolve(toNode, in request, in context, out result);
        }

        return TryResolveSharedRoute(in request, in context, out result);
    }

    /// <summary>
    /// 显式边未命中时按「来源 Trigger（None=任意）+ Cancel 路由 + 输入意图」匹配共享路由。
    /// 共享路由用于回根、统一反击、统一蓄力入口等横切关系，不替代独特连招拓扑。
    /// </summary>
    bool TryResolveSharedRoute(
        in ActionRequest request,
        in ActionResolveContext context,
        out ActionResolveResult result)
    {
        result = default;
        if (sharedRoutes == null || context.CurrentAction == null)
            return false;

        GameplayIntentType sourceTrigger = context.CurrentAction.Trigger;
        for (int i = 0; i < sharedRoutes.Length; i++)
        {
            ActionGraphSharedRoute route = sharedRoutes[i];
            if (route == null
                || route.RouteKind != context.CancelWindowType
                || route.Intent != request.Intent
                || (route.SourceTrigger != GameplayIntentType.None
                    && route.SourceTrigger != sourceTrigger))
            {
                continue;
            }

            if (!TryGetNode(route.ToNodeId, out ActionGraphNode toNode))
                continue;

            return FinalizeNodeResolve(toNode, in request, in context, out result);
        }

        return false;
    }

    /// <summary>枚举从指定节点、指定 Cancel 路由出发的边。</summary>
    public void CollectEdges(
        string fromNodeId,
        CancelWindowType routeKind,
        List<ActionGraphEdge> results)
    {
        results.Clear();
        if (edges == null || string.IsNullOrEmpty(fromNodeId))
            return;

        for (int i = 0; i < edges.Length; i++)
        {
            ActionGraphEdge edge = edges[i];
            if (edge == null)
                continue;

            if (edge.FromNodeId == fromNodeId && edge.RouteKind == routeKind)
                results.Add(edge);
        }
    }

    /// <summary>收集某节点某槽出边目标招的玩法意图（去重）。</summary>
    public void CollectCancelCandidateIntents(
        string fromNodeId,
        CancelWindowType routeKind,
        HashSet<GameplayIntentType> results)
    {
        results.Clear();
        if (edges == null)
            return;

        for (int i = 0; i < edges.Length; i++)
        {
            ActionGraphEdge edge = edges[i];
            if (edge == null || edge.FromNodeId != fromNodeId || edge.RouteKind != routeKind)
                continue;

            if (!TryGetNode(edge.ToNodeId, out ActionGraphNode toNode))
                continue;

            GameplayIntentType trigger = toNode.Action.Trigger;
            if (trigger != GameplayIntentType.None)
                results.Add(trigger);
        }

        if (!TryGetNode(fromNodeId, out ActionGraphNode fromNode) || sharedRoutes == null)
            return;

        GameplayIntentType sourceTrigger = fromNode.Action.Trigger;
        for (int i = 0; i < sharedRoutes.Length; i++)
        {
            ActionGraphSharedRoute route = sharedRoutes[i];
            if (route == null
                || route.RouteKind != routeKind
                || route.Intent == GameplayIntentType.None
                || (route.SourceTrigger != GameplayIntentType.None
                    && route.SourceTrigger != sourceTrigger))
            {
                continue;
            }

            results.Add(route.Intent);
        }
    }

    /// <summary>收集图中全部有效 Trigger 意图（去重）。</summary>
    public void CollectTriggerIntents(HashSet<GameplayIntentType> results)
    {
        results.Clear();
        if (nodes == null)
            return;

        for (int i = 0; i < nodes.Length; i++)
        {
            GameplayIntentType trigger = nodes[i]?.Action != null
                ? nodes[i].Action.Trigger
                : GameplayIntentType.None;
            if (trigger != GameplayIntentType.None)
                results.Add(trigger);
        }
    }

    /// <summary>进入逻辑节点：可选 VariantResolver 决定播放变体，但游标始终保持在该节点。</summary>
    bool FinalizeNodeResolve(
        ActionGraphNode node,
        in ActionRequest request,
        in ActionResolveContext context,
        out ActionResolveResult result)
    {
        result = default;
        ActionResolver variantResolver = node.VariantResolver;
        if (variantResolver != null)
        {
            if (!variantResolver.TryResolve(in request, in context, out ActionResolveResult variantResult)
                || !variantResult.IsValid)
            {
                return false;
            }

            // 变体只改变实际播放 Action，不改变逻辑图游标；六向 Dodge 因此共享一套出边。
            result = ActionResolveResult.FromGraph(variantResult.Action, this, node.NodeId);
            return true;
        }

        result = ActionResolveResult.FromGraph(node.Action, this, node.NodeId);
        return result.IsValid;
    }
}

/// <summary>连招图节点：可标记为 Locomotion 起手入口；Trigger 来自 Action。</summary>
[Serializable]
public class ActionGraphNode
{
    [SerializeField] string nodeId;
    [SerializeField] ActionDefinition action;
    [Tooltip("可作为 Locomotion 起手；同一图可有多个 Entry（Attack / Dodge 等，靠 Trigger 区分）。")]
    [SerializeField] bool isEntry;
    [Tooltip("可选：进入本节点前解析实际播放变体（如 Directional 闪避）；变体共用当前逻辑节点和出边。")]
    [SerializeField] ActionResolver variantResolver;
    [SerializeField] Vector2 editorPosition;

    /// <summary>图内唯一节点 id。</summary>
    public string NodeId => nodeId;

    /// <summary>本节点播放的招式。</summary>
    public ActionDefinition Action => action;

    /// <summary>是否可作为 Locomotion 起手入口。</summary>
    public bool IsEntry => isEntry;

    /// <summary>进入时可选的变体 Resolver（Directional 等）。</summary>
    public ActionResolver VariantResolver => variantResolver;

    /// <summary>编辑器布局位置。</summary>
    public Vector2 EditorPosition => editorPosition;

    /// <summary>写入编辑器拖拽后的坐标。</summary>
    public void SetEditorPosition(Vector2 position) => editorPosition = position;

    /// <summary>编辑器创建节点时赋值。</summary>
    public void SetIdentity(string id, ActionDefinition definition, bool entry = false)
    {
        nodeId = id;
        action = definition;
        isEntry = entry;
    }

    /// <summary>编辑器切换 Entry 标记。</summary>
    public void SetEntry(bool entry) => isEntry = entry;
}

/// <summary>连招图边：从节点的普通或 Perfect Cancel 通道派生到目标节点。</summary>
[Serializable]
public class ActionGraphEdge
{
    [SerializeField] string fromNodeId;
    [SerializeField] CancelWindowType routeKind;
    [SerializeField] string toNodeId;

    /// <summary>边起点节点。</summary>
    public string FromNodeId => fromNodeId;

    /// <summary>绑定的普通或 Perfect Cancel 通道。</summary>
    public CancelWindowType RouteKind => routeKind;

    /// <summary>边终点节点。</summary>
    public string ToNodeId => toNodeId;

    /// <summary>编辑器创建边时赋值。</summary>
    public void Set(string from, CancelWindowType route, string to)
    {
        fromNodeId = from;
        routeKind = route;
        toNodeId = to;
    }
}
