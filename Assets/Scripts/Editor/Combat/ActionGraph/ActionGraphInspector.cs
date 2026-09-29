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
            "Is Entry 表示允许起手。玩家按 Intent 选招；行为树按 NodeId 选招，可有多个 Intent=None 的 Entry。\n" +
            "输入 Cancel 连线目标需要 Intent，来源需有对应窗口；终结动作可不配 Cancel。同 Intent 重叠时 Perfect 优先。\n" +
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

                EditorGUILayout.PropertyField(intent, new GUIContent("Intent（输入选招）", "行为树按 NodeId 起手可留 None；多个 None Entry 合法。输入 Cancel 连线目标仍需 Intent。"));
                EditorGUILayout.PropertyField(variant, new GUIContent("Variant Resolver"));
                EditorGUILayout.PropertyField(
                    node.FindPropertyRelative("targetLockSettings"),
                    new GUIContent("Target Lock"),
                    includeChildren: true);
                EditorGUILayout.PropertyField(
                    node.FindPropertyRelative("startBehaviors"),
                    new GUIContent("Start Behaviors"),
                    includeChildren: true);
                CharacterAuthoringFields.DrawModeSwitch(node);
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

    /// <summary>显示 Domain 共用校验结果；所有入口使用同一套规则。</summary>
    public static void ValidateGraph(ActionGraph graph)
    {
        if (graph != null && ActionGraphValidator.ValidateAndLog(graph))
            Debug.Log($"[ActionGraph] '{graph.name}' 无阻断问题。", graph);
    }
}
