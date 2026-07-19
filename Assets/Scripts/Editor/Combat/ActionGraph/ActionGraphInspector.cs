using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>ActionGraph 自定义 Inspector：多 Entry、节点/边、Trigger 预览与校验。</summary>
[CustomEditor(typeof(ActionGraph))]
public class ActionGraphInspector : Editor
{
    SerializedProperty _nodes;
    SerializedProperty _edges;

    void OnEnable()
    {
        _nodes = serializedObject.FindProperty("nodes");
        _edges = serializedObject.FindProperty("edges");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Action Graph", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "勾选节点 Is Entry 作为 Locomotion 起手；同一图可有多个 Entry（Attack / Dodge 等靠 Action.Trigger 区分）。\n" +
            "边只绑定 Cancel 槽 → 目标节点 In；可连回自身 In（同招再派生/重开）。\n" +
            "输入不再在 ActionSet 重复配置。方向闪避：Entry 上挂 Variant Resolver（Directional）。",
            MessageType.Info);

        DrawNodes();
        EditorGUILayout.Space(6);
        DrawEdges();
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

            // 同 Trigger 的多个 Entry 仅在挂了 VariantResolver（方向分派）时允许。
            if (!entryTriggers.Add(trigger) && node.VariantResolver == null)
            {
                errors.Add(
                    $"多个 Entry 使用相同 Trigger {trigger} 且无 Variant Resolver：'{node.NodeId}'。");
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

        if (errors.Count == 0)
            Debug.Log($"[ActionGraph] '{graph.name}' 校验通过（Entry × {entryCount}）。", graph);
        else
        {
            foreach (string error in errors)
                Debug.LogError($"[ActionGraph] {error}", graph);
        }
    }
}
