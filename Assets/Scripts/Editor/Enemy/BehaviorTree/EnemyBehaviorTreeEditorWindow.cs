using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>敌人行为树 GraphView 编辑器窗口（BT-E3）。</summary>
public sealed class EnemyBehaviorTreeEditorWindow : EditorWindow
{
    EnemyBehaviorTreeAsset _asset;
    EnemyBehaviorGraphView _graphView;
    IMGUIContainer _inspector;
    Label _statusLabel;
    double _nextDebugPoll;

    /// <summary>打开指定行为树资产。</summary>
    public static void Open(EnemyBehaviorTreeAsset asset)
    {
        EnemyBehaviorTreeEditorWindow window = GetWindow<EnemyBehaviorTreeEditorWindow>();
        window.titleContent = new GUIContent(
            asset != null ? $"BT — {asset.name}" : "Behavior Tree");
        window._asset = asset;
        window.RebuildUi();
        window.Focus();
    }

    [MenuItem("ACT/Enemy/Behavior Tree Editor")]
    static void OpenMenu()
    {
        Open(Selection.activeObject as EnemyBehaviorTreeAsset);
    }

    void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        RebuildUi();
    }

    void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    void OnSelectionChange()
    {
        if (Selection.activeObject is EnemyBehaviorTreeAsset asset && asset != _asset)
        {
            _asset = asset;
            titleContent = new GUIContent($"BT — {asset.name}");
            RebuildUi();
        }

        RefreshInspector();
        PollDebugHighlight(force: true);
    }

    void OnPlayModeChanged(PlayModeStateChange _) => PollDebugHighlight(force: true);

    void Update()
    {
        if (EditorApplication.timeSinceStartup < _nextDebugPoll)
            return;
        _nextDebugPoll = EditorApplication.timeSinceStartup + 0.2;
        PollDebugHighlight(force: false);
    }

    void RebuildUi()
    {
        rootVisualElement.Clear();
        _graphView = null;
        _inspector = null;
        EnemyBehaviorTreeStyle.TryApplyStyleSheet(rootVisualElement);
        rootVisualElement.style.backgroundColor = EnemyBehaviorTreeStyle.PanelBg;

        if (_asset == null)
        {
            rootVisualElement.Add(new Label("选中或打开一个 EnemyBehaviorTree 资产。")
            {
                style = { marginLeft = 8, marginTop = 8, color = new Color(0.85f, 0.85f, 0.85f) },
            });
            return;
        }

        // —— 顶栏（对齐 BD 深色工具条）——
        var toolbar = new VisualElement();
        toolbar.AddToClassList("bt-toolbar");
        toolbar.Add(new Button(Save) { text = "Save" });
        toolbar.Add(new Button(Revert) { text = "Revert" });
        toolbar.Add(new Button(Validate) { text = "Validate" });
        toolbar.Add(new Button(() =>
        {
            _graphView?.AutoLayoutTopDown();
            SetStatus("已自上而下整理（记得 Save）");
        })
        {
            text = "Auto Layout",
            tooltip = "根在上、子在下、同级从左到右（BD/UE 树形）",
        });

        _statusLabel = new Label($"  {_asset.name}");
        _statusLabel.AddToClassList("bt-status");
        toolbar.Add(_statusLabel);
        rootVisualElement.Add(toolbar);

        var body = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, flexGrow = 1 },
        };

        // —— 左侧 Tasks 图例（对齐 BD 任务分类浏览习惯）——
        body.Add(BuildTasksSidePanel());

        _graphView = new EnemyBehaviorGraphView(_asset, this, RefreshInspector);
        var graphHost = new VisualElement { style = { flexGrow = 1 } };
        graphHost.Add(_graphView);
        body.Add(graphHost);

        // —— 右侧 Properties ——
        var inspectorHost = new VisualElement();
        inspectorHost.AddToClassList("bt-inspector-panel");
        inspectorHost.style.paddingLeft = 6;
        inspectorHost.style.paddingRight = 6;
        inspectorHost.style.paddingTop = 6;
        var propsTitle = new Label("Properties");
        propsTitle.AddToClassList("bt-panel-title");
        inspectorHost.Add(propsTitle);
        _inspector = new IMGUIContainer(DrawInspector)
        {
            style = { flexGrow = 1 },
        };
        inspectorHost.Add(_inspector);
        body.Add(inspectorHost);

        rootVisualElement.Add(body);
        _graphView.LoadFromAsset();
        SetStatus(_asset.CustomRoot != null ? "已加载" : "空树：请 Create Node 后 Save");
    }

    /// <summary>左侧分类图例 + 操作提示（BD Task 面板简化版）。</summary>
    static VisualElement BuildTasksSidePanel()
    {
        var panel = new VisualElement();
        panel.AddToClassList("bt-side-panel");

        var title = new Label("Tasks");
        title.AddToClassList("bt-panel-title");
        panel.Add(title);

        AddLegend(panel, "Composite", EnemyBehaviorTreeStyle.Composite);
        AddLegend(panel, "Decorator", EnemyBehaviorTreeStyle.Decorator);
        AddLegend(panel, "Conditional", EnemyBehaviorTreeStyle.Condition);
        AddLegend(panel, "Action", EnemyBehaviorTreeStyle.Task);

        var hint = new Label(
            "空格 / 右键：Create Node\n" +
            "拖拽连线：父↓ → 子↑\n" +
            "Play 选中敌人：Running 绿框\n" +
            "样式参考 Behavior Designer（自研近似）");
        hint.AddToClassList("bt-hint");
        panel.Add(hint);
        return panel;
    }

    static void AddLegend(VisualElement parent, string label, Color color)
    {
        var row = new VisualElement();
        row.AddToClassList("bt-legend-row");
        var swatch = new VisualElement();
        swatch.AddToClassList("bt-legend-swatch");
        swatch.style.backgroundColor = color;
        row.Add(swatch);
        row.Add(new Label(label)
        {
            style = { color = new Color(0.85f, 0.85f, 0.85f), fontSize = 11 },
        });
        parent.Add(row);
    }

    void Save()
    {
        if (_graphView == null || _asset == null)
            return;
        if (!_graphView.PersistToAsset())
            return;
        AssetDatabase.SaveAssets();
        SetStatus("已保存");
        Debug.Log($"[BehaviorTree] 已保存 '{_asset.name}'。", _asset);
    }

    void Revert()
    {
        if (_asset == null)
            return;
        AssetDatabase.Refresh();
        string path = AssetDatabase.GetAssetPath(_asset);
        _asset = AssetDatabase.LoadAssetAtPath<EnemyBehaviorTreeAsset>(path);
        RebuildUi();
        SetStatus("已从磁盘重新加载");
    }

    void Validate()
    {
        if (_graphView != null)
            _graphView.PersistToAsset();

        if (_asset == null)
            return;

        EnemyBehaviorTreeValidationResult result = _asset.ValidateAsset();
        if (result.IsValid)
        {
            SetStatus($"Validate 通过（警告 {result.Warnings.Count}）");
            Debug.Log($"[{_asset.name}] Validate 通过。", _asset);
        }
        else
        {
            SetStatus($"Validate 失败：{result.Errors.Count} error");
            for (int i = 0; i < result.Errors.Count; i++)
                Debug.LogError(result.Errors[i], _asset);
        }

        for (int i = 0; i < result.Warnings.Count; i++)
            Debug.LogWarning(result.Warnings[i], _asset);
    }

    void RefreshInspector() => _inspector?.MarkDirtyLayout();

    void DrawInspector()
    {
        EnemyBehaviorGraphNodeView node = _graphView != null ? _graphView.SelectedNode : null;
        if (node?.Def == null)
        {
            EditorGUILayout.HelpBox("选中一个节点以编辑参数。\n空格 / 右键 Create Node 打开调色板。", MessageType.Info);
            return;
        }

        EnemyBehaviorNodeDef def = node.Def;
        EditorGUI.BeginChangeCheck();
        string nodeName = EditorGUILayout.TextField("节点名", def.NodeName ?? string.Empty);
        EditorGUILayout.LabelField("类型", def.GetType().Name);

        DrawTypedFields(def);

        // 参数字段在 DrawTypedFields 内已写回 Def；此处同步标题
        def.NodeName = nodeName;
        if (EditorGUI.EndChangeCheck())
        {
            node.RefreshTitle();
            if (_asset != null)
                EditorUtility.SetDirty(_asset);
        }
        else
        {
            node.RefreshTitle();
        }
    }

    static void DrawTypedFields(EnemyBehaviorNodeDef def)
    {
        switch (def)
        {
            case DistanceLessEqualConditionDef less:
                less.Distance = EditorGUILayout.FloatField("Distance ≤", less.Distance);
                break;
            case DistanceGreaterConditionDef greater:
                greater.Distance = EditorGUILayout.FloatField("Distance >", greater.Distance);
                break;
            case IsCharacterStateConditionDef state:
                state.Expected = (CharacterStateType)EditorGUILayout.EnumPopup("Expected", state.Expected);
                break;
            case CooldownReadyConditionDef ready:
                ready.CooldownId = EditorGUILayout.TextField("Cooldown Id", ready.CooldownId);
                break;
            case CooldownGateNodeDef gate:
                gate.CooldownId = EditorGUILayout.TextField("Cooldown Id", gate.CooldownId);
                gate.CooldownFrames = EditorGUILayout.IntField("Cooldown Frames", gate.CooldownFrames);
                break;
            case StrafeAroundTargetActionDef strafe:
                strafe.SideSign = EditorGUILayout.FloatField("Side Sign", strafe.SideSign);
                break;
            case WaitFramesActionDef wait:
                wait.DurationFrames = EditorGUILayout.IntField("Duration Frames", wait.DurationFrames);
                break;
            default:
                EditorGUILayout.LabelField("（无额外参数）");
                break;
        }
    }

    void PollDebugHighlight(bool force)
    {
        if (_graphView == null || !Application.isPlaying)
        {
            if (force)
                _graphView?.ApplyDebugHighlight(null);
            return;
        }

        EnemyController enemy = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<EnemyController>()
            : null;
        if (enemy == null)
        {
            _graphView.ApplyDebugHighlight(null);
            return;
        }

        enemy.EnsureBehaviorDebugEnabled();
        _graphView.ApplyDebugHighlight(enemy.DebugBehaviorPath);
    }

    void SetStatus(string message)
    {
        if (_statusLabel != null && _asset != null)
            _statusLabel.text = $"  {_asset.name}  —  {message}";
    }
}
