using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>敌人行为树 GraphView 画布：加载/保存 NodeDef 树与布局。</summary>
public sealed class EnemyBehaviorGraphView : GraphView
{
    readonly EnemyBehaviorTreeAsset _asset;
    readonly EditorWindow _hostWindow;
    readonly Dictionary<string, EnemyBehaviorGraphNodeView> _nodes =
        new Dictionary<string, EnemyBehaviorGraphNodeView>();
    readonly EnemyBehaviorNodeSearchWindow _searchWindow;
    bool _isLoading;
    Action _onSelectionChanged;

    /// <summary>创建画布。</summary>
    public EnemyBehaviorGraphView(
        EnemyBehaviorTreeAsset asset,
        EditorWindow hostWindow,
        Action onSelectionChanged = null)
    {
        _asset = asset;
        _hostWindow = hostWindow;
        _onSelectionChanged = onSelectionChanged;
        style.flexGrow = 1;
        AddToClassList("bt-graph");
        EnemyBehaviorTreeStyle.TryApplyStyleSheet(this);
        style.backgroundColor = EnemyBehaviorTreeStyle.CanvasBg;
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
        Insert(0, new GridBackground());

        graphViewChanged = OnGraphViewChanged;
        _searchWindow = ScriptableObject.CreateInstance<EnemyBehaviorNodeSearchWindow>();
        _searchWindow.Initialize(this);
        nodeCreationRequest = ctx =>
            SearchWindow.Open(new SearchWindowContext(ctx.screenMousePosition), _searchWindow);
    }

    /// <summary>当前选中的单个节点视图。</summary>
    public EnemyBehaviorGraphNodeView SelectedNode =>
        selection.OfType<EnemyBehaviorGraphNodeView>().FirstOrDefault();

    /// <summary>从资产 customRoot 加载图；空根则空白画布待手动创建。</summary>
    public void LoadFromAsset()
    {
        _isLoading = true;
        DeleteElements(graphElements.ToList());
        _nodes.Clear();

        EnemyBehaviorNodeDef root = _asset != null ? _asset.CustomRoot : null;
        if (root == null)
        {
            _isLoading = false;
            return;
        }

        _asset.PrepareForGraphEditor();

        List<EnemyBehaviorTreeGraphMapper.FlatRecord> flat = EnemyBehaviorTreeGraphMapper.Flatten(root);
        EnemyBehaviorGraphLayout layout = _asset.GraphLayout;
        Dictionary<string, Vector2> autoPos =
            EnemyBehaviorTreeGraphMapper.ComputeTopDownPositions(flat);

        for (int i = 0; i < flat.Count; i++)
        {
            EnemyBehaviorTreeGraphMapper.FlatRecord record = flat[i];
            if (record?.node == null)
                continue;

            Vector2 pos = autoPos.TryGetValue(record.guid, out Vector2 computed)
                ? computed
                : new Vector2(80f, 40f + i * 120f);
            if (layout != null
                && layout.TryGetNode(record.guid, out EnemyBehaviorGraphNodeLayout nodeLayout))
            {
                pos = nodeLayout.position;
            }

            AddNodeView(record.node, pos);
        }

        for (int i = 0; i < flat.Count; i++)
        {
            EnemyBehaviorTreeGraphMapper.FlatRecord record = flat[i];
            if (record == null || string.IsNullOrEmpty(record.parentGuid))
                continue;
            if (!_nodes.TryGetValue(record.parentGuid, out EnemyBehaviorGraphNodeView parent))
                continue;
            if (!_nodes.TryGetValue(record.guid, out EnemyBehaviorGraphNodeView child))
                continue;
            if (parent.OutputPort == null || child.InputPort == null)
                continue;

            AddElement(parent.OutputPort.ConnectTo(child.InputPort));
        }

        _isLoading = false;
        FrameAll();
        _onSelectionChanged?.Invoke();
    }

    /// <summary>把画布写回资产（customRoot + layout）。</summary>
    public bool PersistToAsset()
    {
        if (_asset == null)
            return false;

        List<EnemyBehaviorTreeGraphMapper.FlatRecord> records = BuildFlatRecordsFromView(out string error);
        if (records == null)
        {
            EditorUtility.DisplayDialog("Behavior Tree", error ?? "保存失败。", "OK");
            return false;
        }

        EnemyBehaviorNodeDef root = EnemyBehaviorTreeGraphMapper.Rebuild(records);
        if (root == null)
        {
            EditorUtility.DisplayDialog("Behavior Tree", "无法重建根节点。", "OK");
            return false;
        }

        EnemyBehaviorTreeValidationResult validation = EnemyBehaviorTreeValidator.ValidateTree(root);
        if (!validation.IsValid)
        {
            EditorUtility.DisplayDialog(
                "Behavior Tree",
                "校验失败：\n" + string.Join("\n", validation.Errors),
                "OK");
            return false;
        }

        Undo.RegisterCompleteObjectUndo(_asset, "Save Behavior Tree Graph");
        _asset.SetCustomRootForEditor(root);

        EnemyBehaviorGraphLayout layout = _asset.GraphLayout;
        layout.Nodes.Clear();
        foreach (KeyValuePair<string, EnemyBehaviorGraphNodeView> pair in _nodes)
        {
            Rect rect = pair.Value.GetPosition();
            layout.SetNode(pair.Key, rect.position, collapsed: false);
        }

        EditorUtility.SetDirty(_asset);
        return true;
    }

    /// <summary>在屏幕坐标创建节点。</summary>
    public void CreateNodeAtScreen(Type defType, Vector2 screenPosition)
    {
        EnemyBehaviorNodeDef def = EnemyBehaviorNodeCatalog.Create(defType);
        Vector2 graphPos = ScreenToGraphPosition(screenPosition);
        Undo.RegisterCompleteObjectUndo(_asset, "Create Behavior Tree Node");
        EnemyBehaviorGraphNodeView view = AddNodeView(def, graphPos);
        ClearSelection();
        AddToSelection(view);
        _onSelectionChanged?.Invoke();
    }

    /// <summary>按当前连线关系重新自上而下排版（不写盘，需 Save）。</summary>
    public void AutoLayoutTopDown()
    {
        List<EnemyBehaviorTreeGraphMapper.FlatRecord> records = BuildFlatRecordsFromView(out string error);
        if (records == null)
        {
            EditorUtility.DisplayDialog("Behavior Tree", error ?? "无法排版。", "OK");
            return;
        }

        Dictionary<string, Vector2> positions =
            EnemyBehaviorTreeGraphMapper.ComputeTopDownPositions(records);
        foreach (KeyValuePair<string, Vector2> pair in positions)
        {
            if (!_nodes.TryGetValue(pair.Key, out EnemyBehaviorGraphNodeView view))
                continue;
            Rect rect = view.GetPosition();
            view.SetPosition(new Rect(pair.Value, rect.size));
        }

        FrameAll();
    }

    /// <summary>按 NodeName 高亮（Play 调试路径）。</summary>
    public void ApplyDebugHighlight(string debugPath)
    {
        var names = new HashSet<string>();
        if (!string.IsNullOrEmpty(debugPath))
        {
            string[] parts = debugPath.Split(new[] { " > " }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                int colon = part.LastIndexOf(':');
                names.Add(colon > 0 ? part.Substring(0, colon) : part);
            }
        }

        foreach (KeyValuePair<string, EnemyBehaviorGraphNodeView> pair in _nodes)
            pair.Value.SetDebugHighlight(names.Contains(pair.Value.title));
    }

    /// <inheritdoc />
    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        var compatible = new List<Port>();
        ports.ForEach(port =>
        {
            if (port == startPort || port.node == startPort.node)
                return;
            if (startPort.direction == port.direction)
                return;

            Port output = startPort.direction == Direction.Output ? startPort : port;
            Port input = startPort.direction == Direction.Input ? startPort : port;
            if (output.direction != Direction.Output || input.direction != Direction.Input)
                return;

            if (output.node is EnemyBehaviorGraphNodeView parent
                && EnemyBehaviorNodeCatalog.IsDecorator(parent.Def)
                && output.connected)
            {
                return;
            }

            compatible.Add(port);
        });
        return compatible;
    }

    /// <inheritdoc />
    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        base.BuildContextualMenu(evt);
        Vector2 screen = _hostWindow != null
            ? _hostWindow.position.position + evt.localMousePosition
            : evt.mousePosition;
        evt.menu.AppendAction(
            "Create Node",
            _ => SearchWindow.Open(new SearchWindowContext(screen), _searchWindow));
    }

    EnemyBehaviorGraphNodeView AddNodeView(EnemyBehaviorNodeDef def, Vector2 position)
    {
        var view = new EnemyBehaviorGraphNodeView(def);
        view.SetPosition(new Rect(position, new Vector2(180f, 90f)));
        AddElement(view);
        _nodes[def.NodeGuid] = view;
        return view;
    }

    GraphViewChange OnGraphViewChanged(GraphViewChange change)
    {
        if (_isLoading)
            return change;

        if (change.edgesToCreate != null)
        {
            for (int i = change.edgesToCreate.Count - 1; i >= 0; i--)
            {
                Edge edge = change.edgesToCreate[i];
                if (edge?.output?.node is not EnemyBehaviorGraphNodeView parent)
                    continue;
                if (!EnemyBehaviorNodeCatalog.IsDecorator(parent.Def) || parent.OutputPort == null)
                    continue;

                foreach (Edge existing in parent.OutputPort.connections)
                {
                    if (existing == edge)
                        continue;
                    change.edgesToCreate.RemoveAt(i);
                    break;
                }
            }
        }

        if (change.elementsToRemove != null)
        {
            foreach (GraphElement element in change.elementsToRemove)
            {
                if (element is EnemyBehaviorGraphNodeView nodeView)
                    _nodes.Remove(nodeView.NodeGuid);
            }
        }

        EditorApplication.delayCall += () => _onSelectionChanged?.Invoke();
        return change;
    }

    List<EnemyBehaviorTreeGraphMapper.FlatRecord> BuildFlatRecordsFromView(out string error)
    {
        error = null;
        if (_nodes.Count == 0)
        {
            error = "画布为空。";
            return null;
        }

        var roots = new List<EnemyBehaviorGraphNodeView>();
        foreach (EnemyBehaviorGraphNodeView node in _nodes.Values)
        {
            if (node.InputPort == null || !node.InputPort.connected)
                roots.Add(node);
        }

        if (roots.Count == 0)
        {
            error = "未找到根节点（所有节点都有父）。";
            return null;
        }

        if (roots.Count > 1)
        {
            error = $"存在 {roots.Count} 个根节点，请只保留一个无父节点。";
            return null;
        }

        var records = new List<EnemyBehaviorTreeGraphMapper.FlatRecord>();
        var visited = new HashSet<string>();
        if (!WalkEmit(roots[0], string.Empty, 0, records, visited, out error))
            return null;

        if (visited.Count != _nodes.Count)
        {
            error = $"存在未连接到根的孤立节点（{_nodes.Count - visited.Count} 个）。";
            return null;
        }

        return records;
    }

    bool WalkEmit(
        EnemyBehaviorGraphNodeView node,
        string parentGuid,
        int siblingIndex,
        List<EnemyBehaviorTreeGraphMapper.FlatRecord> records,
        HashSet<string> visited,
        out string error)
    {
        error = null;
        if (node?.Def == null)
        {
            error = "空节点。";
            return false;
        }

        if (!visited.Add(node.NodeGuid))
        {
            error = $"检测到环：{node.title}";
            return false;
        }

        records.Add(new EnemyBehaviorTreeGraphMapper.FlatRecord
        {
            guid = node.NodeGuid,
            parentGuid = parentGuid ?? string.Empty,
            siblingIndex = siblingIndex,
            node = node.Def,
        });

        if (node.OutputPort == null)
            return true;

        // 自上而下树：同级从左到右为 sibling 顺序
        List<EnemyBehaviorGraphNodeView> children = node.OutputPort.connections
            .Select(edge => edge.input?.node as EnemyBehaviorGraphNodeView)
            .Where(n => n != null)
            .OrderBy(n => n.GetPosition().x)
            .ThenBy(n => n.GetPosition().y)
            .ToList();

        if (EnemyBehaviorNodeCatalog.IsDecorator(node.Def) && children.Count > 1)
        {
            error = $"装饰节点 {node.title} 只能有一个子节点。";
            return false;
        }

        for (int i = 0; i < children.Count; i++)
        {
            if (!WalkEmit(children[i], node.NodeGuid, i, records, visited, out error))
                return false;
        }

        return true;
    }

    Vector2 ScreenToGraphPosition(Vector2 screenPosition)
    {
        if (_hostWindow == null)
            return new Vector2(80f, 80f);

        Vector2 windowMouse = screenPosition - _hostWindow.position.position;
        Vector2 world = _hostWindow.rootVisualElement.LocalToWorld(windowMouse);
        return contentViewContainer.WorldToLocal(world);
    }
}
