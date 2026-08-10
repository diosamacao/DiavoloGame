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
        // 点 Properties 时让画布失焦，避免 GraphView 快捷键抢走 a/o 等字母
        inspectorHost.RegisterCallback<PointerDownEvent>(_ => _graphView?.Blur(), TrickleDown.TrickleDown);
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
            "Condition/Decorator：叠在选中宿主顶\n" +
            "点击徽章编辑；右键移除\n" +
            "连线只连 Composite/Task");
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
        EnemyBehaviorTreeCombatEntryPicker.AppendEntryWarnings(_asset, result);
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
        EnemyBehaviorGraphNodeView host = _graphView != null
            ? _graphView.InspectedHost ?? _graphView.SelectedNode
            : null;
        EnemyBehaviorNodeDef def = _graphView != null
            ? _graphView.InspectedDef ?? host?.Def
            : null;

        if (host == null || def == null)
        {
            EditorGUILayout.HelpBox(
                "选中 Composite / Task 编辑宿主。\n" +
                "先选中宿主再 Create Condition/Decorator（叠成顶部徽章）。\n" +
                "点击徽章可编辑装饰参数。",
                MessageType.Info);
            return;
        }

        bool inspectingDecorator = def != host.Def;
        if (inspectingDecorator)
        {
            EditorGUILayout.HelpBox(
                $"装饰徽章（叠在 {EnemyBehaviorGraphPresentation.ChipLabel(host.Def)} 上）",
                MessageType.None);
        }

        EditorGUI.BeginChangeCheck();
        string nodeName = EditorGUILayout.TextField("节点名", def.NodeName ?? string.Empty);
        EditorGUILayout.LabelField("类型", def.GetType().Name);

        DrawTypedFields(def, _asset);

        def.NodeName = nodeName;
        if (EditorGUI.EndChangeCheck())
        {
            host.RefreshTitle();
            // 徽章文案可能变了：重新选一次触发 chip 刷新
            if (inspectingDecorator)
                host.SetInspectedChip(def);
            else
                host.SetInspectedChip(null);
            if (_asset != null)
                EditorUtility.SetDirty(_asset);
        }
        else if (!inspectingDecorator)
        {
            host.RefreshTitle();
        }
    }

    /// <summary>按 Def 类型绘制参数；RequestCombat 需资产以解析 ActionGraph。</summary>
    static void DrawTypedFields(EnemyBehaviorNodeDef def, EnemyBehaviorTreeAsset tree)
    {
        switch (def)
        {
            case DistanceLessEqualConditionDef less:
                less.Distance = EditorGUILayout.FloatField("Distance ≤", less.Distance);
                break;
            case DistanceGreaterConditionDef greater:
                greater.Distance = EditorGUILayout.FloatField("Distance >", greater.Distance);
                break;
            case DistanceBandConditionDef band:
                band.Mode = (DistanceBandMode)EditorGUILayout.EnumPopup("Mode", band.Mode);
                band.EnterDistance = EditorGUILayout.FloatField("Enter Distance", band.EnterDistance);
                band.ExitDistance = EditorGUILayout.FloatField("Exit Distance", band.ExitDistance);
                band.MinDwellSeconds = EditorGUILayout.FloatField("Min Dwell Seconds", band.MinDwellSeconds);
                break;
            case InAttackRangeConditionDef inRange:
                inRange.Distance = EditorGUILayout.FloatField("Attack Range", inRange.Distance);
                break;
            case IsCharacterStateConditionDef state:
                state.Expected = (CharacterStateType)EditorGUILayout.EnumPopup("Expected", state.Expected);
                break;
            case CooldownReadyConditionDef ready:
                ready.CooldownId = EditorGUILayout.TextField("Cooldown Id", ready.CooldownId);
                break;
            case CooldownNotReadyConditionDef notReady:
                notReady.CooldownId = EditorGUILayout.TextField("Cooldown Id", notReady.CooldownId);
                break;
            case CooldownGateNodeDef gate:
                gate.CooldownId = EditorGUILayout.TextField("Cooldown Id", gate.CooldownId);
                gate.CooldownSeconds = EditorGUILayout.FloatField("Cooldown Seconds", gate.CooldownSeconds);
                break;
            case AggroGateNodeDef aggro:
                aggro.EnterRadius = EditorGUILayout.FloatField("Enter Radius", aggro.EnterRadius);
                aggro.ExitRadius = EditorGUILayout.FloatField("Exit Radius", aggro.ExitRadius);
                break;
            case MoveTowardTargetActionDef move:
                move.Magnitude = EditorGUILayout.Slider("Magnitude", move.Magnitude, 0f, 1f);
                move.StopDistance = EditorGUILayout.FloatField("Stop Distance", move.StopDistance);
                move.FaceTarget = EditorGUILayout.Toggle("Face Target", move.FaceTarget);
                break;
            case BackOffFromTargetActionDef backOff:
                backOff.Magnitude = EditorGUILayout.Slider("Magnitude", backOff.Magnitude, 0f, 1f);
                break;
            case StrafeAroundTargetActionDef strafe:
                strafe.SideSign = EditorGUILayout.FloatField("Side Sign", strafe.SideSign);
                strafe.Magnitude = EditorGUILayout.Slider("Magnitude", strafe.Magnitude, 0f, 1f);
                break;
            case RequestCombatActionDef request:
                EnemyBehaviorTreeCombatEntryPicker.DrawRequestCombatFields(request, tree);
                break;
            case RandomSelectorNodeDef random:
                random.SyncWeightCount();
                EditorGUILayout.LabelField("Weights（与子节点下标对齐）");
                if (random.children != null)
                {
                    for (int i = 0; i < random.children.Count; i++)
                    {
                        string childName = random.children[i] != null
                            ? (string.IsNullOrEmpty(random.children[i].NodeName)
                                ? EnemyBehaviorTreeGraphMapper.DefaultNodeName(random.children[i])
                                : random.children[i].NodeName)
                            : "(null)";
                        random.weights[i] = EditorGUILayout.FloatField($"[{i}] {childName}", random.weights[i]);
                    }
                }

                break;
            case WaitFramesActionDef wait:
                wait.DurationSeconds = EditorGUILayout.FloatField("Duration Seconds", wait.DurationSeconds);
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
