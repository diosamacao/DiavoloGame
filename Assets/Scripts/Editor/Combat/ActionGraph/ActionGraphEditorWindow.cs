using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>ActionGraph 可视化编辑器：拖入 ActionDefinition、从 Cancel 槽端口连到目标节点。</summary>
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
        toolbar.Add(saveButton);
        toolbar.Add(validateButton);
        toolbar.Add(reloadButton);
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
        _graphView.StretchToParentSize();
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

/// <summary>GraphView 画布：节点 = Action，输出端口 = Cancel 槽。</summary>
sealed class ActionGraphView : GraphView
{
    readonly ActionGraph _graph;
    readonly Dictionary<string, ActionGraphNodeView> _nodeViews = new();

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

    /// <summary>从资产重建视图。</summary>
    public void LoadFromAsset()
    {
        DeleteElements(graphElements.ToList());
        _nodeViews.Clear();

        foreach (ActionGraphNode node in _graph.Nodes)
        {
            if (node == null || string.IsNullOrEmpty(node.NodeId))
                continue;
            AddNodeView(node);
        }

        foreach (ActionGraphEdge edge in _graph.Edges)
        {
            if (edge == null)
                continue;
            if (!_nodeViews.TryGetValue(edge.FromNodeId, out ActionGraphNodeView fromView))
                continue;
            if (!_nodeViews.TryGetValue(edge.ToNodeId, out ActionGraphNodeView toView))
                continue;
            if (!fromView.TryGetCancelPort(edge.CancelSlotId, out Port output))
                continue;

            Port input = toView.InputPort;
            Edge graphEdge = output.ConnectTo(input);
            AddElement(graphEdge);
        }
    }

    /// <summary>把当前视图写回 ActionGraph；以画布上仍存在的节点为准，避免字典残留。</summary>
    public void WriteToAsset()
    {
        SyncNodeViewsFromCanvas();

        var so = new SerializedObject(_graph);
        SerializedProperty nodesProp = so.FindProperty("nodes");
        SerializedProperty edgesProp = so.FindProperty("edges");

        // 用稳定顺序写出，避免 Dictionary 枚举顺序抖动。
        var orderedNodes = new List<ActionGraphNodeView>(_nodeViews.Values);
        orderedNodes.Sort((a, b) => string.CompareOrdinal(a.NodeId, b.NodeId));

        nodesProp.arraySize = orderedNodes.Count;
        for (int i = 0; i < orderedNodes.Count; i++)
        {
            ActionGraphNodeView view = orderedNodes[i];
            SerializedProperty node = nodesProp.GetArrayElementAtIndex(i);
            node.FindPropertyRelative("nodeId").stringValue = view.NodeId;
            node.FindPropertyRelative("action").objectReferenceValue = view.Action;
            node.FindPropertyRelative("isEntry").boolValue = view.IsEntry;
            node.FindPropertyRelative("variantResolver").objectReferenceValue = view.VariantResolver;
            Rect layout = view.GetPosition();
            node.FindPropertyRelative("editorPosition").vector2Value = new Vector2(layout.x, layout.y);
        }

        var edgeList = new List<(string from, string slot, string to)>();
        var seenEdges = new HashSet<string>();
        edges.ForEach(edge =>
        {
            if (edge?.output?.node is not ActionGraphNodeView fromView)
                return;
            if (edge.input?.node is not ActionGraphNodeView toView)
                return;
            if (!_nodeViews.ContainsKey(fromView.NodeId) || !_nodeViews.ContainsKey(toView.NodeId))
                return;

            string slot = fromView.GetSlotIdForPort(edge.output);
            if (string.IsNullOrEmpty(slot))
                return;

            string key = $"{fromView.NodeId}|{slot}|{toView.NodeId}";
            if (!seenEdges.Add(key))
                return;

            edgeList.Add((fromView.NodeId, slot, toView.NodeId));
        });

        edgesProp.arraySize = edgeList.Count;
        for (int i = 0; i < edgeList.Count; i++)
        {
            SerializedProperty edge = edgesProp.GetArrayElementAtIndex(i);
            edge.FindPropertyRelative("fromNodeId").stringValue = edgeList[i].from;
            edge.FindPropertyRelative("cancelSlotId").stringValue = edgeList[i].slot;
            edge.FindPropertyRelative("toNodeId").stringValue = edgeList[i].to;
        }

        so.ApplyModifiedProperties();
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

    /// <summary>用资产节点数据创建画布节点，并恢复 Entry、Resolver 与布局。</summary>
    void AddNodeView(ActionGraphNode data)
    {
        var view = new ActionGraphNodeView(data.NodeId, data.Action, data.IsEntry, data.VariantResolver);
        view.SetPosition(new Rect(data.EditorPosition, new Vector2(220, 140)));
        _nodeViews[data.NodeId] = view;
        AddElement(view);
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
            var view = new ActionGraphNodeView(nodeId, action, entry: false, variantResolver: null);
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
        if (!_nodeViews.ContainsKey(baseId))
            return baseId;

        int i = 2;
        while (_nodeViews.ContainsKey(baseId + "_" + i))
            i++;
        return baseId + "_" + i;
    }
}
/// <summary>单个 Action 节点视图：Entry、变体 Resolver、输入端口与 Cancel 输出端口。</summary>
sealed class ActionGraphNodeView : Node
{
    readonly Dictionary<string, Port> _cancelPorts = new();
    readonly Dictionary<Port, string> _portToSlot = new();
    readonly UnityEngine.UIElements.Toggle _entryToggle;
    readonly UnityEditor.UIElements.ObjectField _variantResolverField;

    public string NodeId { get; }
    public ActionDefinition Action { get; }
    public bool IsEntry => _entryToggle != null && _entryToggle.value;
    public ActionResolver VariantResolver => _variantResolverField?.value as ActionResolver;
    public Port InputPort { get; private set; }

    /// <summary>创建节点视图，并恢复可直接在画布中编辑的 Entry 与 Resolver 配置。</summary>
    public ActionGraphNodeView(
        string nodeId,
        ActionDefinition action,
        bool entry = false,
        ActionResolver variantResolver = null)
    {
        NodeId = nodeId;
        Action = action;
        title = action != null ? $"{nodeId}  [{action.Trigger}]" : nodeId;

        _entryToggle = new UnityEngine.UIElements.Toggle("Entry") { value = entry };
        titleContainer.Add(_entryToggle);

        _variantResolverField = new UnityEditor.UIElements.ObjectField("Variant Resolver")
        {
            objectType = typeof(ActionResolver),
            allowSceneObjects = false,
            value = variantResolver,
            tooltip = "进入该节点前执行的可选变体解析器，例如六向闪避 DirectionalActionResolver。",
        };
        extensionContainer.Add(_variantResolverField);

        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        InputPort.portName = "In";
        inputContainer.Add(InputPort);

        if (action != null)
        {
            foreach (CancelWindowNotifyState window in action.Timeline.CancelWindowStates)
            {
                if (window == null || window.CancelType != CancelType.Combo)
                    continue;

                string slot = window.CancelSlotId;
                Port port = InstantiatePort(
                    Orientation.Horizontal,
                    Direction.Output,
                    Port.Capacity.Multi,
                    typeof(bool));
                port.portName = $"{slot} ({window.CancelType} {window.StartFrame}-{window.EndFrame})";
                outputContainer.Add(port);
                _cancelPorts[slot] = port;
                _portToSlot[port] = slot;
            }
        }

        expanded = true;
        RefreshExpandedState();
        RefreshPorts();
    }

    public bool TryGetCancelPort(string slotId, out Port port) =>
        _cancelPorts.TryGetValue(slotId, out port);

    public string GetSlotIdForPort(Port port) =>
        _portToSlot.TryGetValue(port, out string slot) ? slot : null;
}
