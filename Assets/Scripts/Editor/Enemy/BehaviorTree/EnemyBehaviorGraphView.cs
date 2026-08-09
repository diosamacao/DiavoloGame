using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 行为树画布：宿主节点连线；Condition/Decorator 以 UE 风格徽章叠在宿主上。
/// </summary>
public sealed class EnemyBehaviorGraphView : GraphView
{
    readonly EnemyBehaviorTreeAsset _asset;
    readonly EditorWindow _hostWindow;
    readonly Dictionary<string, EnemyBehaviorGraphNodeView> _nodes =
        new Dictionary<string, EnemyBehaviorGraphNodeView>();
    readonly EnemyBehaviorNodeSearchWindow _searchWindow;
    bool _isLoading;
    Action _onSelectionChanged;

    /// <summary>当前检视目标（宿主或徽章装饰）。</summary>
    public EnemyBehaviorNodeDef InspectedDef { get; private set; }

    /// <summary>检视目标所属宿主视图。</summary>
    public EnemyBehaviorGraphNodeView InspectedHost { get; private set; }

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

    /// <summary>当前选中的宿主节点。</summary>
    public EnemyBehaviorGraphNodeView SelectedNode =>
        selection.OfType<EnemyBehaviorGraphNodeView>().FirstOrDefault();

    /// <summary>从资产加载：剥装饰为徽章，只画宿主与宿主边。</summary>
    public void LoadFromAsset()
    {
        _isLoading = true;
        DeleteElements(graphElements.ToList());
        _nodes.Clear();
        InspectedDef = null;
        InspectedHost = null;

        EnemyBehaviorNodeDef root = _asset != null ? _asset.CustomRoot : null;
        if (root == null)
        {
            _isLoading = false;
            return;
        }

        _asset.PrepareForGraphEditor();
        EnemyBehaviorGraphLayout layout = _asset.GraphLayout;

        // 先剥根，再按宿主父子建图
        var hostParent = new Dictionary<string, string>();
        var hostSibling = new Dictionary<string, int>();
        var hostOrder = new List<string>();
        var hostDecorators = new Dictionary<string, List<EnemyBehaviorNodeDef>>();
        var hostDefs = new Dictionary<string, EnemyBehaviorNodeDef>();

        CollectHosts(
            root,
            parentHostGuid: null,
            siblingIndex: 0,
            hostParent,
            hostSibling,
            hostOrder,
            hostDecorators,
            hostDefs);

        // 仅宿主参与自动排版（装饰不再占格）
        var layoutRecords = new List<EnemyBehaviorTreeGraphMapper.FlatRecord>();
        for (int i = 0; i < hostOrder.Count; i++)
        {
            string guid = hostOrder[i];
            hostParent.TryGetValue(guid, out string parentGuid);
            hostSibling.TryGetValue(guid, out int sibling);
            layoutRecords.Add(new EnemyBehaviorTreeGraphMapper.FlatRecord
            {
                guid = guid,
                parentGuid = parentGuid ?? string.Empty,
                siblingIndex = sibling,
                node = hostDefs[guid],
            });
        }

        Dictionary<string, Vector2> autoPos =
            EnemyBehaviorTreeGraphMapper.ComputeTopDownPositions(layoutRecords);

        for (int i = 0; i < hostOrder.Count; i++)
        {
            string guid = hostOrder[i];
            EnemyBehaviorNodeDef host = hostDefs[guid];
            Vector2 pos = autoPos.TryGetValue(guid, out Vector2 computed)
                ? computed
                : new Vector2(80f, 40f + i * 140f);
            if (layout != null
                && layout.TryGetNode(guid, out EnemyBehaviorGraphNodeLayout nodeLayout))
            {
                pos = nodeLayout.position;
            }

            hostDecorators.TryGetValue(guid, out List<EnemyBehaviorNodeDef> decs);
            AddHostView(host, decs, pos);
        }

        for (int i = 0; i < hostOrder.Count; i++)
        {
            string guid = hostOrder[i];
            if (!hostParent.TryGetValue(guid, out string parentGuid) || string.IsNullOrEmpty(parentGuid))
                continue;
            if (!_nodes.TryGetValue(parentGuid, out EnemyBehaviorGraphNodeView parent))
                continue;
            if (!_nodes.TryGetValue(guid, out EnemyBehaviorGraphNodeView child))
                continue;
            if (parent.OutputPort == null || child.InputPort == null)
                continue;
            AddElement(parent.OutputPort.ConnectTo(child.InputPort));
        }

        _isLoading = false;
        FrameAll();
        _onSelectionChanged?.Invoke();
    }

    /// <summary>写回资产：徽章展开为装饰链后 Rebuild。</summary>
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

    /// <summary>
    /// 创建节点：装饰/条件叠到选中宿主；Composite/Task 新建宿主牌。
    /// </summary>
    public void CreateNodeAtScreen(Type defType, Vector2 screenPosition)
    {
        if (_asset == null || defType == null)
            return;

        EnemyBehaviorNodeDef created = EnemyBehaviorNodeCatalog.Create(defType);
        Undo.RegisterCompleteObjectUndo(_asset, "Create Behavior Tree Node");

        if (EnemyBehaviorNodeCatalog.IsDecorator(created))
        {
            EnemyBehaviorGraphNodeView host = SelectedNode;
            if (host == null)
            {
                EditorUtility.DisplayDialog(
                    "Behavior Tree",
                    "请先选中一个 Composite / Task 宿主节点，再添加 Condition / Decorator。",
                    "OK");
                return;
            }

            host.AddDecoratorOuter(created);
            EditorUtility.SetDirty(_asset);
            _onSelectionChanged?.Invoke();
            return;
        }

        Vector2 graphPos = ScreenToGraphPosition(screenPosition);
        EnemyBehaviorGraphNodeView view = AddHostView(created, null, graphPos);
        ClearSelection();
        AddToSelection(view);
        SetInspect(view, created);
        _onSelectionChanged?.Invoke();
    }

    /// <summary>按宿主树自上而下排版（忽略装饰链中间层）。</summary>
    public void AutoLayoutTopDown()
    {
        List<EnemyBehaviorTreeGraphMapper.FlatRecord> hostOnly = BuildHostLayoutRecords(out string error);
        if (hostOnly == null)
        {
            EditorUtility.DisplayDialog("Behavior Tree", error ?? "无法排版。", "OK");
            return;
        }

        Dictionary<string, Vector2> positions =
            EnemyBehaviorTreeGraphMapper.ComputeTopDownPositions(hostOnly);
        foreach (KeyValuePair<string, Vector2> pair in positions)
        {
            if (!_nodes.TryGetValue(pair.Key, out EnemyBehaviorGraphNodeView view))
                continue;
            Rect rect = view.GetPosition();
            view.SetPosition(new Rect(pair.Value, rect.size));
        }

        FrameAll();
    }

    /// <summary>按 NodeName 高亮宿主（含其徽章名）。</summary>
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
            pair.Value.SetDebugHighlight(names);
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

    void SetInspect(EnemyBehaviorGraphNodeView host, EnemyBehaviorNodeDef def)
    {
        InspectedHost = host;
        InspectedDef = def;
        foreach (KeyValuePair<string, EnemyBehaviorGraphNodeView> pair in _nodes)
        {
            EnemyBehaviorNodeDef chip = pair.Value == host && def != null && def != host.Def
                ? def
                : null;
            pair.Value.SetInspectedChip(chip);
        }
    }

    void OnInspectRequest(EnemyBehaviorGraphNodeView host, EnemyBehaviorNodeDef def)
    {
        ClearSelection();
        if (host != null)
            AddToSelection(host);
        SetInspect(host, def);
        _onSelectionChanged?.Invoke();
    }

    EnemyBehaviorGraphNodeView AddHostView(
        EnemyBehaviorNodeDef host,
        IReadOnlyList<EnemyBehaviorNodeDef> decorators,
        Vector2 position)
    {
        var view = new EnemyBehaviorGraphNodeView(host, decorators, OnInspectRequest);
        view.SetPosition(new Rect(position, new Vector2(200f, 100f)));
        AddElement(view);
        _nodes[host.NodeGuid] = view;
        return view;
    }

    GraphViewChange OnGraphViewChanged(GraphViewChange change)
    {
        if (_isLoading)
            return change;

        if (change.elementsToRemove != null)
        {
            foreach (GraphElement element in change.elementsToRemove)
            {
                if (element is EnemyBehaviorGraphNodeView nodeView)
                {
                    _nodes.Remove(nodeView.NodeGuid);
                    if (InspectedHost == nodeView)
                    {
                        InspectedHost = null;
                        InspectedDef = null;
                    }
                }
            }
        }

        EditorApplication.delayCall += () =>
        {
            // 框选宿主时默认检视宿主本体
            EnemyBehaviorGraphNodeView selected = SelectedNode;
            if (selected != null
                && (InspectedHost != selected
                    || InspectedDef == null
                    || !IsDefOnHost(selected, InspectedDef)))
            {
                SetInspect(selected, selected.Def);
            }

            _onSelectionChanged?.Invoke();
        };
        return change;
    }

    static bool IsDefOnHost(EnemyBehaviorGraphNodeView host, EnemyBehaviorNodeDef def)
    {
        if (host == null || def == null)
            return false;
        if (host.Def == def)
            return true;
        for (int i = 0; i < host.Decorators.Count; i++)
        {
            if (host.Decorators[i] == def)
                return true;
        }

        return false;
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
        var visitedHosts = new HashSet<string>();
        if (!WalkEmit(roots[0], string.Empty, 0, records, visitedHosts, out error))
            return null;

        if (visitedHosts.Count != _nodes.Count)
        {
            error = $"存在未连接到根的孤立节点（{_nodes.Count - visitedHosts.Count} 个）。";
            return null;
        }

        return records;
    }

    bool WalkEmit(
        EnemyBehaviorGraphNodeView node,
        string parentGuid,
        int siblingIndex,
        List<EnemyBehaviorTreeGraphMapper.FlatRecord> records,
        HashSet<string> visitedHosts,
        out string error)
    {
        error = null;
        if (node?.Def == null)
        {
            error = "空节点。";
            return false;
        }

        if (!visitedHosts.Add(node.NodeGuid))
        {
            error = $"检测到环：{node.title}";
            return false;
        }

        // 徽章外→内展开为装饰链，再接宿主
        string prevParent = parentGuid ?? string.Empty;
        int emitSibling = siblingIndex;
        for (int i = 0; i < node.Decorators.Count; i++)
        {
            EnemyBehaviorNodeDef dec = node.Decorators[i];
            if (dec == null)
            {
                error = $"空装饰：{node.title}";
                return false;
            }

            if (string.IsNullOrEmpty(dec.NodeGuid))
                EnemyBehaviorTreeGraphMapper.EnsureStableIds(dec);

            records.Add(new EnemyBehaviorTreeGraphMapper.FlatRecord
            {
                guid = dec.NodeGuid,
                parentGuid = prevParent,
                siblingIndex = emitSibling,
                node = dec,
            });
            prevParent = dec.NodeGuid;
            emitSibling = 0;
        }

        records.Add(new EnemyBehaviorTreeGraphMapper.FlatRecord
        {
            guid = node.NodeGuid,
            parentGuid = prevParent,
            siblingIndex = emitSibling,
            node = node.Def,
        });

        if (node.OutputPort == null)
            return true;

        List<EnemyBehaviorGraphNodeView> children = node.OutputPort.connections
            .Select(edge => edge.input?.node as EnemyBehaviorGraphNodeView)
            .Where(n => n != null)
            .OrderBy(n => n.GetPosition().x)
            .ThenBy(n => n.GetPosition().y)
            .ToList();

        for (int i = 0; i < children.Count; i++)
        {
            if (!WalkEmit(children[i], node.NodeGuid, i, records, visitedHosts, out error))
                return false;
        }

        return true;
    }

    static void CollectHosts(
        EnemyBehaviorNodeDef node,
        string parentHostGuid,
        int siblingIndex,
        Dictionary<string, string> hostParent,
        Dictionary<string, int> hostSibling,
        List<string> hostOrder,
        Dictionary<string, List<EnemyBehaviorNodeDef>> hostDecorators,
        Dictionary<string, EnemyBehaviorNodeDef> hostDefs)
    {
        if (node == null)
            return;

        EnemyBehaviorGraphPresentation.Peel(node, out List<EnemyBehaviorNodeDef> decs, out EnemyBehaviorNodeDef host);
        if (host == null || !EnemyBehaviorGraphPresentation.IsHost(host))
            return;

        if (string.IsNullOrEmpty(host.NodeGuid))
            EnemyBehaviorTreeGraphMapper.EnsureStableIds(host);

        if (!hostDefs.ContainsKey(host.NodeGuid))
        {
            hostOrder.Add(host.NodeGuid);
            hostDefs[host.NodeGuid] = host;
            hostDecorators[host.NodeGuid] = decs;
            hostSibling[host.NodeGuid] = siblingIndex;
            if (!string.IsNullOrEmpty(parentHostGuid))
                hostParent[host.NodeGuid] = parentHostGuid;
        }

        if (EnemyBehaviorTreeGraphMapper.TryGetChildren(host, out List<EnemyBehaviorNodeDef> children))
        {
            for (int i = 0; i < children.Count; i++)
            {
                CollectHosts(
                    children[i],
                    host.NodeGuid,
                    i,
                    hostParent,
                    hostSibling,
                    hostOrder,
                    hostDecorators,
                    hostDefs);
            }
        }
    }

    /// <summary>从宿主连线生成排版用 FlatRecord（parent 均为宿主 guid）。</summary>
    List<EnemyBehaviorTreeGraphMapper.FlatRecord> BuildHostLayoutRecords(out string error)
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

        if (roots.Count != 1)
        {
            error = roots.Count == 0 ? "未找到根节点。" : $"存在 {roots.Count} 个根节点。";
            return null;
        }

        var records = new List<EnemyBehaviorTreeGraphMapper.FlatRecord>();
        var visited = new HashSet<string>();
        if (!WalkHostLayout(roots[0], string.Empty, 0, records, visited, out error))
            return null;
        return records;
    }

    bool WalkHostLayout(
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

        List<EnemyBehaviorGraphNodeView> children = node.OutputPort.connections
            .Select(edge => edge.input?.node as EnemyBehaviorGraphNodeView)
            .Where(n => n != null)
            .OrderBy(n => n.GetPosition().x)
            .ThenBy(n => n.GetPosition().y)
            .ToList();

        for (int i = 0; i < children.Count; i++)
        {
            if (!WalkHostLayout(children[i], node.NodeGuid, i, records, visited, out error))
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
