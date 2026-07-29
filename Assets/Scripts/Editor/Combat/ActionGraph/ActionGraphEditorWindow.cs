using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>ActionGraph 可视化编辑器：双通道节点连线与可直接编辑的顺序组。</summary>
public sealed class ActionGraphEditorWindow : EditorWindow
{
    ActionGraph _graph;
    ActionGraphView _graphView;

    /// <summary>打开指定 Graph 的编辑窗口。</summary>
    public static void Open(ActionGraph graph)
    {
        ActionGraphEditorWindow window = GetWindow<ActionGraphEditorWindow>("Action Graph");
        window._graph = graph;
        window.titleContent = new GUIContent(graph != null ? $"Action Graph — {graph.name}" : "Action Graph");
        window.RebuildView();
    }

    [MenuItem("ACT/Combat/Action Graph Editor")]
    static void OpenMenu()
    {
        ActionGraph selected = Selection.activeObject as ActionGraph;
        Open(selected);
    }

    void OnEnable() => RebuildView();

    void OnSelectionChange()
    {
        if (Selection.activeObject is ActionGraph graph && graph != _graph)
        {
            _graph = graph;
            titleContent = new GUIContent($"Action Graph — {graph.name}");
            RebuildView();
        }
    }

    void RebuildView()
    {
        rootVisualElement.Clear();
        if (_graph == null)
        {
            rootVisualElement.Add(new Label("选中或打开一个 ActionGraph 资产。"));
            return;
        }

        var toolbar = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                paddingLeft = 4,
                paddingRight = 4,
                paddingTop = 2,
                paddingBottom = 2,
            },
        };
        var saveButton = new Button(SaveFromView) { text = "Save" };
        var validateButton = new Button(() => ActionGraphInspector.ValidateGraph(_graph)) { text = "Validate" };
        var reloadButton = new Button(RebuildView) { text = "Reload" };
        var mergeButton = new Button(() =>
        {
            if (_graphView != null && _graphView.MergeSelectedNodes())
                RebuildView();
        })
        {
            text = "Merge Sequence",
            tooltip = "将选中的 Action 节点按画布纵向顺序合并为自动 Cancel 序列。",
        };
        toolbar.Add(saveButton);
        toolbar.Add(validateButton);
        toolbar.Add(reloadButton);
        toolbar.Add(mergeButton);
        toolbar.Add(new Label($"  {_graph.name}") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginLeft = 8 } });
        // 隐式关系只显示摘要，不在 GraphView 复制成视觉连线。
        toolbar.Add(new Label(
            $"  显式边 {_graph.Edges.Count} · 隐式共享路由 {_graph.SharedRoutes.Count} · Recovery→Entry")
        {
            tooltip = "画布只显示独特拓扑；共享路由与 Recovery Phase Entry 不画重复连线。",
            style = { unityTextAlign = TextAnchor.MiddleLeft, marginLeft = 12 },
        });
        rootVisualElement.Add(toolbar);

        _graphView = new ActionGraphView(_graph);
        var viewHost = new VisualElement { style = { flexGrow = 1 } };
        viewHost.Add(_graphView);
        rootVisualElement.Add(viewHost);
        _graphView.LoadFromAsset();
    }

    void SaveFromView()
    {
        if (_graphView == null || _graph == null)
            return;

        _graphView.PersistViewToAsset();
        AssetDatabase.SaveAssets();
        Debug.Log($"[ActionGraph] 已保存 '{_graph.name}'。", _graph);
    }
}

/// <summary>GraphView 画布：节点/顺序组输出固定为 Normal 与 Perfect CancelWindow。</summary>
sealed class ActionGraphView : GraphView
{
    readonly ActionGraph _graph;
    readonly Dictionary<string, ActionGraphNodeView> _nodeViews = new();
    readonly Dictionary<string, ActionGraphGroupView> _groupViews = new();
    readonly Dictionary<string, string> _nodeToGroupId = new();
    bool _isLoading;

    /// <summary>创建可直接编辑节点策略的画布。</summary>
    public ActionGraphView(ActionGraph graph)
    {
        _graph = graph;
        style.flexGrow = 1;
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
        Insert(0, new GridBackground());

        // 删除节点时同步字典并写回资产，避免 Save/Reload 把已删节点复活。
        graphViewChanged = OnGraphViewChanged;

        RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
        RegisterCallback<DragPerformEvent>(OnDragPerform);
    }

    /// <summary>节点/边从画布移除时更新 _nodeViews，并延迟写回 SO。</summary>
    GraphViewChange OnGraphViewChanged(GraphViewChange change)
    {
        // LoadFromAsset 会批量移除旧视图；这不是用户删除，不能反向覆盖资产布局。
        if (_isLoading)
            return change;

        if (change.elementsToRemove == null || change.elementsToRemove.Count == 0)
            return change;

        bool removedNode = false;
        foreach (GraphElement element in change.elementsToRemove)
        {
            if (element is not ActionGraphNodeView nodeView)
                continue;

            _nodeViews.Remove(nodeView.NodeId);
            removedNode = true;
        }

        if (removedNode)
        {
            // 等 GraphView 真正删掉元素后再序列化，边列表才与画布一致。
            EditorApplication.delayCall += PersistViewToAsset;
        }

        return change;
    }

    /// <summary>将当前画布状态写入 ActionGraph 并标脏。</summary>
    public void PersistViewToAsset()
    {
        if (_graph == null)
            return;

        WriteToAsset();
        EditorUtility.SetDirty(_graph);
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        var compatible = new List<Port>();
        ports.ForEach(port =>
        {
            if (startPort == port)
                return;
            if (startPort.direction == port.direction)
                return;

            // 允许 Cancel 输出连回本节点 In（自环：同招再派生/重开）。
            if (startPort.node == port.node)
            {
                if (startPort.node is ActionGraphNodeView nodeView
                    && startPort.direction == Direction.Output
                    && port == nodeView.InputPort)
                {
                    compatible.Add(port);
                }

                return;
            }

            compatible.Add(port);
        });
        return compatible;
    }

    /// <summary>将选中普通节点按画布纵坐标排序后合并为自动 Cancel 序列。</summary>
    public bool MergeSelectedNodes()
    {
        List<ActionGraphNodeView> selectedNodes = selection
            .OfType<ActionGraphNodeView>()
            .OrderBy(view => view.GetPosition().y)
            .ThenBy(view => view.GetPosition().x)
            .ToList();
        if (selectedNodes.Count < 2)
        {
            EditorUtility.DisplayDialog("Merge Sequence", "请至少选择两个未分组 Action 节点。", "OK");
            return false;
        }

        PersistViewToAsset();
        string groupId = MakeUniqueGroupId("Sequence");
        Vector2 center = Vector2.zero;
        for (int i = 0; i < selectedNodes.Count; i++)
            center += selectedNodes[i].GetPosition().position;
        center /= selectedNodes.Count;

        var so = new SerializedObject(_graph);
        SerializedProperty groups = so.FindProperty("nodeGroups");
        int groupIndex = groups.arraySize;
        groups.arraySize++;
        SerializedProperty group = groups.GetArrayElementAtIndex(groupIndex);
        group.FindPropertyRelative("groupId").stringValue = groupId;
        group.FindPropertyRelative("displayName").stringValue = groupId;
        group.FindPropertyRelative("editorPosition").vector2Value = center;
        SerializedProperty children = group.FindPropertyRelative("childNodeIds");
        children.arraySize = selectedNodes.Count;
        for (int i = 0; i < selectedNodes.Count; i++)
            children.GetArrayElementAtIndex(i).stringValue = selectedNodes[i].NodeId;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(_graph);
        RebuildGeneratedSequenceEdges();
        return true;
    }

    /// <summary>删除组元数据；子节点与已生成的具体边保留。</summary>
    void Ungroup(string groupId)
    {
        PersistViewToAsset();
        MutateGroup(groupId, (groups, index) => groups.DeleteArrayElementAtIndex(index));
        EditorApplication.delayCall += LoadFromAsset;
    }

    /// <summary>调整组内 Action 行顺序；下次保存会重建相邻普通 Cancel 边。</summary>
    void MoveGroupChild(string groupId, int index, int delta)
    {
        PersistViewToAsset();
        MutateGroup(groupId, (groups, groupIndex) =>
        {
            SerializedProperty children = groups
                .GetArrayElementAtIndex(groupIndex)
                .FindPropertyRelative("childNodeIds");
            int target = Mathf.Clamp(index + delta, 0, children.arraySize - 1);
            if (target != index)
                children.MoveArrayElement(index, target);
        });
        RebuildGeneratedSequenceEdges();
        EditorApplication.delayCall += LoadFromAsset;
    }

    /// <summary>把拖入的 ActionDefinition 创建为新图节点并追加到指定顺序组。</summary>
    void AddActionToGroup(string groupId, ActionDefinition action)
    {
        if (action == null)
            return;

        PersistViewToAsset();
        var so = new SerializedObject(_graph);
        SerializedProperty nodes = so.FindProperty("nodes");
        int nodeIndex = nodes.arraySize;
        nodes.arraySize++;
        SerializedProperty node = nodes.GetArrayElementAtIndex(nodeIndex);
        string nodeId = MakeUniqueId(action.name);
        node.FindPropertyRelative("nodeId").stringValue = nodeId;
        node.FindPropertyRelative("action").objectReferenceValue = action;
        node.FindPropertyRelative("intent").enumValueIndex = (int)GameplayIntentType.None;
        node.FindPropertyRelative("isEntry").boolValue = false;
        node.FindPropertyRelative("variantResolver").objectReferenceValue = null;
        ResetNodePolicy(node);
        Vector2 childPosition = _groupViews.TryGetValue(groupId, out ActionGraphGroupView groupView)
            ? groupView.GetPosition().position + new Vector2(32f, 32f * groupView.ChildNodeIds.Count)
            : new Vector2(80f, 80f);
        node.FindPropertyRelative("editorPosition").vector2Value = childPosition;

        SerializedProperty groups = so.FindProperty("nodeGroups");
        for (int i = 0; i < groups.arraySize; i++)
        {
            SerializedProperty group = groups.GetArrayElementAtIndex(i);
            if (group.FindPropertyRelative("groupId").stringValue != groupId)
                continue;

            SerializedProperty children = group.FindPropertyRelative("childNodeIds");
            int childIndex = children.arraySize;
            children.arraySize++;
            children.GetArrayElementAtIndex(childIndex).stringValue = nodeId;
            break;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(_graph);
        RebuildGeneratedSequenceEdges();
        EditorApplication.delayCall += LoadFromAsset;
    }

    /// <summary>按 groupId 修改一个 Serialized 节点组并标脏。</summary>
    void MutateGroup(
        string groupId,
        System.Action<SerializedProperty, int> mutation)
    {
        var so = new SerializedObject(_graph);
        SerializedProperty groups = so.FindProperty("nodeGroups");
        for (int i = 0; i < groups.arraySize; i++)
        {
            if (groups.GetArrayElementAtIndex(i).FindPropertyRelative("groupId").stringValue != groupId)
                continue;

            mutation(groups, i);
            break;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(_graph);
    }

    /// <summary>
    /// 直接按资产中的最新组顺序重建内部普通 Cancel 边。
    /// 不读取刚重载的 GraphView 布局，避免 UI 尚未布局时以原点覆盖持久化坐标。
    /// </summary>
    void RebuildGeneratedSequenceEdges()
    {
        var nodeToGroup = new Dictionary<string, string>();
        foreach (ActionGraphNodeGroup group in _graph.NodeGroups)
        {
            if (group == null)
                continue;

            foreach (string childNodeId in group.ChildNodeIds)
                nodeToGroup[childNodeId] = group.GroupId;
        }

        var edgeList = new List<(string from, CancelWindowType route, string to)>();
        var seenEdges = new HashSet<string>();
        foreach (ActionGraphEdge edge in _graph.Edges)
        {
            if (edge == null)
                continue;

            bool isInternalSequenceEdge =
                edge.RouteKind == CancelWindowType.Normal
                && nodeToGroup.TryGetValue(edge.FromNodeId, out string fromGroup)
                && nodeToGroup.TryGetValue(edge.ToNodeId, out string toGroup)
                && fromGroup == toGroup;
            if (!isInternalSequenceEdge)
            {
                AddConcreteEdge(
                    edgeList,
                    seenEdges,
                    edge.FromNodeId,
                    edge.RouteKind,
                    edge.ToNodeId);
            }
        }

        foreach (ActionGraphNodeGroup group in _graph.NodeGroups)
        {
            if (group == null)
                continue;

            for (int i = 0; i < group.ChildNodeIds.Count - 1; i++)
            {
                AddConcreteEdge(
                    edgeList,
                    seenEdges,
                    group.ChildNodeIds[i],
                    CancelWindowType.Normal,
                    group.ChildNodeIds[i + 1]);
            }
        }

        var so = new SerializedObject(_graph);
        SerializedProperty edges = so.FindProperty("edges");
        edges.arraySize = edgeList.Count;
        for (int i = 0; i < edgeList.Count; i++)
        {
            SerializedProperty edge = edges.GetArrayElementAtIndex(i);
            edge.FindPropertyRelative("fromNodeId").stringValue = edgeList[i].from;
            edge.FindPropertyRelative("routeKind").enumValueIndex = (int)edgeList[i].route;
            edge.FindPropertyRelative("toNodeId").stringValue = edgeList[i].to;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(_graph);
    }

    /// <summary>从资产重建视图。</summary>
    public void LoadFromAsset()
    {
        _isLoading = true;
        try
        {
            DeleteElements(graphElements.ToList());
            _nodeViews.Clear();
            _groupViews.Clear();
            _nodeToGroupId.Clear();

            foreach (ActionGraphNodeGroup group in _graph.NodeGroups)
            {
                if (group == null || string.IsNullOrEmpty(group.GroupId))
                    continue;

                AddGroupView(group);
                foreach (string childNodeId in group.ChildNodeIds)
                    _nodeToGroupId[childNodeId] = group.GroupId;
            }

            foreach (ActionGraphNode node in _graph.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.NodeId))
                    continue;
                if (_nodeToGroupId.ContainsKey(node.NodeId))
                    continue;
                AddNodeView(node);
            }

            var visualEdges = new HashSet<string>();
            foreach (ActionGraphEdge edge in _graph.Edges)
            {
                if (edge == null)
                    continue;

                if (IsGeneratedSequenceEdge(edge))
                    continue;

                if (!TryResolveVisualOutput(edge, out Port output, out string sourceKey))
                    continue;
                if (!TryResolveVisualInput(edge.ToNodeId, out Port input, out string targetKey))
                    continue;

                if (!visualEdges.Add($"{sourceKey}|{targetKey}"))
                    continue;

                Edge graphEdge = output.ConnectTo(input);
                AddElement(graphEdge);
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>把当前视图写回 ActionGraph；以画布上仍存在的节点为准，避免字典残留。</summary>
    public void WriteToAsset()
    {
        SyncNodeViewsFromCanvas();

        var so = new SerializedObject(_graph);
        SerializedProperty nodesProp = so.FindProperty("nodes");
        SerializedProperty edgesProp = so.FindProperty("edges");
        SerializedProperty groupsProp = so.FindProperty("nodeGroups");

        // SerializedProperty 写入会原地修改托管数组对象，必须在重排数组前复制全部值。
        var existingNodes = _graph.Nodes
            .Where(node => node != null && !string.IsNullOrEmpty(node.NodeId))
            .ToDictionary(
                node => node.NodeId,
                node => new NodeSnapshot(node));
        var retainedNodeIds = new HashSet<string>(_nodeViews.Keys);
        foreach (ActionGraphGroupView group in _groupViews.Values)
        {
            foreach (string childNodeId in group.ChildNodeIds)
                retainedNodeIds.Add(childNodeId);
        }

        List<string> orderedNodeIds = retainedNodeIds.OrderBy(id => id).ToList();
        nodesProp.arraySize = orderedNodeIds.Count;
        for (int i = 0; i < orderedNodeIds.Count; i++)
        {
            string nodeId = orderedNodeIds[i];
            SerializedProperty node = nodesProp.GetArrayElementAtIndex(i);
            if (_nodeViews.TryGetValue(nodeId, out ActionGraphNodeView view))
            {
                WriteNodeView(node, view);
                if (existingNodes.TryGetValue(nodeId, out NodeSnapshot existing))
                    WriteNodePolicy(node, existing);
                else
                    ResetNodePolicy(node);
            }
            else if (existingNodes.TryGetValue(nodeId, out NodeSnapshot existing))
            {
                WriteExistingNode(node, existing);
                WriteNodePolicy(node, existing);
            }
        }

        List<ActionGraphGroupView> orderedGroups = _groupViews.Values
            .OrderBy(group => group.GroupId)
            .ToList();
        groupsProp.arraySize = orderedGroups.Count;
        for (int i = 0; i < orderedGroups.Count; i++)
            WriteGroupView(groupsProp.GetArrayElementAtIndex(i), orderedGroups[i]);

        var edgeList = new List<(string from, CancelWindowType route, string to)>();
        var seenEdges = new HashSet<string>();

        // 组顺序是普通 Cancel 链的唯一配置源。
        foreach (ActionGraphGroupView group in orderedGroups)
        {
            for (int i = 0; i < group.ChildNodeIds.Count - 1; i++)
            {
                AddConcreteEdge(
                    edgeList,
                    seenEdges,
                    group.ChildNodeIds[i],
                    CancelWindowType.Normal,
                    group.ChildNodeIds[i + 1]);
            }
        }

        edges.ForEach(edge =>
        {
            if (edge?.output == null || edge.input == null)
                return;

            string targetNodeId = edge.input.node switch
            {
                ActionGraphNodeView targetNode => targetNode.NodeId,
                ActionGraphGroupView targetGroup => targetGroup.GetNodeIdForInput(edge.input),
                _ => null,
            };
            if (string.IsNullOrEmpty(targetNodeId))
                return;

            if (edge.output.node is ActionGraphNodeView fromNode)
            {
                CancelWindowType? nodeRoute = fromNode.GetRouteForPort(edge.output);
                if (nodeRoute.HasValue)
                {
                    AddConcreteEdge(
                        edgeList,
                        seenEdges,
                        fromNode.NodeId,
                        nodeRoute.Value,
                        targetNodeId);
                }

                return;
            }

            if (edge.output.node is not ActionGraphGroupView fromGroup)
                return;

            CancelWindowType? groupRoute = fromGroup.GetRouteForPort(edge.output);
            if (!groupRoute.HasValue)
                return;

            var sources = new List<string>();
            fromGroup.CollectExternalSourceNodeIds(groupRoute.Value, sources);
            for (int i = 0; i < sources.Count; i++)
            {
                AddConcreteEdge(
                    edgeList,
                    seenEdges,
                    sources[i],
                    groupRoute.Value,
                    targetNodeId);
            }
        });

        edgesProp.arraySize = edgeList.Count;
        for (int i = 0; i < edgeList.Count; i++)
        {
            SerializedProperty edge = edgesProp.GetArrayElementAtIndex(i);
            edge.FindPropertyRelative("fromNodeId").stringValue = edgeList[i].from;
            edge.FindPropertyRelative("routeKind").enumValueIndex = (int)edgeList[i].route;
            edge.FindPropertyRelative("toNodeId").stringValue = edgeList[i].to;
        }

        so.ApplyModifiedProperties();
    }

    static void WriteNodeView(SerializedProperty node, ActionGraphNodeView view)
    {
        node.FindPropertyRelative("nodeId").stringValue = view.NodeId;
        node.FindPropertyRelative("action").objectReferenceValue = view.Action;
        node.FindPropertyRelative("intent").enumValueIndex = (int)view.Intent;
        node.FindPropertyRelative("isEntry").boolValue = view.IsEntry;
        node.FindPropertyRelative("variantResolver").objectReferenceValue = view.VariantResolver;
        node.FindPropertyRelative("editorPosition").vector2Value = view.GetPosition().position;
    }

    static void WriteExistingNode(SerializedProperty node, NodeSnapshot existing)
    {
        node.FindPropertyRelative("nodeId").stringValue = existing.NodeId;
        node.FindPropertyRelative("action").objectReferenceValue = existing.Action;
        node.FindPropertyRelative("intent").enumValueIndex = (int)existing.Intent;
        node.FindPropertyRelative("isEntry").boolValue = existing.IsEntry;
        node.FindPropertyRelative("variantResolver").objectReferenceValue = existing.VariantResolver;
        node.FindPropertyRelative("editorPosition").vector2Value = existing.EditorPosition;
    }

    /// <summary>恢复 GraphView 不直接编辑的节点策略，避免数组重排导致策略串到其它节点。</summary>
    static void WriteNodePolicy(SerializedProperty node, NodeSnapshot existing)
    {
        SerializedProperty behaviors = node.FindPropertyRelative("startBehaviors");
        behaviors.arraySize = existing.StartBehaviors.Length;
        for (int i = 0; i < existing.StartBehaviors.Length; i++)
            behaviors.GetArrayElementAtIndex(i).enumValueIndex = (int)existing.StartBehaviors[i];

        node.FindPropertyRelative("switchCombatModeTarget").enumValueIndex =
            (int)existing.SwitchCombatModeTarget;
        node.FindPropertyRelative("switchCombatModePolicy").enumValueIndex =
            (int)existing.SwitchCombatModePolicy;

        SerializedProperty targeting = node.FindPropertyRelative("targetLockSettings");
        targeting.FindPropertyRelative("enabled").boolValue = existing.TargetLockEnabled;
        targeting.FindPropertyRelative("lockRange").floatValue = existing.TargetLockRange;
        targeting.FindPropertyRelative("forwardConeAngle").floatValue =
            existing.TargetForwardConeAngle;
        targeting.FindPropertyRelative("policy").enumValueIndex =
            (int)existing.TargetSelectionPolicy;
        targeting.FindPropertyRelative("lockRotationSmoothTimeOverride").floatValue =
            existing.TargetLockRotationSmoothTimeOverride;

        SerializedProperty transitions = node.FindPropertyRelative("automaticTransitions");
        transitions.arraySize = existing.AutomaticTransitions.Length;
        for (int i = 0; i < existing.AutomaticTransitions.Length; i++)
        {
            TransitionSnapshot snapshot = existing.AutomaticTransitions[i];
            SerializedProperty transition = transitions.GetArrayElementAtIndex(i);
            transition.FindPropertyRelative("condition").enumValueIndex = (int)snapshot.Condition;
            transition.FindPropertyRelative("startFrame").intValue = snapshot.StartFrame;
            transition.FindPropertyRelative("targetNodeId").stringValue = snapshot.TargetNodeId;
            transition.FindPropertyRelative("priority").intValue = snapshot.Priority;
        }
    }

    /// <summary>新建节点清空可能由 SerializedProperty 数组槽继承的旧策略数据。</summary>
    public static void ResetNodePolicy(SerializedProperty node)
    {
        node.FindPropertyRelative("startBehaviors").arraySize = 0;
        node.FindPropertyRelative("switchCombatModeTarget").enumValueIndex =
            (int)CombatModeType.Default;
        node.FindPropertyRelative("switchCombatModePolicy").enumValueIndex =
            (int)CombatModeSwitchPolicy.Immediate;

        SerializedProperty targeting = node.FindPropertyRelative("targetLockSettings");
        targeting.FindPropertyRelative("enabled").boolValue = false;
        targeting.FindPropertyRelative("lockRange").floatValue = 8f;
        targeting.FindPropertyRelative("forwardConeAngle").floatValue = 120f;
        targeting.FindPropertyRelative("policy").enumValueIndex =
            (int)TargetSelectionPolicy.NearestDistance;
        targeting.FindPropertyRelative("lockRotationSmoothTimeOverride").floatValue = 0f;
        node.FindPropertyRelative("automaticTransitions").arraySize = 0;
    }

    /// <summary>在 SerializedProperty 重排前冻结节点数据，避免原地写入污染后续节点。</summary>
    readonly struct NodeSnapshot
    {
        public readonly string NodeId;
        public readonly ActionDefinition Action;
        public readonly GameplayIntentType Intent;
        public readonly bool IsEntry;
        public readonly ActionResolver VariantResolver;
        public readonly Vector2 EditorPosition;
        public readonly ActionGraphStartBehaviorType[] StartBehaviors;
        public readonly CombatModeType SwitchCombatModeTarget;
        public readonly CombatModeSwitchPolicy SwitchCombatModePolicy;
        public readonly bool TargetLockEnabled;
        public readonly float TargetLockRange;
        public readonly float TargetForwardConeAngle;
        public readonly TargetSelectionPolicy TargetSelectionPolicy;
        public readonly float TargetLockRotationSmoothTimeOverride;
        public readonly TransitionSnapshot[] AutomaticTransitions;

        public NodeSnapshot(ActionGraphNode node)
        {
            NodeId = node.NodeId;
            Action = node.Action;
            Intent = node.Intent;
            IsEntry = node.IsEntry;
            VariantResolver = node.VariantResolver;
            EditorPosition = node.EditorPosition;
            StartBehaviors = node.StartBehaviors.ToArray();
            SwitchCombatModeTarget = node.SwitchCombatModeTarget;
            SwitchCombatModePolicy = node.SwitchCombatModePolicy;
            TargetLockSettings targeting = node.TargetLockSettings;
            TargetLockEnabled = targeting.Enabled;
            TargetLockRange = targeting.LockRange;
            TargetForwardConeAngle = targeting.ForwardConeAngle;
            TargetSelectionPolicy = targeting.Policy;
            TargetLockRotationSmoothTimeOverride =
                targeting.LockRotationSmoothTimeOverride;
            AutomaticTransitions = node.AutomaticTransitions
                .Where(transition => transition != null)
                .Select(transition => new TransitionSnapshot(transition))
                .ToArray();
        }
    }

    /// <summary>冻结一条自动衔接规则，供 GraphView 重排节点数组后恢复。</summary>
    readonly struct TransitionSnapshot
    {
        public readonly ActionTransitionCondition Condition;
        public readonly int StartFrame;
        public readonly string TargetNodeId;
        public readonly int Priority;

        public TransitionSnapshot(ActionGraphTransition transition)
        {
            Condition = transition.Condition;
            StartFrame = transition.StartFrame;
            TargetNodeId = transition.TargetNodeId;
            Priority = transition.Priority;
        }
    }

    static void WriteGroupView(SerializedProperty group, ActionGraphGroupView view)
    {
        group.FindPropertyRelative("groupId").stringValue = view.GroupId;
        group.FindPropertyRelative("displayName").stringValue = view.DisplayName;
        group.FindPropertyRelative("editorPosition").vector2Value = view.GetPosition().position;
        SerializedProperty children = group.FindPropertyRelative("childNodeIds");
        children.arraySize = view.ChildNodeIds.Count;
        for (int i = 0; i < view.ChildNodeIds.Count; i++)
            children.GetArrayElementAtIndex(i).stringValue = view.ChildNodeIds[i];
    }

    static void AddConcreteEdge(
        List<(string from, CancelWindowType route, string to)> edges,
        HashSet<string> seen,
        string from,
        CancelWindowType route,
        string to)
    {
        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            return;

        string key = $"{from}|{route}|{to}";
        if (seen.Add(key))
            edges.Add((from, route, to));
    }

    /// <summary>以画布 nodes 为准重建字典，清掉已删除但仍留在字典里的幽灵节点。</summary>
    void SyncNodeViewsFromCanvas()
    {
        var live = new Dictionary<string, ActionGraphNodeView>();
        nodes.ForEach(node =>
        {
            if (node is ActionGraphNodeView view && !string.IsNullOrEmpty(view.NodeId))
                live[view.NodeId] = view;
        });
        _nodeViews.Clear();
        foreach (KeyValuePair<string, ActionGraphNodeView> pair in live)
            _nodeViews[pair.Key] = pair.Value;
    }

    /// <summary>用资产节点数据创建画布节点，并恢复 Intent、Entry、Resolver 与布局。</summary>
    void AddNodeView(ActionGraphNode data)
    {
        var view = new ActionGraphNodeView(
            data.NodeId,
            data.Action,
            data.Intent,
            data.IsEntry,
            data.VariantResolver,
            _graph);
        view.SetPosition(new Rect(data.EditorPosition, new Vector2(220, 140)));
        _nodeViews[data.NodeId] = view;
        AddElement(view);
    }

    /// <summary>创建顺序组；每行保留独立输入，组级按窗口类型聚合输出。</summary>
    void AddGroupView(ActionGraphNodeGroup data)
    {
        var children = new List<ActionGraphNode>();
        foreach (string childNodeId in data.ChildNodeIds)
        {
            if (_graph.TryGetNode(childNodeId, out ActionGraphNode child))
                children.Add(child);
        }

        if (children.Count == 0)
            return;

        var view = new ActionGraphGroupView(
            data.GroupId,
            data.DisplayName,
            children,
            () => Ungroup(data.GroupId),
            (index, delta) => MoveGroupChild(data.GroupId, index, delta),
            action => AddActionToGroup(data.GroupId, action),
            _graph);
        view.SetPosition(new Rect(data.EditorPosition, new Vector2(300, 190)));
        _groupViews[data.GroupId] = view;
        AddElement(view);
    }

    /// <summary>判断边是否是组顺序自动生成的相邻普通 Cancel，折叠视图不绘制。</summary>
    bool IsGeneratedSequenceEdge(ActionGraphEdge edge)
    {
        if (edge.RouteKind != CancelWindowType.Normal
            || !_nodeToGroupId.TryGetValue(edge.FromNodeId, out string fromGroup)
            || !_nodeToGroupId.TryGetValue(edge.ToNodeId, out string toGroup)
            || fromGroup != toGroup
            || !_groupViews.TryGetValue(fromGroup, out ActionGraphGroupView group))
        {
            return false;
        }

        return group.IsNextNode(edge.FromNodeId, edge.ToNodeId);
    }

    /// <summary>映射具体边起点到普通节点端口或顺序组的两个聚合出口。</summary>
    bool TryResolveVisualOutput(ActionGraphEdge edge, out Port output, out string sourceKey)
    {
        output = null;
        sourceKey = null;
        if (_nodeToGroupId.TryGetValue(edge.FromNodeId, out string groupId))
        {
            if (!_groupViews.TryGetValue(groupId, out ActionGraphGroupView group)
                || !group.AcceptsExternalSource(edge.FromNodeId, edge.RouteKind)
                || !group.TryGetCancelPort(edge.RouteKind, out output))
            {
                return false;
            }

            sourceKey = $"G:{groupId}:{edge.RouteKind}";
            return true;
        }

        if (!_nodeViews.TryGetValue(edge.FromNodeId, out ActionGraphNodeView node)
            || !node.TryGetCancelPort(edge.RouteKind, out output))
        {
            return false;
        }

        sourceKey = $"N:{edge.FromNodeId}:{edge.RouteKind}";
        return true;
    }

    /// <summary>映射目标到普通节点 In，或顺序组内对应 Action 行的独立 In。</summary>
    bool TryResolveVisualInput(string targetNodeId, out Port input, out string targetKey)
    {
        input = null;
        targetKey = null;
        if (_nodeToGroupId.TryGetValue(targetNodeId, out string groupId))
        {
            if (!_groupViews.TryGetValue(groupId, out ActionGraphGroupView group)
                || !group.TryGetInputPort(targetNodeId, out input))
            {
                return false;
            }

            targetKey = $"G:{groupId}:{targetNodeId}";
            return true;
        }

        if (!_nodeViews.TryGetValue(targetNodeId, out ActionGraphNodeView node))
            return false;

        input = node.InputPort;
        targetKey = $"N:{targetNodeId}";
        return true;
    }

    void OnDragUpdated(DragUpdatedEvent evt)
    {
        if (HasActionDefinitionDrag())
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
    }

    /// <summary>接收拖入的 ActionDefinition，并以空 Resolver 创建新节点后立即落盘。</summary>
    void OnDragPerform(DragPerformEvent evt)
    {
        if (!HasActionDefinitionDrag())
            return;

        DragAndDrop.AcceptDrag();
        Vector2 local = contentViewContainer.WorldToLocal(evt.mousePosition);
        foreach (Object obj in DragAndDrop.objectReferences)
        {
            if (obj is not ActionDefinition action)
                continue;

            string nodeId = MakeUniqueId(action.name);
            var view = new ActionGraphNodeView(
                nodeId,
                action,
                GameplayIntentType.None,
                entry: false,
                variantResolver: null,
                graph: _graph);
            view.SetPosition(new Rect(local, new Vector2(220, 140)));
            _nodeViews[nodeId] = view;
            AddElement(view);
            local += new Vector2(30, 30);
        }

        // 拖入后立即落盘，避免只改画布、Reload 丢节点或与 Inspector 打架。
        PersistViewToAsset();
    }

    static bool HasActionDefinitionDrag()
    {
        foreach (Object obj in DragAndDrop.objectReferences)
        {
            if (obj is ActionDefinition)
                return true;
        }

        return false;
    }

    string MakeUniqueId(string baseId)
    {
        if (string.IsNullOrEmpty(baseId))
            baseId = "Node";
        // 标题里的换行在部分 UI 会显示成 \，id 只用合法单行名。
        baseId = baseId.Replace('\n', '_').Replace('\\', '_').Trim();
        var usedIds = new HashSet<string>(
            _graph.Nodes
                .Where(node => node != null)
                .Select(node => node.NodeId));
        foreach (string visibleId in _nodeViews.Keys)
            usedIds.Add(visibleId);

        if (!usedIds.Contains(baseId))
            return baseId;

        int i = 2;
        while (usedIds.Contains(baseId + "_" + i))
            i++;
        return baseId + "_" + i;
    }

    /// <summary>生成不与现有顺序组冲突的 Id。</summary>
    string MakeUniqueGroupId(string baseId)
    {
        var ids = new HashSet<string>(
            _graph.NodeGroups
                .Where(group => group != null)
                .Select(group => group.GroupId));
        if (!ids.Contains(baseId))
            return baseId;

        int i = 2;
        while (ids.Contains(baseId + "_" + i))
            i++;
        return baseId + "_" + i;
    }
}

/// <summary>嵌入 Graph 节点内部的策略折叠区，直接写入对应 ActionGraphNode。</summary>
sealed class ActionGraphNodePolicyView : IMGUIContainer
{
    readonly ActionGraph _graph;
    readonly SerializedObject _serializedGraph;
    readonly string _nodeId;
    readonly bool _includeBasics;
    bool _expanded;
    bool _targetLockExpanded;
    bool _startBehaviorsExpanded;
    bool _automaticTransitionsExpanded;

    /// <summary>创建节点内联策略编辑器；顺序组子节点额外显示基础输入字段。</summary>
    public ActionGraphNodePolicyView(ActionGraph graph, string nodeId, bool includeBasics)
    {
        _graph = graph;
        _serializedGraph = graph != null ? new SerializedObject(graph) : null;
        _nodeId = nodeId;
        _includeBasics = includeBasics;
        style.minWidth = 280;
        style.marginTop = 3;
        onGUIHandler = DrawPolicy;
    }

    /// <summary>绘制并提交当前节点字段；NodeId 用于抵御节点数组重排。</summary>
    void DrawPolicy()
    {
        _expanded = EditorGUILayout.Foldout(
            _expanded,
            _includeBasics ? $"{_nodeId} Policy" : "Node Policy",
            true);
        if (!_expanded || _serializedGraph == null)
            return;

        // 复用同一个 SerializedObject，保留 TargetLock/数组等子属性的展开状态。
        _serializedGraph.Update();
        SerializedProperty node = FindNode(_serializedGraph, _nodeId);
        if (node == null)
        {
            EditorGUILayout.HelpBox("节点已不存在。", MessageType.Warning);
            return;
        }

        EditorGUI.BeginChangeCheck();
        if (_includeBasics)
        {
            EditorGUILayout.PropertyField(
                node.FindPropertyRelative("intent"),
                new GUIContent("Input Intent"));
            EditorGUILayout.PropertyField(
                node.FindPropertyRelative("isEntry"),
                new GUIContent("Is Entry"));
            EditorGUILayout.PropertyField(
                node.FindPropertyRelative("variantResolver"),
                new GUIContent("Variant Resolver"));
        }

        DrawTargetLock(node.FindPropertyRelative("targetLockSettings"));
        DrawStartBehaviors(node.FindPropertyRelative("startBehaviors"));
        EditorGUILayout.PropertyField(
            node.FindPropertyRelative("switchCombatModeTarget"),
            new GUIContent("Combat Mode Target"));
        EditorGUILayout.PropertyField(
            node.FindPropertyRelative("switchCombatModePolicy"),
            new GUIContent("Combat Mode Policy"));
        DrawAutomaticTransitions(node.FindPropertyRelative("automaticTransitions"));

        if (EditorGUI.EndChangeCheck())
        {
            _serializedGraph.ApplyModifiedProperties();
            EditorUtility.SetDirty(_graph);
        }
    }

    /// <summary>用视图自身保存折叠状态，避免 SerializedProperty 重建导致鼠标移动时收起。</summary>
    void DrawTargetLock(SerializedProperty targetLock)
    {
        _targetLockExpanded = EditorGUILayout.Foldout(
            _targetLockExpanded,
            "Target Lock",
            true);
        if (!_targetLockExpanded)
            return;

        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(targetLock.FindPropertyRelative("enabled"));
        EditorGUILayout.PropertyField(targetLock.FindPropertyRelative("lockRange"));
        EditorGUILayout.PropertyField(targetLock.FindPropertyRelative("forwardConeAngle"));
        EditorGUILayout.PropertyField(targetLock.FindPropertyRelative("policy"));
        EditorGUILayout.PropertyField(
            targetLock.FindPropertyRelative("lockRotationSmoothTimeOverride"));
        EditorGUI.indentLevel--;
    }

    /// <summary>直接绘制起手行为数组，不依赖 SerializedProperty 的临时展开标记。</summary>
    void DrawStartBehaviors(SerializedProperty behaviors)
    {
        _startBehaviorsExpanded = EditorGUILayout.Foldout(
            _startBehaviorsExpanded,
            "Start Behaviors",
            true);
        if (!_startBehaviorsExpanded)
            return;

        EditorGUI.indentLevel++;
        int size = Mathf.Max(0, EditorGUILayout.IntField("Size", behaviors.arraySize));
        if (size != behaviors.arraySize)
            behaviors.arraySize = size;
        for (int i = 0; i < behaviors.arraySize; i++)
        {
            EditorGUILayout.PropertyField(
                behaviors.GetArrayElementAtIndex(i),
                new GUIContent($"Element {i}"));
        }
        EditorGUI.indentLevel--;
    }

    /// <summary>直接绘制自动衔接数组及每条规则，确保所有层级保持展开。</summary>
    void DrawAutomaticTransitions(SerializedProperty transitions)
    {
        _automaticTransitionsExpanded = EditorGUILayout.Foldout(
            _automaticTransitionsExpanded,
            "Automatic Transitions",
            true);
        if (!_automaticTransitionsExpanded)
            return;

        EditorGUI.indentLevel++;
        int size = Mathf.Max(0, EditorGUILayout.IntField("Size", transitions.arraySize));
        if (size != transitions.arraySize)
            transitions.arraySize = size;
        for (int i = 0; i < transitions.arraySize; i++)
        {
            SerializedProperty transition = transitions.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField($"Transition {i}", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(transition.FindPropertyRelative("condition"));
                EditorGUILayout.PropertyField(transition.FindPropertyRelative("startFrame"));
                EditorGUILayout.PropertyField(transition.FindPropertyRelative("targetNodeId"));
                EditorGUILayout.PropertyField(transition.FindPropertyRelative("priority"));
            }
        }
        EditorGUI.indentLevel--;
    }

    /// <summary>按稳定 NodeId 查找节点，避免数组顺序改变后写入其它节点。</summary>
    static SerializedProperty FindNode(SerializedObject so, string nodeId)
    {
        SerializedProperty nodes = so.FindProperty("nodes");
        for (int i = 0; i < nodes.arraySize; i++)
        {
            SerializedProperty node = nodes.GetArrayElementAtIndex(i);
            if (node.FindPropertyRelative("nodeId").stringValue == nodeId)
                return node;
        }

        return null;
    }
}

/// <summary>单个 Action 节点视图：基础字段、策略入口与 Cancel 端口。</summary>
sealed class ActionGraphNodeView : Node
{
    readonly Dictionary<CancelWindowType, Port> _cancelPorts = new();
    readonly Dictionary<Port, CancelWindowType> _portToRoute = new();
    readonly UnityEngine.UIElements.Toggle _entryToggle;
    readonly EnumField _intentField;
    readonly UnityEditor.UIElements.ObjectField _variantResolverField;

    public string NodeId { get; }
    public ActionDefinition Action { get; }
    public GameplayIntentType Intent =>
        _intentField?.value is GameplayIntentType intent ? intent : GameplayIntentType.None;
    public bool IsEntry => _entryToggle != null && _entryToggle.value;
    public ActionResolver VariantResolver => _variantResolverField?.value as ActionResolver;
    public Port InputPort { get; private set; }

    /// <summary>创建节点视图，并恢复可直接在画布中编辑的 Entry、Intent 与 Resolver 配置。</summary>
    public ActionGraphNodeView(
        string nodeId,
        ActionDefinition action,
        GameplayIntentType intent,
        bool entry = false,
        ActionResolver variantResolver = null,
        ActionGraph graph = null)
    {
        NodeId = nodeId;
        Action = action;
        title = action != null ? $"{nodeId}  [{intent}]" : nodeId;

        _entryToggle = new UnityEngine.UIElements.Toggle("Entry") { value = entry };
        titleContainer.Add(_entryToggle);

        _intentField = new EnumField("Intent", intent)
        {
            tooltip = "进入该节点所匹配的设备无关玩法意图。",
        };
        extensionContainer.Add(_intentField);
        _intentField.RegisterValueChangedCallback(evt => UpdateTitle((GameplayIntentType)evt.newValue));

        _variantResolverField = new UnityEditor.UIElements.ObjectField("Variant Resolver")
        {
            objectType = typeof(ActionResolver),
            allowSceneObjects = false,
            value = variantResolver,
            tooltip = "进入该节点前执行的可选变体解析器，例如六向闪避 DirectionalActionResolver。",
        };
        extensionContainer.Add(_variantResolverField);
        extensionContainer.Add(new ActionGraphNodePolicyView(graph, NodeId, includeBasics: false));

        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        InputPort.portName = "In";
        inputContainer.Add(InputPort);

        if (action != null)
        {
            if (action.GetCancelWindow(CancelWindowType.Normal) != null)
                AddCancelPort(CancelWindowType.Normal, "NormalCancelWindow");
            if (action.GetCancelWindow(CancelWindowType.Perfect) != null)
                AddCancelPort(CancelWindowType.Perfect, "PerfectCancelWindow");
        }

        expanded = true;
        RefreshExpandedState();
        RefreshPorts();
    }

    /// <summary>让标题始终反映当前输入语义。</summary>
    void UpdateTitle(GameplayIntentType intent)
    {
        title = Action != null ? $"{NodeId}  [{intent}]" : NodeId;
    }

    /// <summary>查询普通或 Perfect Cancel 输出端口。</summary>
    public bool TryGetCancelPort(CancelWindowType route, out Port port) =>
        _cancelPorts.TryGetValue(route, out port);

    /// <summary>从输出端口反查普通或 Perfect 路由。</summary>
    public CancelWindowType? GetRouteForPort(Port port) =>
        _portToRoute.TryGetValue(port, out CancelWindowType route) ? route : null;

    /// <summary>创建一个固定语义的 Cancel 输出端口。</summary>
    void AddCancelPort(CancelWindowType route, string label)
    {
        Port port = InstantiatePort(
            Orientation.Horizontal,
            Direction.Output,
            Port.Capacity.Multi,
            typeof(bool));
        port.portName = label;
        outputContainer.Add(port);
        _cancelPorts[route] = port;
        _portToRoute[port] = route;
    }
}

/// <summary>
/// 顺序组视图：每行 Action 一个输入端口；普通 Cancel 自动进入下一行，
/// 组级 Normal / Perfect 输出分别聚合全部配置对应窗口类型的子节点。
/// </summary>
sealed class ActionGraphGroupView : Node
{
    readonly List<ActionGraphNode> _children;
    readonly List<string> _childNodeIds;
    readonly Dictionary<string, Port> _inputPorts = new();
    readonly Dictionary<Port, string> _portToNodeId = new();
    readonly Dictionary<CancelWindowType, Port> _outputPorts = new();
    readonly Dictionary<Port, CancelWindowType> _portToRoute = new();
    readonly System.Action<ActionDefinition> _addAction;

    public string GroupId { get; }
    public string DisplayName { get; }
    public IReadOnlyList<string> ChildNodeIds => _childNodeIds;

    /// <summary>创建可直接接收 ActionDefinition 的顺序组。</summary>
    public ActionGraphGroupView(
        string groupId,
        string displayName,
        List<ActionGraphNode> children,
        System.Action ungroup,
        System.Action<int, int> moveChild,
        System.Action<ActionDefinition> addAction,
        ActionGraph graph)
    {
        GroupId = groupId;
        DisplayName = string.IsNullOrEmpty(displayName) ? groupId : displayName;
        _children = children ?? new List<ActionGraphNode>();
        _childNodeIds = _children.Select(child => child.NodeId).ToList();
        _addAction = addAction;
        title = $"{DisplayName}  [Sequence]";
        capabilities &= ~Capabilities.Deletable;

        for (int i = 0; i < _children.Count; i++)
        {
            ActionGraphNode child = _children[i];
            Port input = InstantiatePort(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Multi,
                typeof(bool));
            input.portName = $"{i + 1}. {child.NodeId}";
            inputContainer.Add(input);
            _inputPorts[child.NodeId] = input;
            _portToNodeId[input] = child.NodeId;

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.Add(new Label(child.Action != null ? child.Action.name : child.NodeId)
            {
                style = { flexGrow = 1 },
            });
            int capturedIndex = i;
            row.Add(new Button(() => moveChild(capturedIndex, -1)) { text = "↑" });
            row.Add(new Button(() => moveChild(capturedIndex, 1)) { text = "↓" });
            extensionContainer.Add(row);
            extensionContainer.Add(
                new ActionGraphNodePolicyView(graph, child.NodeId, includeBasics: true));
        }

        if (_children.Any(
                child => child.Action?.GetCancelWindow(CancelWindowType.Normal) != null))
        {
            AddOutput(CancelWindowType.Normal, "NormalCancelWindow");
        }

        if (_children.Any(
                child => child.Action?.GetCancelWindow(CancelWindowType.Perfect) != null))
        {
            AddOutput(CancelWindowType.Perfect, "PerfectCancelWindow");
        }

        extensionContainer.Add(new Label("拖入 ActionDefinition 可追加到序列末尾"));
        extensionContainer.Add(new Button(ungroup) { text = "Ungroup" });
        RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
        RegisterCallback<DragPerformEvent>(OnDragPerform);

        expanded = true;
        RefreshExpandedState();
        RefreshPorts();
    }

    /// <summary>指定边是否应显示为组级外部出口。</summary>
    public bool AcceptsExternalSource(string nodeId, CancelWindowType route)
    {
        return _children.Any(
            child => child.NodeId == nodeId
                && child.Action?.GetCancelWindow(route) != null);
    }

    /// <summary>判断两个节点是否为顺序组内相邻项。</summary>
    public bool IsNextNode(string fromNodeId, string toNodeId)
    {
        int index = _childNodeIds.IndexOf(fromNodeId);
        return index >= 0
            && index + 1 < _childNodeIds.Count
            && _childNodeIds[index + 1] == toNodeId;
    }

    public bool TryGetInputPort(string nodeId, out Port port) =>
        _inputPorts.TryGetValue(nodeId, out port);

    public string GetNodeIdForInput(Port port) =>
        _portToNodeId.TryGetValue(port, out string nodeId) ? nodeId : null;

    public bool TryGetCancelPort(CancelWindowType route, out Port port) =>
        _outputPorts.TryGetValue(route, out port);

    public CancelWindowType? GetRouteForPort(Port port) =>
        _portToRoute.TryGetValue(port, out CancelWindowType route) ? route : null;

    /// <summary>展开组级出口：覆盖全部配置指定窗口类型的子节点。</summary>
    public void CollectExternalSourceNodeIds(
        CancelWindowType route,
        List<string> results)
    {
        results.Clear();
        foreach (ActionGraphNode child in _children)
        {
            if (child.Action?.GetCancelWindow(route) != null)
                results.Add(child.NodeId);
        }
    }

    void AddOutput(CancelWindowType route, string label)
    {
        Port output = InstantiatePort(
            Orientation.Horizontal,
            Direction.Output,
            Port.Capacity.Multi,
            typeof(bool));
        output.portName = label;
        outputContainer.Add(output);
        _outputPorts[route] = output;
        _portToRoute[output] = route;
    }

    void OnDragUpdated(DragUpdatedEvent evt)
    {
        if (DragAndDrop.objectReferences.Any(obj => obj is ActionDefinition))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.StopPropagation();
        }
    }

    void OnDragPerform(DragPerformEvent evt)
    {
        ActionDefinition action = DragAndDrop.objectReferences.OfType<ActionDefinition>().FirstOrDefault();
        if (action == null)
            return;

        DragAndDrop.AcceptDrag();
        _addAction?.Invoke(action);
        evt.StopPropagation();
    }
}
