using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>ActionGraph 自定义 Inspector：多 Entry、节点/边、Trigger 预览与校验。</summary>
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
            "勾选节点 Is Entry 作为 Locomotion 起手；同一图可有多个 Entry（Attack / Dodge 等靠 Action.Trigger 区分）。\n" +
            "显式边只表达独特连招；重复的同槽去向用 Shared Route，Recovery Phase 自动按 Entry 重开。\n" +
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
            "显式边未匹配时才使用。Source=None 表示任意来源；按 Source Trigger + Cancel Slot + Intent 路由到目标节点。",
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
                        route.FindPropertyRelative("sourceTrigger"),
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
                    route.FindPropertyRelative("cancelSlotId"),
                    new GUIContent("Cancel Slot"));
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

                EditorGUILayout.PropertyField(variant, new GUIContent("Variant Resolver"));

                ActionDefinition def = action.objectReferenceValue as ActionDefinition;
                if (def != null)
                {
                    EditorGUILayout.LabelField(
                        $"Trigger: {def.Trigger}",
                        EditorStyles.miniLabel);
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
            SerializedProperty slot = edge.FindPropertyRelative("cancelSlotId");
            SerializedProperty to = edge.FindPropertyRelative("toNodeId");

            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawStringPopup(from, nodeIds, "From", 80);
                    DrawSlotPopup(from, slot);
                    DrawStringPopup(to, nodeIds, "To", 80);
                    if (GUILayout.Button("×", GUILayout.Width(24)))
                    {
                        _edges.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }

                string triggerLabel = ResolveTargetTriggerLabel(to.stringValue);
                if (!string.IsNullOrEmpty(triggerLabel))
                    EditorGUILayout.LabelField($"匹配 Trigger: {triggerLabel}", EditorStyles.miniLabel);
            }
        }
    }

    void DrawSlotPopup(SerializedProperty fromNodeId, SerializedProperty slotProp)
    {
        List<string> slots = CollectCancelSlotIds(fromNodeId.stringValue);
        if (slots.Count == 0)
        {
            EditorGUILayout.PropertyField(slotProp, GUIContent.none, GUILayout.MinWidth(100));
            return;
        }

        int index = Mathf.Max(0, slots.IndexOf(slotProp.stringValue));
        int next = EditorGUILayout.Popup(index, slots.ToArray(), GUILayout.MinWidth(100));
        if (next >= 0 && next < slots.Count)
            slotProp.stringValue = slots[next];
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
        node.FindPropertyRelative("isEntry").boolValue = false;
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

    List<string> CollectCancelSlotIds(string fromNodeId)
    {
        var slots = new List<string>();
        ActionDefinition action = FindNodeAction(fromNodeId);
        if (action == null)
            return slots;

        foreach (CancelWindowNotifyState window in action.Timeline.CancelWindowStates)
        {
            if (window == null)
                continue;
            string slot = window.CancelSlotId;
            if (!string.IsNullOrEmpty(slot) && !slots.Contains(slot))
                slots.Add(slot);
        }

        return slots;
    }

    ActionDefinition FindNodeAction(string nodeId)
    {
        for (int i = 0; i < _nodes.arraySize; i++)
        {
            SerializedProperty node = _nodes.GetArrayElementAtIndex(i);
            if (node.FindPropertyRelative("nodeId").stringValue != nodeId)
                continue;
            return node.FindPropertyRelative("action").objectReferenceValue as ActionDefinition;
        }

        return null;
    }

    string ResolveTargetTriggerLabel(string toNodeId)
    {
        ActionDefinition action = FindNodeAction(toNodeId);
        return action != null ? action.Trigger.ToString() : null;
    }

    static void DrawCancelSlotsPreview(ActionDefinition action)
    {
        foreach (CancelWindowNotifyState window in action.Timeline.CancelWindowStates)
        {
            if (window == null)
                continue;
            EditorGUILayout.LabelField(
                $"  Cancel [{window.CancelSlotId}] {window.CancelType} f{window.StartFrame}-{window.EndFrame}",
                EditorStyles.miniLabel);
        }
    }

    /// <summary>校验多 Entry Trigger、边、槽与同槽冲突。</summary>
    public static void ValidateGraph(ActionGraph graph)
    {
        if (graph == null)
            return;

        var errors = new List<string>();
        int entryCount = 0;
        var entryTriggers = new HashSet<GameplayIntentType>();

        foreach (ActionGraphNode node in graph.Nodes)
        {
            if (node == null || !node.IsEntry || node.Action == null)
                continue;

            entryCount++;
            GameplayIntentType trigger = node.Action.Trigger;
            if (trigger == GameplayIntentType.None)
            {
                errors.Add($"Entry '{node.NodeId}' 的 Action.Trigger 未配置。");
                continue;
            }

            // 每种 Trigger 只允许一个逻辑 Entry；Directional 变体由该 Entry 的 Resolver 折叠。
            if (!entryTriggers.Add(trigger))
            {
                errors.Add(
                    $"多个 Entry 使用相同 Trigger {trigger}：'{node.NodeId}'。请折叠为一个逻辑 Entry。");
            }
        }

        if (entryCount == 0)
            errors.Add("至少需要一个 Is Entry 节点作为 Locomotion 起手。");

        var triggerKeys = new HashSet<string>();
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

            bool slotFound = false;
            foreach (CancelWindowNotifyState window in from.Action.Timeline.CancelWindowStates)
            {
                if (window != null && window.CancelSlotId == edge.CancelSlotId)
                {
                    slotFound = true;
                    if (window.CancelType != CancelType.Combo)
                        errors.Add(
                            $"边 {edge.FromNodeId}/{edge.CancelSlotId} 引用了非 Combo 窗口。");
                    break;
                }
            }

            if (!slotFound)
                errors.Add($"节点 {edge.FromNodeId} 缺少 Cancel 槽 '{edge.CancelSlotId}'。");

            GameplayIntentType trigger = to.Action.Trigger;
            if (trigger == GameplayIntentType.None)
            {
                errors.Add($"目标 {edge.ToNodeId} 的 Action.Trigger 未配置。");
                continue;
            }

            string edgeKey = $"{edge.FromNodeId}|{edge.CancelSlotId}|{trigger}";
            if (!triggerKeys.Add(edgeKey))
                errors.Add($"同槽 Trigger 冲突: {edge.FromNodeId}/{edge.CancelSlotId} → {trigger}");
        }

        var validatedSharedRoutes = new List<ActionGraphSharedRoute>();
        foreach (ActionGraphSharedRoute route in graph.SharedRoutes)
        {
            if (route == null)
                continue;

            if (string.IsNullOrWhiteSpace(route.CancelSlotId))
                errors.Add("Shared Route 的 CancelSlotId 不能为空。");
            if (route.Intent == GameplayIntentType.None)
                errors.Add($"Shared Route '{route.CancelSlotId}' 的 Intent 不能为 None。");
            if (!graph.TryGetNode(route.ToNodeId, out ActionGraphNode target))
            {
                errors.Add($"Shared Route 目标节点无效: {route.ToNodeId}");
                continue;
            }

            if (target.Action.Trigger != route.Intent)
            {
                errors.Add(
                    $"Shared Route '{route.CancelSlotId}' Intent={route.Intent} 与目标 " +
                    $"'{route.ToNodeId}' Trigger={target.Action.Trigger} 不一致。");
            }

            foreach (ActionGraphSharedRoute existing in validatedSharedRoutes)
            {
                bool sourceOverlaps = existing.SourceTrigger == GameplayIntentType.None
                    || route.SourceTrigger == GameplayIntentType.None
                    || existing.SourceTrigger == route.SourceTrigger;
                if (sourceOverlaps
                    && existing.CancelSlotId == route.CancelSlotId
                    && existing.Intent == route.Intent)
                {
                    errors.Add(
                        $"Shared Route 冲突: {route.SourceTrigger}/{route.CancelSlotId}/{route.Intent}");
                    break;
                }
            }

            validatedSharedRoutes.Add(route);

            bool matchingComboSlotFound = false;
            foreach (ActionGraphNode node in graph.Nodes)
            {
                if (node?.Action == null
                    || (route.SourceTrigger != GameplayIntentType.None
                        && node.Action.Trigger != route.SourceTrigger))
                {
                    continue;
                }

                foreach (CancelWindowNotifyState window in node.Action.Timeline.CancelWindowStates)
                {
                    if (window != null
                        && window.CancelSlotId == route.CancelSlotId
                        && window.CancelType == CancelType.Combo)
                    {
                        matchingComboSlotFound = true;
                        break;
                    }
                }

                if (matchingComboSlotFound)
                    break;
            }

            if (!matchingComboSlotFound)
            {
                errors.Add(
                    $"Shared Route '{route.CancelSlotId}' 找不到匹配来源的 Combo 窗口。");
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
                    || route.CancelSlotId != edge.CancelSlotId
                    || route.Intent != to.Action.Trigger
                    || route.ToNodeId != edge.ToNodeId)
                {
                    continue;
                }

                if (route.SourceTrigger == GameplayIntentType.None
                    || route.SourceTrigger == from.Action.Trigger)
                {
                    errors.Add(
                        $"冗余显式边: {edge.FromNodeId}/{edge.CancelSlotId} → {edge.ToNodeId} " +
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
}
