using UnityEditor;
using UnityEngine;

/// <summary>
/// ACT Action Editor 主窗口：上方模型与属性，下方完整时间轴；单一会话管理场景和隔离预览。
/// 菜单：ACT / Action Editor
/// </summary>
public sealed class ActionEditorWindow : EditorWindow
{
    const string PreviewCharacterPrefKey = "ACTGame.ActionEditor.PreviewCharacter";
    /// <summary>假敌相对预览原点的本地 X（米）。</summary>
    const string AdhesionEnemyLocalXPrefKey = "ACTGame.ActionEditor.AdhesionEnemyLocalX";
    /// <summary>假敌相对预览原点的本地 Z（米）。</summary>
    const string AdhesionEnemyLocalZPrefKey = "ACTGame.ActionEditor.AdhesionEnemyLocalZ";
    const float DefaultAdhesionEnemyLocalZ = 3f;

    readonly ActionListPanel _listPanel = new();
    readonly ActionToolbar _toolbar = new();
    readonly ActionTimelineView _timelineView = new();

    [SerializeField] ActionDefinition _selectedAction;
    [SerializeField] CharacterConfig _characterContext;
    [SerializeField] int _characterMode;
    [SerializeField] bool _useScenePreview;
    [SerializeField] Transform _scenePreviewCharacter;

    /// <summary>使用角色引用范围和隔离模型打开时间轴。</summary>
    public static void OpenForCharacter(CharacterConfig config, int mode, ActionDefinition action)
    {
        var window = GetWindow<ActionEditorWindow>();
        bool keepPreview = window._characterContext == config || window._useScenePreview;
        window._characterContext = config;
        window._characterMode = mode;
        window._listPanel.SetCharacterScope(config, mode);
        if (!keepPreview) window.UseIsolatedPreview();
        window.titleContent = new GUIContent("Action — " + config.name);
        window.SelectAction(action);
        window.Show();
        window.Focus();
    }
    SerializedObject _serializedObject;
    readonly ActionEditorSelectionSet _selection = new();
    ActionEditorPreviewSession _previewSession;
    ActionEditorVfxPreviewExtension _vfxPreviewExtension;
    ActionEditorCameraShotPreview _cameraShotPreviewExtension;
    readonly ActionEditorHitboxWorldSpacePreview _hitboxWorldPreview = new();

    Transform _previewCharacter;
    int _previewFrame;
    bool _isPlaying;
    bool _loop = true;
    double _lastPlayTime;

    readonly ActionEditorPreviewViewport _viewport = new();
    readonly CharacterConfigSourcePanel _sourcePanel = new();
    ActionEditorWorkspaceView _workspace;
    [SerializeField] GameObject _previewPrefab;
    [SerializeField] bool _libraryVisible = true;
    [SerializeField] float _previewSpeed = 1;
    int _inspectorTab;
    string _validationResult;
    ActionDefinitionAuditEntry _auditEntry;
    Vector2 _validationScroll;
    readonly ActionEditorSfxPreview _sfxPreview = new();
    readonly ActionEditorSelectionSet _actionSettingsSelection = new();
    readonly ActionMotionBakePanel _motionBakePanel = new();
    Vector2 _motionBakeScroll;
    bool _showHitboxes = true, _showTrajectory = true;
    string _ownerSummary;


    [MenuItem("ACT/Action Editor")]
    public static void Open()
    {
        ActionEditorWindow window = GetWindow<ActionEditorWindow>();
        window._characterContext = null;
        window.UseScenePreview(null);
        window.RestorePreviewCharacter();
        window._listPanel.SetCharacterScope(null);
        window.titleContent = new GUIContent("Action Editor");
        window.minSize = new Vector2(960f, 520f);
        window.Show();
    }

    void OnEnable()
    {
        wantsMouseMove = true;
        _listPanel.SetCharacterScope(_characterContext, _characterMode);
        EditorApplication.delayCall += RestoreCharacterContext;
        minSize = new Vector2(960, 600);
        EditorApplication.playModeStateChanged += OnPlayModeChanged;

        _vfxPreviewExtension = new ActionEditorVfxPreviewExtension();
        _vfxPreviewExtension.Bind(GetVfxArrayProperty);
        _previewSession = new ActionEditorPreviewSession(this);
        // 世界 VFX 与 Camera Snapshot Binding 需 Session 临时采样进入帧 Pose。
        _vfxPreviewExtension.BindWorldPoseEvaluator(_previewSession.TryEvaluateAttachWorldPoseAtFrame);
        _previewSession.RegisterExtension(_vfxPreviewExtension);
        _cameraShotPreviewExtension = new ActionEditorCameraShotPreview();
        _cameraShotPreviewExtension.Bind(
            () => _selection.Primary,
            () => _serializedObject,
            _previewSession.TryEvaluateAttachWorldPoseAtFrame);
        _previewSession.RegisterExtension(_cameraShotPreviewExtension);
        if (_characterContext == null) SelectAction(_selectedAction);
        Undo.undoRedoPerformed += OnUndoRedo;

        EditorApplication.update += OnEditorUpdate;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        EditorApplication.delayCall -= RestoreCharacterContext;
        EditorApplication.update -= OnEditorUpdate;
        SceneView.duringSceneGui -= OnSceneGUI;
        SavePreviewCharacter();
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        _previewSession?.Dispose();
        _previewSession = null;
        _serializedObject?.Dispose(); _serializedObject = null;
        _viewport.Dispose();
        _sfxPreview.Dispose();
    }

    /// <summary>构建唯一编辑工作区；各画布继续使用原有命令与序列化上下文。</summary>
    public void CreateGUI()
    {
        _workspace = new ActionEditorWorkspaceView(rootVisualElement, DrawHeader, DrawLibrary,
            DrawViewport, DrawInspector, DrawTimeline);
        _workspace.SetLibraryVisible(_libraryVisible);
        rootVisualElement.RegisterCallback<UnityEngine.UIElements.KeyDownEvent>(e =>
        {
            if (!(e.ctrlKey || e.commandKey) || e.keyCode != KeyCode.S || EditorGUIUtility.editingTextField) return;
            if (_selectedAction != null) AssetDatabase.SaveAssetIfDirty(_selectedAction);
            e.StopImmediatePropagation();
        }, UnityEngine.UIElements.TrickleDown.TrickleDown);
    }

    void DrawHeader()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            bool visible = GUILayout.Toggle(_libraryVisible, "动作库", EditorStyles.toolbarButton, GUILayout.Width(55));
            if (visible != _libraryVisible) { _libraryVisible = visible; _workspace.SetLibraryVisible(visible); }
            if (_characterContext != null)
            { if (GUILayout.Button(_characterContext.name, EditorStyles.toolbarButton, GUILayout.Width(150))) CharacterAuthoringWindow.Open(_characterContext); }
            else GUILayout.Label("全部动作", GUILayout.Width(150));
            GUILayout.Label(_selectedAction != null ? _selectedAction.name + (EditorUtility.IsDirty(_selectedAction) ? " *" : "") : "请选择动作", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(new GUIContent(_ownerSummary ?? "", "当前动作的引用范围；详情见检查 / 来源"), EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(_selectedAction == null))
            {
                if (GUILayout.Button("位移烘焙", EditorStyles.toolbarButton, GUILayout.Width(70))) _inspectorTab = 3;
                if (GUILayout.Button("校验当前动作", EditorStyles.toolbarButton, GUILayout.Width(95))) ValidateCurrentAction();
                using (new EditorGUI.DisabledScope(_selectedAction == null || !EditorUtility.IsDirty(_selectedAction)))
                    if (GUILayout.Button("保存动作", EditorStyles.toolbarButton, GUILayout.Width(65))) AssetDatabase.SaveAssetIfDirty(_selectedAction);
            }
        }
        DrawToolbar();
    }

    void DrawLibrary(Rect rect)
    {
        var next = _listPanel.Draw(rect, _selectedAction, OpenCreateActionWindow);
        if (next != _selectedAction) SelectAction(next);
    }

    void DrawTimeline(Rect rect)
    {
        if (_selectedAction == null || _serializedObject == null) { GUI.Label(rect, "从动作库选择或创建动作", EditorStyles.centeredGreyMiniLabel); return; }
        _serializedObject.Update();
        int previousFrame = _previewFrame;
        var previousSelection = _selection.Primary;
        if (_timelineView.Draw(rect, _serializedObject, _selectedAction, _selection, ref _previewFrame, ShowAddTrackMenu))
        {
            _serializedObject.ApplyModifiedProperties();
        }
        if (!_selection.Primary.Equals(previousSelection) && _selection.HasSelection) _inspectorTab = 0;
        if (previousFrame != _previewFrame) { _isPlaying = false; _sfxPreview.Dispose(); }
        if (_timelineView.ConsumePendingRepaint()) Repaint();
    }

    void DrawInspector(Rect rect)
    {
        _inspectorTab = GUI.Toolbar(new Rect(rect.x, rect.y, rect.width, 22), _inspectorTab, new[] { "选中项", "动作", "检查 / 来源", "位移烘焙" });
        rect.yMin += 25;
        if (_serializedObject == null) { GUI.Label(rect, "选择动作后编辑属性"); return; }
        if (_inspectorTab == 3)
        {
            GUILayout.BeginArea(rect);
            _motionBakeScroll = EditorGUILayout.BeginScrollView(_motionBakeScroll);
            if (_motionBakePanel.Draw(_selectedAction, _characterContext, () =>
                { _isPlaying = false; _sfxPreview.Dispose(); _serializedObject.ApplyModifiedProperties(); }))
            {
                _serializedObject.Update();
                _previewSession.SetAction(null);
                _previewSession.SetAction(_selectedAction);
                _hitboxWorldPreview.Clear();
                _validationResult = null; _auditEntry = null;
                _showTrajectory = true;
                SceneView.RepaintAll(); Repaint();
            }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            return;
        }
        if (_inspectorTab < 2)
        {
            ActionNotifySelectionDrawer.Draw(rect, _serializedObject,
                _inspectorTab == 0 ? _selection : _actionSettingsSelection, _selectedAction, _previewCharacter);
            return;
        }
        GUILayout.BeginArea(rect);
        _validationScroll = EditorGUILayout.BeginScrollView(_validationScroll);
        _sourcePanel.Draw("当前动作", _selectedAction);
        EditorGUILayout.LabelField("烘焙", _selectedAction.BakedMotion.IsReady ? $"{_selectedAction.BakedMotion.frameCount} 帧（来源检查见位移烘焙页）" : "未就绪");
        if (GUILayout.Button("在 Inspector 定位当前资产")) { Selection.activeObject = _selectedAction; EditorGUIUtility.PingObject(_selectedAction); }
        var tracks = _serializedObject.FindProperty("timeline.tracks");
        if (tracks != null && tracks.arraySize == 0 && GUILayout.Button("根据已有窗口建立轨道"))
        { Undo.RecordObject(_selectedAction, "Build Timeline Tracks"); ActionTimelineCommands.EnsureTracksFromWindows(_serializedObject); }
        EditorGUILayout.HelpBox(_validationResult ?? "点击顶部校验按钮检查当前动作。校验不会修改资产。", MessageType.Info);
        if (_auditEntry != null)
            foreach (var issue in _auditEntry.Issues)
            {
                EditorGUILayout.HelpBox(issue.Code + "\n" + issue.Message,
                    issue.Severity == ActionDefinitionAuditSeverity.Error ? MessageType.Error : MessageType.Warning);
                if (GUILayout.Button("定位对应属性")) LocateIssue(issue.Message);
            }
        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void ValidateCurrentAction()
    {
        _auditEntry = ActionDefinitionAuditUtility.Audit(_selectedAction);
        void Collect(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Warning)
                _auditEntry.AddIssue(type == LogType.Error ? ActionDefinitionAuditSeverity.Error : ActionDefinitionAuditSeverity.Warning,
                    "ACTION_CONTENT", message);
        }
        Application.logMessageReceived += Collect;
        try { _selectedAction.ValidateContent(_selectedAction); }
        finally { Application.logMessageReceived -= Collect; }
        _validationResult = $"上次校验：{(_auditEntry.HasError ? "未通过" : "通过")} · {_auditEntry.Issues.Count} 条提示";
        _inspectorTab = 2;
    }

    void LocateIssue(string message)
    {
        foreach (ActionTimelineTrackKind kind in System.Enum.GetValues(typeof(ActionTimelineTrackKind)))
        {
            string name = ActionTimelineCommands.GetArrayPropertyName(kind);
            if (name == null) continue;
            var array = _serializedObject.FindProperty("timeline." + name);
            for (int i = 0; array != null && i < array.arraySize; i++)
            {
                var item = array.GetArrayElementAtIndex(i);
                string id = item.FindPropertyRelative("id")?.stringValue;
                if (id == null || !message.Contains("'" + id + "'") || !message.Contains(item.type)) continue;
                _selection.Set(new ActionEditorSelection(array, i, kind));
                _previewFrame = Mathf.Clamp(item.FindPropertyRelative("startFrame").intValue, 0, Mathf.Max(0, _selectedAction.TotalFrames - 1));
                _isPlaying = false; _inspectorTab = 0; Repaint(); return;
            }
        }
        _inspectorTab = 1;
    }

    void DrawViewport(Rect rect)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            GUI.Label(rect, "Play 模式已暂停编辑器预览；返回 Edit Mode 后恢复。", EditorStyles.centeredGreyMiniLabel);
            return;
        }
        GUILayout.BeginArea(new Rect(rect.x, rect.y, rect.width, 25));
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(_useScenePreview ? "场景模式" : "隔离模式", EditorStyles.miniButton, GUILayout.Width(70)))
            { if (_useScenePreview) UseIsolatedPreview(); else UseScenePreview(_scenePreviewCharacter); }
            if (_useScenePreview)
            {
                var target = (Transform)EditorGUILayout.ObjectField(_scenePreviewCharacter, typeof(Transform), true);
                if (target != _scenePreviewCharacter && (target == null || !EditorUtility.IsPersistent(target))) UseScenePreview(target);
            }
            else
            {
                var prefab = (GameObject)EditorGUILayout.ObjectField(_previewPrefab, typeof(GameObject), false);
                if (prefab != _previewPrefab) { _previewPrefab = prefab; BindIsolatedPreview(); }
            }
        }
        GUILayout.EndArea();
        rect.yMin += 26;
        GUILayout.BeginArea(new Rect(rect.x + 5, rect.y, rect.width - 10, 23));
        using (new EditorGUILayout.HorizontalScope())
        {
            _showHitboxes = GUILayout.Toggle(_showHitboxes, "命中框", EditorStyles.miniButton, GUILayout.Width(55));
            _showTrajectory = GUILayout.Toggle(_showTrajectory, "轨迹", EditorStyles.miniButton, GUILayout.Width(45));
            _sfxPreview.Draw(_selection);
        }
        GUILayout.EndArea();
        rect.yMin += 24;
        _viewport.Draw(rect, _useScenePreview, _scenePreviewCharacter, _ => DrawPreviewGizmos());
        if (!_useScenePreview && _previewCharacter != null && _showTrajectory
            && _selectedAction != null && _selectedAction.BakedMotion is { IsReady: true } baked)
        {
            // 固定图例属于 GUI，不能混入 RenderTexture 的 Handles.Label 绘制。
            GUI.Label(new Rect(rect.x + 140, rect.y + 6, Mathf.Max(1, rect.width - 148), 22),
                $"轨迹：橙 = 原始  青 = Gameplay ({baked.planarMode})  紫 = 残差", EditorStyles.miniLabel);
        }
        if (Event.current.type == EventType.Used) Repaint();
    }

    void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            _isPlaying = false;
            _previewSession?.SetPreviewCharacter(null);
            _sfxPreview.Dispose();
            _previewCharacter = null;
            _viewport.Dispose();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            if (_useScenePreview) UseScenePreview(_scenePreviewCharacter);
            else BindIsolatedPreview();
            SelectAction(_selectedAction);
        }
    }

    void DrawToolbar()
    {
        bool wasPlaying = _isPlaying;
        int previous = _previewFrame;
        bool stopped = _toolbar.Draw(_selectedAction, ref _previewFrame, ref _isPlaying, ref _loop, ref _previewSpeed);
        if (stopped || (wasPlaying && !_isPlaying) || previous != _previewFrame) _sfxPreview.Dispose();
        if (_characterContext != null && Event.current.type == EventType.Repaint) SavePreviewCharacter();
    }

    void ShowAddTrackMenu()
    {
        if (_selectedAction == null || _serializedObject == null)
        {
            EditorUtility.DisplayDialog("Action Editor", "请先选择一个 ActionDefinition。", "OK");
            return;
        }

        var menu = new GenericMenu();
        foreach (ActionTimelineTrackKind kind in System.Enum.GetValues(typeof(ActionTimelineTrackKind)))
        {
            // Animation 为默认固定轨；Phase 与其它业务窗口统一手动加轨。
            if (kind == ActionTimelineTrackKind.Animation)
                continue;

            ActionTimelineTrackKind captured = kind;
            menu.AddItem(new GUIContent(ActionEditorStyles.DisplayName(kind)), false, () =>
            {
                ActionTimelineCommands.AddTrack(_serializedObject, captured);
                Repaint();
            });
        }

        menu.ShowAsContext();
    }

    /// <summary>打开独立创建 ActionDefinition 面板。</summary>
    void OpenCreateActionWindow()
    {
        if (_characterContext != null)
        {
            CharacterActionCreateWindow.Open(_characterContext, _characterMode);
            return;
        }
        ActionDefinitionCreateWindow.Open(created =>
        {
            _listPanel.Refresh();
            SelectAction(created);
            Focus();
            Repaint();
        }, _previewPrefab);
    }

    void SelectAction(ActionDefinition action)
    {
        _selectedAction = action;
        _selection.Clear();
        _isPlaying = false;
        _serializedObject?.Dispose();
        _serializedObject = action != null ? new SerializedObject(action) : null;
        _validationResult = null; _auditEntry = null;
        _sfxPreview.Dispose();
        _sourcePanel.Invalidate();
        var owners = action != null ? CharacterAuthoringService.FindOwners(action) : null;
        _ownerSummary = owners != null && owners.Count > 1 ? $"共享 · 影响 {owners.Count} 个配置" : "";
        _previewFrame = 0;
        _hitboxWorldPreview.Clear();
        _previewSession?.SetAction(action);
        Repaint();
    }

    void OnEditorUpdate()
    {
        if (ActionAnimationPickerPanel.PreviewOwner != null)
        { _isPlaying = false; _lastPlayTime = 0; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (_isPlaying && _selectedAction != null)
        {
            double now = EditorApplication.timeSinceStartup;
            float step = 1f / (Mathf.Max(1, _selectedAction.SampleRate) * Mathf.Max(.1f, _previewSpeed));
            if (_lastPlayTime <= 0d)
                _lastPlayTime = now;

            while (now - _lastPlayTime >= step)
            {
                _lastPlayTime += step;
                int maxFrame = Mathf.Max(0, _selectedAction.TotalFrames - 1);
                if (_previewFrame >= maxFrame)
                {
                    if (_loop)
                        _previewFrame = 0;
                    else
                    {
                        _isPlaying = false;
                        break;
                    }
                }
                else
                {
                    _previewFrame++;
                }
            }

            Repaint();
        }
        else
        {
            _lastPlayTime = 0d;
        }

        if (_previewSession == null)
            return;

        _previewSession.SetAction(_selectedAction);
        _previewSession.SetPreviewCharacter(_previewCharacter);
        _previewSession.SetPreviewFrame(_previewFrame);
        _vfxPreviewExtension.IsEnabled = true;
        _previewSession.Tick();
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (!_useScenePreview) return;
        DrawPreviewGizmos();
        _cameraShotPreviewExtension?.DrawSceneGUI(sceneView);
    }

    void DrawPreviewGizmos()
    {
        if (_selectedAction == null || _previewCharacter == null)
            return;

        // 烘焙根运动轨迹：相对预览原点绘制（角色已被 Session 挪动后仍对齐）
        Vector3 trajectoryOrigin = _previewCharacter.position;
        Quaternion trajectoryRotation = _previewCharacter.rotation;
        if (_previewSession != null
            && _previewSession.TryGetBakedPreviewOrigin(out Vector3 originPos, out Quaternion originRot))
        {
            trajectoryOrigin = originPos;
            trajectoryRotation = originRot;
        }

        if (_showTrajectory) ActionMotionTrajectorySceneDrawing.DrawBakedTrajectories(
            _selectedAction,
            trajectoryOrigin,
            trajectoryRotation,
            _previewFrame, drawLabel: _useScenePreview);

        // Hitbox：仅在窗口激活时绘制；ParentToAttachPoint=false 时按 StartFrame 冻结世界盒
        ActionFrameQueryResult frameQuery =
            ActionFrameQuery.Query(_selectedAction, _previewFrame);
        HitboxNotifyState[] hitboxes = _selectedAction.HitboxStates;
        _hitboxWorldPreview.PruneInactive(hitboxes, _previewFrame);
        for (int i = 0; _showHitboxes && i < hitboxes.Length; i++)
        {
            HitboxNotifyState hitbox = hitboxes[i];
            if (hitbox == null || !frameQuery.IsStateActive(hitbox))
                continue;

            HitboxOrientedBox box = _hitboxWorldPreview.ResolveBox(
                i,
                hitbox,
                _previewCharacter,
                _previewSession);
            HitboxSceneDrawing.DrawWireOrientedBox(box, new Color(1f, 0.35f, 0.15f, 0.95f));
        }

        PlayVfxNotify[] vfxList = _selectedAction.PlayVfxNotifies;
        for (int i = 0; i < vfxList.Length; i++)
        {
            PlayVfxNotify vfx = vfxList[i];
            if (vfx == null)
                continue;

            Transform vfxAnchor = ActionEditorPreviewAttachPoint.Resolve(_previewCharacter, vfx.AttachPointId);
            // 触发后仍高亮，便于 scrub 对照挂点姿态。
            bool active = ActionFrameQuery.HasPointEventOccurred(vfx, _previewFrame);
            Color color = active
                ? new Color(0.35f, 0.75f, 1f, 0.95f)
                : new Color(0.5f, 0.5f, 0.55f, 0.4f);
            ActionVfxSceneDrawing.DrawVfxMarker(vfxAnchor, vfx, color);
        }

        // 选中 MotionModifier 时：假敌球 + TargetAdhesion 修正轨迹 / 角色落点预览
        if (_useScenePreview) DrawMotionModifierScenePreview(trajectoryOrigin, trajectoryRotation);
    }

    /// <summary>
    /// 选中 MotionModifier 窗口时在 Scene 画假敌；Adhesion 模式叠修正路径并把预览根挪到修正落点。
    /// </summary>
    void DrawMotionModifierScenePreview(Vector3 originPosition, Quaternion originRotation)
    {
        if (!_selection.HasSelection)
            return;

        ActionEditorSelection primary = _selection.Primary;
        if (!primary.IsValid || primary.Kind != ActionTimelineTrackKind.MotionModifier)
            return;

        MotionModifierNotifyState[] modifiers = _selectedAction.Timeline.MotionModifierStates;
        if (primary.Index < 0 || primary.Index >= modifiers.Length)
            return;

        MotionModifierNotifyState window = modifiers[primary.Index];
        if (window == null)
            return;

        Vector3 enemyLocal = new(
            EditorPrefs.GetFloat(AdhesionEnemyLocalXPrefKey, 0f),
            0f,
            EditorPrefs.GetFloat(AdhesionEnemyLocalZPrefKey, DefaultAdhesionEnemyLocalZ));
        Vector3 enemyWorld = originPosition + originRotation * enemyLocal;
        enemyWorld.y = originPosition.y;

        EditorGUI.BeginChangeCheck();
        // 可拖拽假敌位置；存本地偏移，随预览原点旋转
        enemyWorld = Handles.PositionHandle(enemyWorld, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Vector3 local = Quaternion.Inverse(originRotation) * (enemyWorld - originPosition);
            EditorPrefs.SetFloat(AdhesionEnemyLocalXPrefKey, local.x);
            EditorPrefs.SetFloat(AdhesionEnemyLocalZPrefKey, local.z);
            Repaint();
            SceneView.RepaintAll();
        }

        ActionMotionAdhesionSceneDrawing.Draw(
            _selectedAction,
            window,
            originPosition,
            originRotation,
            enemyWorld,
            _previewFrame,
            out Vector3 adhesionActorWorld);

        // TargetAdhesion：预览根叠修正落点（Session 每帧会先贴 Bake，再被此处覆盖）
        if (window.Mode == MotionModifierMode.TargetAdhesion && _previewCharacter != null)
        {
            Vector3 p = _previewCharacter.position;
            p.x = adhesionActorWorld.x;
            p.z = adhesionActorWorld.z;
            _previewCharacter.position = p;
        }
    }

    /// <summary>提供全部 VFX 点事件数组，供预览扩展按 Scrub 帧驱动（无需时间轴选中）。</summary>
    SerializedProperty GetVfxArrayProperty() =>
        _serializedObject?.FindProperty("timeline.playVfxNotifies");

    void RestorePreviewCharacter()
    {
        int id = EditorPrefs.GetInt(PreviewCharacterPrefKey, 0);
        if (id == 0)
            return;

        Object obj = EditorUtility.InstanceIDToObject(id);
        UseScenePreview(obj as Transform);
    }

    /// <summary>选择场景模型并立即结束旧目标的采样；保留角色动作范围，不强制回到隔离模型。</summary>
    public void UseScenePreview(Transform target)
    {
        if (target != null && (EditorUtility.IsPersistent(target) || !target.gameObject.scene.IsValid()
            || UnityEditor.SceneManagement.EditorSceneManager.IsPreviewSceneObject(target.gameObject)))
            throw new System.ArgumentException("请选择 Hierarchy 中的场景模型。");
        _isPlaying = false;
        _sfxPreview.Dispose();
        _previewSession?.SetPreviewCharacter(target);
        _viewport.Dispose();
        _useScenePreview = true;
        _scenePreviewCharacter = target;
        _previewCharacter = target;
        Repaint();
    }

    /// <summary>显式切回角色的隔离模型；先恢复场景目标，再创建或复用预览实例。</summary>
    public void UseIsolatedPreview()
    {
        _isPlaying = false;
        _sfxPreview.Dispose();
        _previewSession?.SetPreviewCharacter(null);
        _useScenePreview = false;
        _scenePreviewCharacter = null;
        _previewPrefab = _characterContext != null ? _characterContext.ModelPrefab : _previewPrefab;
        BindIsolatedPreview();
    }

    void BindIsolatedPreview()
    {
        _isPlaying = false;
        _sfxPreview.Dispose();
        _previewSession?.SetPreviewCharacter(null);
        _previewCharacter = _viewport.Bind(_previewPrefab,
            _characterContext != null ? _characterContext.ModelLocalPosition : Vector3.zero,
            _characterContext != null ? _characterContext.ModelLocalRotation : Quaternion.identity);
        _previewSession?.SetPreviewCharacter(_previewCharacter);
        Repaint();
    }

    void OnUndoRedo()
    {
        _serializedObject?.Update();
        var restored = new ActionEditorSelectionSet();
        foreach (var selected in _selection.Items)
        {
            string arrayName = selected.Kind == ActionTimelineTrackKind.Animation ? "animationSegments"
                : "timeline." + ActionTimelineCommands.GetArrayPropertyName(selected.Kind);
            var array = _serializedObject?.FindProperty(arrayName);
            if (array != null && selected.Index >= 0 && selected.Index < array.arraySize)
                restored.Toggle(new ActionEditorSelection(array, selected.Index, selected.Kind));
        }
        _selection.ReplaceWith(restored);
        _validationResult = null; _auditEntry = null;
        Repaint();
    }

    void SavePreviewCharacter()
    {
        if (!_useScenePreview) return;
        EditorPrefs.SetInt(
            PreviewCharacterPrefKey,
            _previewCharacter != null ? _previewCharacter.GetInstanceID() : 0);
    }

    /// <summary>脚本重载后恢复作者选择的场景或隔离预览，并重建动作序列化上下文。</summary>
    void RestoreCharacterContext()
    {
        if (this == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (_useScenePreview) UseScenePreview(_scenePreviewCharacter);
        else BindIsolatedPreview();
        SelectAction(_selectedAction);
        Repaint();
    }
}
