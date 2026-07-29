using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>ActionGraph 自定义 Inspector：节点意图、执行上下文、流程边与校验。</summary>
[CustomEditor(typeof(ActionGraph))]
public class ActionGraphInspector : Editor
{
    SerializedProperty _nodes;
    SerializedProperty _edges;
    SerializedProperty _sharedRoutes;

    void OnEnable()
    {
        _nodes = serializedObject.FindProperty("nodes");
        _edges = serializedObject.FindProperty("edges");
        _sharedRoutes = serializedObject.FindProperty("sharedRoutes");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Action Graph", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "勾选节点 Is Entry 作为 Locomotion 起手；输入 Intent、索敌、起手行为和自动衔接都配置在节点。\n" +
            "每招一个 Normal、可选一个 Perfect CancelWindow；同 Intent 重叠时 Perfect 优先。\n" +
            "Graph Editor 可将节点合并为顺序组：每行独立 In，普通 Cancel 自动进入下一行。\n" +
            "方向闪避只保留一个 Entry + Directional Resolver，六向变体共用该逻辑节点。",
            MessageType.Info);

        DrawNodes();
        EditorGUILayout.Space(6);
        DrawEdges();
        EditorGUILayout.Space(8);
        DrawSharedRoutes();
        EditorGUILayout.Space(8);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Validate"))
                ValidateGraph((ActionGraph)target);

            if (GUILayout.Button("Open Graph Editor"))
                ActionGraphEditorWindow.Open((ActionGraph)target);
        }

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>绘制图级共享路由；一条规则替代多个来源节点的重复连线。</summary>
    void DrawSharedRoutes()
    {
        EditorGUILayout.LabelField("Shared Routes (Implicit)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "显式边未匹配时才使用。Source=None 表示任意来源；按来源节点 Intent + Normal/Perfect + 请求 Intent 路由。",
            MessageType.None);

        if (GUILayout.Button("Add Shared Route"))
            _sharedRoutes.arraySize++;

        List<string> nodeIds = CollectNodeIds();
        for (int i = 0; i < _sharedRoutes.arraySize; i++)
        {
            SerializedProperty route = _sharedRoutes.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(
                        route.FindPropertyRelative("sourceIntent"),
                        new GUIContent("Source"),
                        GUILayout.MinWidth(130));
                    EditorGUILayout.PropertyField(
                        route.FindPropertyRelative("intent"),
                        new GUIContent("Intent"),
                        GUILayout.MinWidth(130));
                    if (GUILayout.Button("×", GUILayout.Width(24)))
                    {
                        _sharedRoutes.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }

                EditorGUILayout.PropertyField(
                    route.FindPropertyRelative("routeKind"),
                    new GUIContent("Route"));
                DrawStringPopup(
                    route.FindPropertyRelative("toNodeId"),
                    nodeIds,
                    "Target",
                    180);
            }
        }
    }

    void DrawNodes()
    {
        EditorGUILayout.LabelField("Nodes", EditorStyles.boldLabel);
        Rect drop = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
        GUI.Box(drop, "拖入 ActionDefinition 到此处添加节点", EditorStyles.helpBox);
        HandleActionDrop(drop);

        for (int i = 0; i < _nodes.arraySize; i++)
        {
            SerializedProperty node = _nodes.GetArrayElementAtIndex(i);
            SerializedProperty nodeId = node.FindPropertyRelative("nodeId");
            SerializedProperty action = node.FindPropertyRelative("action");
            SerializedProperty intent = node.FindPropertyRelative("intent");
            SerializedProperty isEntry = node.FindPropertyRelative("isEntry");
            SerializedProperty variant = node.FindPropertyRelative("variantResolver");

            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    isEntry.boolValue = GUILayout.Toggle(isEntry.boolValue, "Entry", GUILayout.Width(52));
                    EditorGUILayout.PropertyField(nodeId, GUIContent.none, GUILayout.Width(100));
                    EditorGUILayout.PropertyField(action, GUIContent.none);
                    if (GUILayout.Button("×", GUILayout.Width(24)))
                    {
                        _nodes.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }

                EditorGUILayout.PropertyField(intent, new GUIContent("Intent"));
                EditorGUILayout.PropertyField(variant, new GUIContent("Variant Resolver"));
                EditorGUILayout.PropertyField(
                    node.FindPropertyRelative("targetLockSettings"),
                    new GUIContent("Target Lock"),
                    includeChildren: true);
                EditorGUILayout.PropertyField(
                    node.FindPropertyRelative("startBehaviors"),
                    new GUIContent("Start Behaviors"),
                    includeChildren: true);
                EditorGUILayout.PropertyField(
                    node.FindPropertyRelative("switchCombatModeTarget"));
                EditorGUILayout.PropertyField(
                    node.FindPropertyRelative("switchCombatModePolicy"));
                EditorGUILayout.PropertyField(
                    node.FindPropertyRelative("automaticTransitions"),
                    new GUIContent("Automatic Transitions"),
                    includeChildren: true);

                ActionDefinition def = action.objectReferenceValue as ActionDefinition;
                if (def != null)
                {
                    DrawCancelSlotsPreview(def);
                }
            }
        }
    }

    void DrawEdges()
    {
        EditorGUILayout.LabelField("Edges", EditorStyles.boldLabel);
        if (GUILayout.Button("Add Edge"))
            _edges.arraySize++;

        List<string> nodeIds = CollectNodeIds();
        for (int i = 0; i < _edges.arraySize; i++)
        {
            SerializedProperty edge = _edges.GetArrayElementAtIndex(i);
            SerializedProperty from = edge.FindPropertyRelative("fromNodeId");
            SerializedProperty route = edge.FindPropertyRelative("routeKind");
            SerializedProperty to = edge.FindPropertyRelative("toNodeId");

            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawStringPopup(from, nodeIds, "From", 80);
                    EditorGUILayout.PropertyField(route, GUIContent.none, GUILayout.Width(110));
                    DrawStringPopup(to, nodeIds, "To", 80);
                    if (GUILayout.Button("×", GUILayout.Width(24)))
                    {
                        _edges.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }

                string intentLabel = ResolveTargetIntentLabel(to.stringValue);
                if (!string.IsNullOrEmpty(intentLabel))
                    EditorGUILayout.LabelField($"匹配 Intent: {intentLabel}", EditorStyles.miniLabel);
            }
        }
    }

    static void DrawStringPopup(SerializedProperty prop, List<string> options, string label, float width)
    {
        if (options.Count == 0)
        {
            EditorGUILayout.PropertyField(prop, new GUIContent(label), GUILayout.Width(width + 40));
            return;
        }

        int index = Mathf.Max(0, options.IndexOf(prop.stringValue));
        int next = EditorGUILayout.Popup(label, index, options.ToArray(), GUILayout.Width(width + 60));
        if (next >= 0 && next < options.Count)
            prop.stringValue = options[next];
    }

    void HandleActionDrop(Rect dropArea)
    {
        Event evt = Event.current;
        if (!dropArea.Contains(evt.mousePosition))
            return;

        if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
            return;

        bool hasAction = false;
        foreach (Object obj in DragAndDrop.objectReferences)
        {
            if (obj is ActionDefinition)
            {
                hasAction = true;
                break;
            }
        }

        if (!hasAction)
            return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (evt.type != EventType.DragPerform)
            return;

        DragAndDrop.AcceptDrag();
        foreach (Object obj in DragAndDrop.objectReferences)
        {
            if (obj is ActionDefinition action)
                AddNode(action);
        }

        evt.Use();
    }

    void AddNode(ActionDefinition action)
    {
        int index = _nodes.arraySize;
        _nodes.arraySize++;
        SerializedProperty node = _nodes.GetArrayElementAtIndex(index);
        string baseId = string.IsNullOrEmpty(action.name) ? "Node" : action.name;
        node.FindPropertyRelative("nodeId").stringValue = MakeUniqueNodeId(baseId);
        node.FindPropertyRelative("action").objectReferenceValue = action;
        node.FindPropertyRelative("intent").enumValueIndex = (int)GameplayIntentType.None;
        node.FindPropertyRelative("isEntry").boolValue = false;
        node.FindPropertyRelative("variantResolver").objectReferenceValue = null;
        ActionGraphView.ResetNodePolicy(node);
        node.FindPropertyRelative("editorPosition").vector2Value = new Vector2(80 + index * 40, 80 + index * 24);
    }

    string MakeUniqueNodeId(string baseId)
    {
        HashSet<string> existing = new(CollectNodeIds());
        if (!existing.Contains(baseId))
            return baseId;

        int i = 2;
        while (existing.Contains(baseId + "_" + i))
            i++;
        return baseId + "_" + i;
    }

    List<string> CollectNodeIds()
    {
        var ids = new List<string>();
        for (int i = 0; i < _nodes.arraySize; i++)
        {
            string id = _nodes.GetArrayElementAtIndex(i).FindPropertyRelative("nodeId").stringValue;
            if (!string.IsNullOrEmpty(id))
                ids.Add(id);
        }

        return ids;
    }

    string ResolveTargetIntentLabel(string toNodeId)
    {
        for (int i = 0; i < _nodes.arraySize; i++)
        {
            SerializedProperty node = _nodes.GetArrayElementAtIndex(i);
            if (node.FindPropertyRelative("nodeId").stringValue == toNodeId)
                return ((GameplayIntentType)node.FindPropertyRelative("intent").enumValueIndex).ToString();
        }

        return null;
    }

    static void DrawCancelSlotsPreview(ActionDefinition action)
    {
        foreach (CancelWindowNotifyState window in action.Timeline.CancelWindowStates)
        {
            if (window == null)
                continue;
            EditorGUILayout.LabelField(
                $"  {window.WindowType} Cancel f{window.StartFrame}-{window.EndFrame}",
                EditorStyles.miniLabel);
        }
    }

    /// <summary>校验多 Entry Intent、双通道边、自动衔接、顺序组与路由冲突。</summary>
    public static void ValidateGraph(ActionGraph graph)
    {
        if (graph == null)
            return;

        var errors = new List<string>();
        int entryCount = 0;
        var entryIntents = new HashSet<GameplayIntentType>();

        foreach (ActionGraphNode node in graph.Nodes)
        {
            if (node == null || !node.IsEntry || node.Action == null)
                continue;

            entryCount++;
            GameplayIntentType intent = node.Intent;
            if (intent == GameplayIntentType.None)
            {
                errors.Add($"Entry '{node.NodeId}' 的 Intent 未配置。");
                continue;
            }

            // 每种 Intent 只允许一个逻辑 Entry；Directional 变体由该 Entry 的 Resolver 折叠。
            if (!entryIntents.Add(intent))
            {
                errors.Add(
                    $"多个 Entry 使用相同 Intent {intent}：'{node.NodeId}'。请折叠为一个逻辑 Entry。");
            }
        }

        if (entryCount == 0)
            errors.Add("至少需要一个 Is Entry 节点作为 Locomotion 起手。");

        var groupIds = new HashSet<string>();
        var groupedNodes = new HashSet<string>();
        foreach (ActionGraphNodeGroup group in graph.NodeGroups)
        {
            if (group == null)
                continue;
            if (string.IsNullOrWhiteSpace(group.GroupId) || !groupIds.Add(group.GroupId))
                errors.Add($"顺序组 Id 为空或重复: '{group.GroupId}'。");
            if (group.ChildNodeIds.Count == 0)
                errors.Add($"顺序组 '{group.GroupId}' 没有 Action。");

            foreach (string childNodeId in group.ChildNodeIds)
            {
                if (!graph.TryGetNode(childNodeId, out ActionGraphNode child))
                {
                    errors.Add($"顺序组 '{group.GroupId}' 包含无效节点: {childNodeId}");
                    continue;
                }

                if (!groupedNodes.Add(childNodeId))
                    errors.Add($"节点 '{childNodeId}' 同时属于多个顺序组。");
            }
        }

        foreach (ActionGraphNode node in graph.Nodes)
        {
            if (node?.Action != null)
            {
                ValidateCancelWindows(node.Action, errors);
                ValidateAutomaticTransitions(graph, node, errors);
            }
        }

        var intentKeys = new HashSet<string>();
        foreach (ActionGraphEdge edge in graph.Edges)
        {
            if (edge == null)
                continue;

            if (!graph.TryGetNode(edge.FromNodeId, out ActionGraphNode from))
            {
                errors.Add($"边 From 无效: {edge.FromNodeId}");
                continue;
            }

            if (!graph.TryGetNode(edge.ToNodeId, out ActionGraphNode to))
            {
                errors.Add($"边 To 无效: {edge.ToNodeId}");
                continue;
            }

            CancelWindowNotifyState window = from.Action.GetCancelWindow(edge.RouteKind);
            if (window == null)
                errors.Add($"节点 {edge.FromNodeId} 缺少 {edge.RouteKind} CancelWindow。");

            GameplayIntentType intent = to.Intent;
            if (intent == GameplayIntentType.None)
            {
                errors.Add($"目标 {edge.ToNodeId} 的 Intent 未配置。");
                continue;
            }

            string edgeKey = $"{edge.FromNodeId}|{edge.RouteKind}|{intent}";
            if (!intentKeys.Add(edgeKey))
                errors.Add($"同路由 Intent 冲突: {edge.FromNodeId}/{edge.RouteKind} → {intent}");
        }

        var validatedSharedRoutes = new List<ActionGraphSharedRoute>();
        foreach (ActionGraphSharedRoute route in graph.SharedRoutes)
        {
            if (route == null)
                continue;

            if (route.Intent == GameplayIntentType.None)
                errors.Add($"Shared Route '{route.RouteKind}' 的 Intent 不能为 None。");
            if (!graph.TryGetNode(route.ToNodeId, out ActionGraphNode target))
            {
                errors.Add($"Shared Route 目标节点无效: {route.ToNodeId}");
                continue;
            }

            if (target.Intent != route.Intent)
            {
                errors.Add(
                    $"Shared Route '{route.RouteKind}' Intent={route.Intent} 与目标 " +
                    $"'{route.ToNodeId}' Intent={target.Intent} 不一致。");
            }

            foreach (ActionGraphSharedRoute existing in validatedSharedRoutes)
            {
                bool sourceOverlaps = existing.SourceIntent == GameplayIntentType.None
                    || route.SourceIntent == GameplayIntentType.None
                    || existing.SourceIntent == route.SourceIntent;
                if (sourceOverlaps
                    && existing.RouteKind == route.RouteKind
                    && existing.Intent == route.Intent)
                {
                    errors.Add(
                        $"Shared Route 冲突: {route.SourceIntent}/{route.RouteKind}/{route.Intent}");
                    break;
                }
            }

            validatedSharedRoutes.Add(route);

            bool matchingRouteFound = false;
            foreach (ActionGraphNode node in graph.Nodes)
            {
                if (node?.Action == null
                    || (route.SourceIntent != GameplayIntentType.None
                        && node.Intent != route.SourceIntent))
                {
                    continue;
                }

                CancelWindowNotifyState sourceWindow =
                    node.Action.GetCancelWindow(route.RouteKind);
                if (sourceWindow != null)
                {
                    matchingRouteFound = true;
                    break;
                }
            }

            if (!matchingRouteFound)
            {
                errors.Add(
                    $"Shared Route '{route.RouteKind}' 找不到匹配来源窗口。");
            }
        }

        // 显式边与完全覆盖它的共享路由同时存在只会制造视觉噪音，应删除显式边。
        foreach (ActionGraphEdge edge in graph.Edges)
        {
            if (edge == null
                || !graph.TryGetNode(edge.FromNodeId, out ActionGraphNode from)
                || !graph.TryGetNode(edge.ToNodeId, out ActionGraphNode to))
            {
                continue;
            }

            foreach (ActionGraphSharedRoute route in graph.SharedRoutes)
            {
                if (route == null
                    || route.RouteKind != edge.RouteKind
                    || route.Intent != to.Intent
                    || route.ToNodeId != edge.ToNodeId)
                {
                    continue;
                }

                if (route.SourceIntent == GameplayIntentType.None
                    || route.SourceIntent == from.Intent)
                {
                    errors.Add(
                        $"冗余显式边: {edge.FromNodeId}/{edge.RouteKind} → {edge.ToNodeId} " +
                        "已由 Shared Route 覆盖。");
                }
            }
        }

        if (errors.Count == 0)
            Debug.Log($"[ActionGraph] '{graph.name}' 校验通过（Entry × {entryCount}）。", graph);
        else
        {
            foreach (string error in errors)
                Debug.LogError($"[ActionGraph] {error}", graph);
        }
    }

    /// <summary>校验节点自动衔接目标与同优先级规则，防止流程配置悬空或不确定。</summary>
    static void ValidateAutomaticTransitions(
        ActionGraph graph,
        ActionGraphNode node,
        List<string> errors)
    {
        var priorities = new HashSet<int>();
        foreach (ActionGraphTransition transition in node.AutomaticTransitions)
        {
            if (transition == null)
                continue;

            if (!priorities.Add(transition.Priority))
            {
                errors.Add(
                    $"节点 '{node.NodeId}' 存在重复自动衔接优先级 {transition.Priority}。");
            }

            if (!string.IsNullOrEmpty(transition.TargetNodeId)
                && !graph.TryGetNode(transition.TargetNodeId, out _))
            {
                errors.Add(
                    $"节点 '{node.NodeId}' 的自动衔接目标无效: {transition.TargetNodeId}");
            }
        }
    }

    /// <summary>校验每个 Action 恰有一个 Normal，且最多一个 Perfect CancelWindow。</summary>
    static void ValidateCancelWindows(ActionDefinition action, List<string> errors)
    {
        int normalCount = 0;
        int perfectCount = 0;
        foreach (CancelWindowNotifyState window in action.Timeline.CancelWindowStates)
        {
            if (window == null)
                continue;

            if (window.WindowType == CancelWindowType.Perfect)
                perfectCount++;
            else
                normalCount++;
        }

        if (normalCount != 1)
            errors.Add($"Action '{action.name}' 必须且只能配置一个 Normal CancelWindow。");
        if (perfectCount > 1)
            errors.Add($"Action '{action.name}' 最多只能配置一个 Perfect CancelWindow。");
    }
}
