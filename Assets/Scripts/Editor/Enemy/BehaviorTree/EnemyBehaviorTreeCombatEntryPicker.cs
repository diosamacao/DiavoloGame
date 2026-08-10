using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// BT 编辑器：从运行时同一条配置链解析 ActionGraph，供 RequestCombatAction 选 Entry。
/// 真源：EnemyDefinition → CharacterConfig.CombatProfile → DefaultMode ActionGraph。
/// 不在 BT 资产上另挂 Graph，避免双配置。
/// </summary>
public static class EnemyBehaviorTreeCombatEntryPicker
{
    const string SessionKeyPrefix = "ACT.BT.EntryPicker.Def.";

    /// <summary>解析编辑器用 Graph 与来源说明（只读反查，不写资产）。</summary>
    public static bool TryResolveGraph(
        EnemyBehaviorTreeAsset tree,
        out ActionGraph graph,
        out EnemyDefinition sourceDefinition,
        out string sourceLabel)
    {
        graph = null;
        sourceDefinition = null;
        sourceLabel = string.Empty;
        if (tree == null)
            return false;

        List<EnemyDefinition> defs = FindDefinitionsReferencingTree(tree);
        if (defs.Count == 0)
            return false;

        EnemyDefinition chosen = ChooseDefinition(tree, defs);
        if (chosen == null)
            return false;

        if (!TryGetDefaultActionGraph(chosen, out graph) || graph == null)
            return false;

        sourceDefinition = chosen;
        sourceLabel = $"{chosen.name} → CombatProfile → {graph.name}";
        return true;
    }

    /// <summary>Validate：按反查到的 Graph 检查 Request Entry 可起手。</summary>
    public static void AppendEntryWarnings(
        EnemyBehaviorTreeAsset tree,
        EnemyBehaviorTreeValidationResult result)
    {
        if (tree == null || tree.CustomRoot == null || result == null)
            return;

        if (!TryResolveGraph(tree, out ActionGraph graph, out _, out _))
        {
            if (TreeHasRequestCombat(tree.CustomRoot))
            {
                result.AddWarning(
                    "无法反查 ActionGraph（需有 EnemyDefinition 引用本树且 CombatProfile 已配 Default 模式图），跳过 Entry 可起手校验。");
            }

            return;
        }

        EnemyBehaviorTreeValidator.ValidateRequestCombatEntries(tree.CustomRoot, graph, result);
    }

    /// <summary>绘制 Entry 下拉；Graph 只读显示来源链路。</summary>
    public static void DrawRequestCombatFields(RequestCombatActionDef request, EnemyBehaviorTreeAsset tree)
    {
        if (request == null)
            return;

        List<EnemyDefinition> defs = tree != null
            ? FindDefinitionsReferencingTree(tree)
            : new List<EnemyDefinition>();

        if (defs.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "未找到引用本行为树的 EnemyDefinition。\n" +
                "请在 EnemyDefinition 上挂本树，并配置 CharacterConfig → CombatProfile → Default 模式 ActionGraph。\n" +
                "ActionGraph 只在该处配置一次，勿在 BT 资产上重复绑定。",
                MessageType.Warning);
            request.EntryNodeId = EditorGUILayout.TextField("Entry Node Id", request.EntryNodeId ?? string.Empty);
            return;
        }

        if (defs.Count > 1)
        {
            var names = new string[defs.Count];
            for (int i = 0; i < defs.Count; i++)
                names[i] = defs[i] != null ? defs[i].name : "(null)";

            int idx = GetSessionDefIndex(tree, defs.Count);
            int next = EditorGUILayout.Popup(
                new GUIContent("配置来源", "多份 EnemyDefinition 引用同一棵树时，选择用哪份 CombatProfile 的 Graph"),
                idx,
                names);
            if (next != idx)
                SetSessionDefIndex(tree, next);
        }

        if (!TryResolveGraph(tree, out ActionGraph graph, out EnemyDefinition sourceDef, out string sourceLabel)
            || graph == null)
        {
            EditorGUILayout.HelpBox(
                "已找到 EnemyDefinition，但 Default 模式 ActionGraph 未配置。请到 CharacterConfig.CombatProfile 填写。",
                MessageType.Warning);
            request.EntryNodeId = EditorGUILayout.TextField("Entry Node Id", request.EntryNodeId ?? string.Empty);
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField(
                new GUIContent("Action Graph", "只读：来自 EnemyDefinition 配置链"),
                graph,
                typeof(ActionGraph),
                false);
            if (sourceDef != null)
            {
                EditorGUILayout.ObjectField(
                    new GUIContent("Enemy Definition", "只读：引用本树的定义"),
                    sourceDef,
                    typeof(EnemyDefinition),
                    false);
            }
        }

        EditorGUILayout.LabelField("链路", sourceLabel, EditorStyles.miniLabel);

        List<EntryOption> options = CollectEntryOptions(graph);
        if (options.Count == 0)
        {
            EditorGUILayout.HelpBox(
                $"ActionGraph「{graph.name}」没有 IsEntry 且已挂 Action 的节点。",
                MessageType.Warning);
            request.EntryNodeId = EditorGUILayout.TextField("Entry Node Id", request.EntryNodeId ?? string.Empty);
            return;
        }

        string current = request.EntryNodeId ?? string.Empty;
        int selected = FindOptionIndex(options, current);
        bool currentInvalid = !string.IsNullOrEmpty(current) && selected < 0;

        var labels = new string[options.Count + 1];
        labels[0] = currentInvalid ? $"（无效）{current}" : "（未选择 Entry）";
        for (int i = 0; i < options.Count; i++)
            labels[i + 1] = options[i].Label;

        int popupIndex = selected >= 0 ? selected + 1 : 0;
        int nextEntry = EditorGUILayout.Popup(
            new GUIContent("Entry", "ActiveGraph 上标记为 Entry 的节点"),
            popupIndex,
            labels);

        if (nextEntry > 0)
            request.EntryNodeId = options[nextEntry - 1].NodeId;
        else if (selected >= 0)
            request.EntryNodeId = string.Empty;

        if (!string.IsNullOrEmpty(request.EntryNodeId) && FindOptionIndex(options, request.EntryNodeId) < 0)
        {
            EditorGUILayout.HelpBox(
                $"当前 Entry「{request.EntryNodeId}」不在「{graph.name}」的 Entry 列表中。",
                MessageType.Error);
            request.EntryNodeId = EditorGUILayout.TextField("Entry Node Id（手改）", request.EntryNodeId);
        }
        else if (!string.IsNullOrEmpty(request.EntryNodeId))
        {
            EditorGUILayout.LabelField("Node Id", request.EntryNodeId);
        }
    }

    /// <summary>收集图上可起手 Entry（IsEntry + Action）。</summary>
    public static List<EntryOption> CollectEntryOptions(ActionGraph graph)
    {
        var list = new List<EntryOption>();
        if (graph == null || graph.Nodes == null)
            return list;

        IReadOnlyList<ActionGraphNode> nodes = graph.Nodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            ActionGraphNode node = nodes[i];
            if (node == null || !node.IsEntry || node.Action == null)
                continue;
            if (string.IsNullOrEmpty(node.NodeId))
                continue;

            string actionName = node.Action != null ? node.Action.name : "?";
            string label = $"{node.NodeId}  ·  {actionName}  [{node.Intent}]";
            list.Add(new EntryOption(node.NodeId, label));
        }

        return list;
    }

    static EnemyDefinition ChooseDefinition(EnemyBehaviorTreeAsset tree, List<EnemyDefinition> defs)
    {
        if (defs == null || defs.Count == 0)
            return null;
        int idx = GetSessionDefIndex(tree, defs.Count);
        return defs[idx];
    }

    static int GetSessionDefIndex(EnemyBehaviorTreeAsset tree, int count)
    {
        if (tree == null || count <= 0)
            return 0;
        int idx = SessionState.GetInt(SessionKeyPrefix + tree.GetInstanceID(), 0);
        if (idx < 0 || idx >= count)
            return 0;
        return idx;
    }

    static void SetSessionDefIndex(EnemyBehaviorTreeAsset tree, int index)
    {
        if (tree == null)
            return;
        SessionState.SetInt(SessionKeyPrefix + tree.GetInstanceID(), Mathf.Max(0, index));
    }

    static List<EnemyDefinition> FindDefinitionsReferencingTree(EnemyBehaviorTreeAsset tree)
    {
        var list = new List<EnemyDefinition>();
        if (tree == null)
            return list;

        string[] guids = AssetDatabase.FindAssets("t:EnemyDefinition");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (def != null && def.BehaviorTree == tree)
                list.Add(def);
        }

        return list;
    }

    static bool TryGetDefaultActionGraph(EnemyDefinition definition, out ActionGraph graph)
    {
        graph = null;
        if (definition == null)
            return false;

        CharacterConfig config = definition.CharacterConfig;
        CombatModeProfile profile = config != null ? config.CombatProfile : null;
        return profile != null && profile.TryGetActionGraph(profile.DefaultMode, out graph) && graph != null;
    }

    static bool TreeHasRequestCombat(EnemyBehaviorNodeDef node)
    {
        if (node == null)
            return false;
        if (node is RequestCombatActionDef)
            return true;

        if (EnemyBehaviorTreeGraphMapper.TryGetChildren(node, out List<EnemyBehaviorNodeDef> children))
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (TreeHasRequestCombat(children[i]))
                    return true;
            }

            return false;
        }

        if (EnemyBehaviorTreeGraphMapper.TryGetSingleChild(node, out EnemyBehaviorNodeDef child))
            return TreeHasRequestCombat(child);

        return false;
    }

    static int FindOptionIndex(List<EntryOption> options, string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return -1;
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i].NodeId == nodeId)
                return i;
        }

        return -1;
    }

    /// <summary>下拉选项。</summary>
    public readonly struct EntryOption
    {
        public readonly string NodeId;
        public readonly string Label;

        public EntryOption(string nodeId, string label)
        {
            NodeId = nodeId;
            Label = label;
        }
    }
}
