using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 连招图资产：节点引用 ActionDefinition，边从 Cancel 槽派生到目标节点。
/// 支持多个 Locomotion 起手入口（按目标招 Trigger 匹配，可同时含攻击/闪避等）。
/// </summary>
[CreateAssetMenu(fileName = "ActionGraph", menuName = "ACT/Combat/Action Graph")]
public class ActionGraph : ScriptableObject
{
    [SerializeField] ActionGraphNode[] nodes = Array.Empty<ActionGraphNode>();
    [SerializeField] ActionGraphEdge[] edges = Array.Empty<ActionGraphEdge>();

    /// <summary>图节点列表。</summary>
    public IReadOnlyList<ActionGraphNode> Nodes => nodes ?? Array.Empty<ActionGraphNode>();

    /// <summary>图边列表。</summary>
    public IReadOnlyList<ActionGraphEdge> Edges => edges ?? Array.Empty<ActionGraphEdge>();

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
    /// 若节点配置了 VariantResolver（如 Directional），则再解析变体并尽量落到对应节点。
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

            ActionTrigger trigger = node.Action.Trigger;
            if (trigger == null || !trigger.Matches(in request))
                continue;

            return FinalizeNodeResolve(node, in request, in context, out result);
        }

        return false;
    }

    /// <summary>Cancel：按 (CurrentNodeId, CancelSlotId) 出边，目标 Trigger 匹配 request。</summary>
    public bool TryResolveCancel(
        in ActionRequest request,
        in ActionResolveContext context,
        out ActionResolveResult result)
    {
        result = default;
        if (string.IsNullOrEmpty(context.CurrentNodeId) || string.IsNullOrEmpty(context.CancelSlotId))
            return false;

        var edgeBuffer = new List<ActionGraphEdge>(8);
        CollectEdges(context.CurrentNodeId, context.CancelSlotId, edgeBuffer);

        for (int i = 0; i < edgeBuffer.Count; i++)
        {
            ActionGraphEdge edge = edgeBuffer[i];
            if (!TryGetNode(edge.ToNodeId, out ActionGraphNode toNode))
                continue;

            ActionTrigger trigger = toNode.Action.Trigger;
            if (trigger == null || !trigger.Matches(in request))
                continue;

            return FinalizeNodeResolve(toNode, in request, in context, out result);
        }

        return false;
    }

    /// <summary>枚举从指定节点、指定 Cancel 槽出发的边。</summary>
    public void CollectEdges(string fromNodeId, string cancelSlotId, List<ActionGraphEdge> results)
    {
        results.Clear();
        if (edges == null || string.IsNullOrEmpty(fromNodeId) || string.IsNullOrEmpty(cancelSlotId))
            return;

        for (int i = 0; i < edges.Length; i++)
        {
            ActionGraphEdge edge = edges[i];
            if (edge == null)
                continue;

            if (edge.FromNodeId == fromNodeId && edge.CancelSlotId == cancelSlotId)
                results.Add(edge);
        }
    }

    /// <summary>收集某节点某槽出边目标招的 Trigger inputId（去重）。</summary>
    public void CollectCancelCandidateInputIds(string fromNodeId, string cancelSlotId, HashSet<string> results)
    {
        results.Clear();
        if (edges == null)
            return;

        for (int i = 0; i < edges.Length; i++)
        {
            ActionGraphEdge edge = edges[i];
            if (edge == null || edge.FromNodeId != fromNodeId || edge.CancelSlotId != cancelSlotId)
                continue;

            if (!TryGetNode(edge.ToNodeId, out ActionGraphNode toNode))
                continue;

            ActionTrigger trigger = toNode.Action.Trigger;
            if (trigger != null && trigger.IsValid)
                results.Add(trigger.InputId);
        }
    }

    /// <summary>收集图中全部有效 Trigger 的 InputActionReference（起手 Entry + 任意节点，供输入注册）。</summary>
    public void CollectTriggerInputReferences(List<InputActionReference> results)
    {
        results.Clear();
        if (nodes == null)
            return;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < nodes.Length; i++)
        {
            ActionGraphNode node = nodes[i];
            ActionTrigger trigger = node?.Action?.Trigger;
            if (trigger == null || !trigger.IsValid)
                continue;

            string inputId = trigger.InputId;
            if (!seen.Add(inputId))
                continue;

            InputActionReference reference = trigger.InputReference;
            if (reference != null)
                results.Add(reference);
        }
    }

    /// <summary>枚举图中全部 Trigger inputId（去重）。</summary>
    public void CollectTriggerInputIds(HashSet<string> results)
    {
        results.Clear();
        if (nodes == null)
            return;

        for (int i = 0; i < nodes.Length; i++)
        {
            ActionTrigger trigger = nodes[i]?.Action?.Trigger;
            if (trigger != null && trigger.IsValid)
                results.Add(trigger.InputId);
        }
    }

    /// <summary>进入节点：可选 VariantResolver（方向闪避）后再定位游标节点。</summary>
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

            // 优先落到与变体 Action 对应的图节点，便于 Cancel 边按正确节点配置。
            if (TryFindNodeByAction(variantResult.Action, out ActionGraphNode variantNode))
            {
                result = ActionResolveResult.FromGraph(variantResult.Action, this, variantNode.NodeId);
                return true;
            }

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
    [Tooltip("可选：进入本节点前再解析变体（如 Directional 闪避）。变体 Action 最好也在图中有对应节点。")]
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

/// <summary>连招图边：从某节点的 Cancel 槽派生到目标节点（可与起点相同，表示连回自身 In）。</summary>
[Serializable]
public class ActionGraphEdge
{
    [SerializeField] string fromNodeId;
    [SerializeField] string cancelSlotId;
    [SerializeField] string toNodeId;

    /// <summary>边起点节点。</summary>
    public string FromNodeId => fromNodeId;

    /// <summary>绑定的 Cancel 槽 id（= CancelWindow 时间轴条目 Id）。</summary>
    public string CancelSlotId => cancelSlotId;

    /// <summary>边终点节点。</summary>
    public string ToNodeId => toNodeId;

    /// <summary>编辑器创建边时赋值。</summary>
    public void Set(string from, string slotId, string to)
    {
        fromNodeId = from;
        cancelSlotId = slotId;
        toNodeId = to;
    }
}
