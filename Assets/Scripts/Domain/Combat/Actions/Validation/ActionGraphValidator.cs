using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>动作图结构校验唯一入口；无副作用，草稿可保存，错误阻止内容启动。</summary>
public static class ActionGraphValidator
{
    /// <summary>收集节点、派生窗口、组、自动衔接与共享路由问题；终结动作无需强制配置 Cancel。</summary>
    public static List<ContentValidationIssue> Validate(ActionGraph graph)
    {
        var issues = new List<ContentValidationIssue>();
        if (graph == null)
        {
            issues.Add(new ContentValidationIssue("Graph.Missing", null, "", "未绑定动作图。"));
            return issues;
        }
        void Add(string code, string path, string message, string id = null,
            ContentIssueSeverity severity = ContentIssueSeverity.Error) =>
            issues.Add(new ContentValidationIssue(code, graph, path, message, id, severity));
        var nodes = new Dictionary<string, ActionGraphNode>(StringComparer.Ordinal);
        var intents = new HashSet<GameplayIntentType>();
        int entries = 0;
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            ActionGraphNode node = graph.Nodes[i];
            string path = $"nodes.Array.data[{i}]";
            if (node == null) { Add("Node.Missing", path, "节点为空。"); continue; }
            if (string.IsNullOrWhiteSpace(node.NodeId) || nodes.ContainsKey(node.NodeId))
                Add("Node.InvalidId", path + ".nodeId", "节点 Id 为空或重复。", node.NodeId);
            else nodes.Add(node.NodeId, node);
            if (node.Action == null)
                Add("Node.MissingAction", path + ".action", "节点未绑定 Action。", node.NodeId);
            if (!node.IsEntry) continue;
            entries++;
            // None Entry 由 NodeId 寻址，不参与输入匹配，也不存在同意图冲突。
            if (node.Intent != GameplayIntentType.None
                && !intents.Add(node.Intent) && node.Intent != GameplayIntentType.Special)
                Add("Entry.DuplicateIntent", path + ".intent", $"重复入口意图 {node.Intent}。", node.NodeId);
        }
        if (entries == 0) Add("Graph.NoEntry", "nodes", "至少需要一个起手入口。");

        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            ActionGraphNode node = graph.Nodes[i];
            if (node == null) continue;
            string path = $"nodes.Array.data[{i}]";
            var priorities = new HashSet<int>();
            for (int t = 0; t < node.AutomaticTransitions.Count; t++)
            {
                ActionGraphTransition transition = node.AutomaticTransitions[t];
                if (transition == null) continue;
                string tp = path + $".automaticTransitions.Array.data[{t}]";
                if (!priorities.Add(transition.Priority))
                    Add("Transition.DuplicatePriority", tp + ".priority", "自动衔接优先级重复。", node.NodeId);
                if (!string.IsNullOrEmpty(transition.TargetNodeId) && !nodes.ContainsKey(transition.TargetNodeId))
                    Add("Transition.MissingTarget", tp + ".targetNodeId", "自动衔接目标不存在。", node.NodeId);
            }
            var actions = new HashSet<ActionDefinition>();
            if (node.Action != null) actions.Add(node.Action);
            var variants = new List<ActionDefinition>();
            node.VariantResolver?.CollectActions(variants);
            foreach (ActionDefinition variant in variants) if (variant != null) actions.Add(variant);
            foreach (ActionDefinition action in actions)
            {
                var kinds = new HashSet<CancelWindowType>();
                for (int w = 0; w < action.Timeline.CancelWindowStates.Length; w++)
                {
                    CancelWindowNotifyState window = action.Timeline.CancelWindowStates[w];
                    if (window != null && !kinds.Add(window.WindowType))
                        issues.Add(new ContentValidationIssue("Cancel.Duplicate", action,
                            $"timeline.cancelWindowStates.Array.data[{w}]", $"重复 {window.WindowType} 窗口。", node.NodeId));
                }
            }
        }

        var edgeKeys = new HashSet<string>();
        for (int i = 0; i < graph.Edges.Count; i++)
        {
            ActionGraphEdge edge = graph.Edges[i];
            string path = $"edges.Array.data[{i}]";
            if (edge == null) { Add("Edge.Missing", path, "边为空。"); continue; }
            if (!nodes.TryGetValue(edge.FromNodeId ?? "", out ActionGraphNode from))
            { Add("Edge.MissingSource", path + ".fromNodeId", "来源节点不存在。"); continue; }
            if (!nodes.TryGetValue(edge.ToNodeId ?? "", out ActionGraphNode to))
            { Add("Edge.MissingTarget", path + ".toNodeId", "目标节点不存在。"); continue; }
            if (from.Action != null && from.Action.GetCancelWindow(edge.RouteKind) == null)
                Add("Edge.MissingWindow", path, $"来源缺少 {edge.RouteKind} Cancel 窗口。", from.NodeId);
            if (to.Intent == GameplayIntentType.None)
                Add("Edge.MissingIntent", path, "派生目标需要 Intent。", to.NodeId);
            // Special 的普通/EX 多候选由能量选择器解析，与 Entry 使用相同语义。
            if (!edgeKeys.Add($"{from.NodeId}|{edge.RouteKind}|{to.Intent}") && to.Intent != GameplayIntentType.Special)
                Add("Edge.Conflict", path, "同来源、通道、意图存在多条边。", from.NodeId);
        }

        var groupIds = new HashSet<string>();
        var members = new HashSet<string>();
        for (int i = 0; i < graph.NodeGroups.Count; i++)
        {
            ActionGraphNodeGroup group = graph.NodeGroups[i];
            string path = $"nodeGroups.Array.data[{i}]";
            if (group == null) continue;
            if (string.IsNullOrWhiteSpace(group.GroupId) || !groupIds.Add(group.GroupId))
                Add("Group.InvalidId", path, "顺序组 Id 为空或重复。");
            if (group.ChildNodeIds.Count == 0) Add("Group.Empty", path, "顺序组为空。");
            foreach (string id in group.ChildNodeIds)
            {
                if (!nodes.ContainsKey(id ?? "")) Add("Group.MissingNode", path, $"组成员 {id} 不存在。");
                if (!members.Add(id)) Add("Group.DuplicateMembership", path, $"节点 {id} 重复归组。", id);
            }
        }
        for (int i = 0; i < graph.SharedRoutes.Count; i++)
        {
            ActionGraphSharedRoute route = graph.SharedRoutes[i];
            string path = $"sharedRoutes.Array.data[{i}]";
            if (route == null) continue;
            if (route.Intent == GameplayIntentType.None)
                Add("Route.MissingIntent", path + ".intent", "共享路由需要 Intent。");
            if (!nodes.TryGetValue(route.ToNodeId ?? "", out ActionGraphNode target))
                Add("Route.MissingTarget", path + ".toNodeId", "共享路由目标不存在。");
            else if (target.Intent != route.Intent)
                Add("Route.IntentMismatch", path + ".intent", "共享路由意图与目标不一致。", target.NodeId);
            bool source = false;
            foreach (ActionGraphNode node in nodes.Values)
                if (node.Action != null && (route.SourceIntent == GameplayIntentType.None || node.Intent == route.SourceIntent)
                    && node.Action.GetCancelWindow(route.RouteKind) != null) source = true;
            if (!source) Add("Route.NoSource", path, "共享路由没有匹配的来源窗口。");
            for (int j = 0; j < i; j++)
            {
                ActionGraphSharedRoute previous = graph.SharedRoutes[j];
                if (previous == null || previous.RouteKind != route.RouteKind || previous.Intent != route.Intent) continue;
                if (route.Intent != GameplayIntentType.Special && (previous.SourceIntent == GameplayIntentType.None || route.SourceIntent == GameplayIntentType.None
                    || previous.SourceIntent == route.SourceIntent))
                    Add("Route.Conflict", path, "共享路由来源范围重叠。");
            }
            foreach (ActionGraphEdge edge in graph.Edges)
                if (edge != null && edge.RouteKind == route.RouteKind && edge.ToNodeId == route.ToNodeId
                    && nodes.TryGetValue(edge.FromNodeId ?? "", out ActionGraphNode from)
                    && (route.SourceIntent == GameplayIntentType.None || route.SourceIntent == from.Intent))
                    Add("Route.RedundantEdge", path, "显式边被共享路由覆盖，可简化。", from.NodeId, ContentIssueSeverity.Warning);
        }
        return issues;
    }

    /// <summary>将同一校验结果输出日志；仅 Error 使启动门禁失败。</summary>
    public static bool ValidateAndLog(ActionGraph graph)
    {
        bool valid = true;
        foreach (ContentValidationIssue issue in Validate(graph))
        {
            if (issue.Severity == ContentIssueSeverity.Error)
            { Debug.LogError(issue.ToString(), issue.Asset); valid = false; }
            else Debug.LogWarning(issue.ToString(), issue.Asset);
        }
        return valid;
    }
}
