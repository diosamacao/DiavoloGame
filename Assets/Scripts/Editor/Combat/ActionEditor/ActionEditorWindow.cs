using UnityEditor;
using UnityEngine;

/// <summary>
/// ACT Action Editor 主窗口：左侧资产列表、中部时间轴、右侧细节、顶部预览工具栏。
/// 菜单：ACT / Action Editor
/// </summary>
public sealed class ActionEditorWindow : EditorWindow
{
    const string PreviewCharacterPrefKey = "ACTGame.ActionEditor.PreviewCharacter";
    const string LeftWidthPrefKey = "ACTGame.ActionEditor.LeftWidth";
    const string RightWidthPrefKey = "ACTGame.ActionEditor.RightWidth";

    readonly ActionListPanel _listPanel = new();
    readonly ActionToolbar _toolbar = new();
    readonly ActionTimelineView _timelineView = new();

    ActionDefinition _selectedAction;
    SerializedObject _serializedObject;
    readonly ActionEditorSelectionSet _selection = new();
    ActionEditorPreviewSession _previewSession;
    ActionEditorVfxPreviewExtension _vfxPreviewExtension;
    readonly ActionEditorHitboxWorldSpacePreview _hitboxWorldPreview = new();

    Transform _previewCharacter;
    int _previewFrame;
    bool _isPlaying;
    bool _loop = true;
    double _lastPlayTime;

    float _leftWidth = ActionEditorStyles.DefaultLeftWidth;
    float _rightWidth = ActionEditorStyles.DefaultRightWidth;
    int _splitterDrag; // 0=无 1=左分隔 2=右分隔
    float _splitterDragStartX;
    float _splitterDragStartWidth;

    [MenuItem("ACT/Action Editor")]
    public static void Open()
    {
        ActionEditorWindow window = GetWindow<ActionEditorWindow>();
        window.titleContent = new GUIContent("Action Editor");
        window.minSize = new Vector2(960f, 520f);
        window.Show();
    }

    void OnEnable()
    {
        wantsMouseMove = true;
        _listPanel.Refresh();
        RestorePreviewCharacter();
        _leftWidth = EditorPrefs.GetFloat(LeftWidthPrefKey, ActionEditorStyles.DefaultLeftWidth);
        _rightWidth = EditorPrefs.GetFloat(RightWidthPrefKey, ActionEditorStyles.DefaultRightWidth);

        _vfxPreviewExtension = new ActionEditorVfxPreviewExtension();
        _vfxPreviewExtension.Bind(GetVfxArrayProperty);
        _previewSession = new ActionEditorPreviewSession(this);
        // 世界空间 VFX 在触发帧冻结落点，需 Session 临时采样该帧 Pose
        _vfxPreviewExtension.BindWorldPoseEvaluator(_previewSession.TryEvaluateAttachWorldPoseAtFrame);
        _previewSession.RegisterExtension(_vfxPreviewExtension);

        EditorApplication.update += OnEditorUpdate;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        SceneView.duringSceneGui -= OnSceneGUI;
        SavePreviewCharacter();
        EditorPrefs.SetFloat(LeftWidthPrefKey, _leftWidth);
        EditorPrefs.SetFloat(RightWidthPrefKey, _rightWidth);
        _previewSession?.Dispose();
        _previewSession = null;
    }

    void OnGUI()
    {
        if (_timelineView.ConsumePendingRepaint())
            Repaint();

        DrawToolbar();

        // 工具栏高度用实际 layout 后的剩余区域，避免硬编码 22 与真实高度错位。
        Rect content = new(0f, 24f, position.width, Mathf.Max(0f, position.height - 24f));
        ComputePanelRects(content, out Rect left, out Rect splitterL, out Rect center, out Rect splitterR, out Rect right);
        HandleSplitterDrag(splitterL, splitterR);

        Rect leftBody = ActionEditorStyles.DrawPanelChrome(left, "Actions", ActionEditorStyles.PanelLeft);
        Rect centerBody = ActionEditorStyles.DrawPanelChrome(center, "Timeline", ActionEditorStyles.PanelCenter);
        Rect rightBody = ActionEditorStyles.DrawPanelChrome(right, "Inspector", ActionEditorStyles.PanelRight);
        ActionEditorStyles.DrawSplitter(splitterL);
        ActionEditorStyles.DrawSplitter(splitterR);

        ActionDefinition next = _listPanel.Draw(leftBody, _selectedAction, OpenCreateActionWindow);
        if (next != _selectedAction)
            SelectAction(next);

        if (_selectedAction != null && _serializedObject != null)
        {
            _serializedObject.Update();
            if (_timelineView.Draw(
                    centerBody,
                    _serializedObject,
                    _selectedAction,
                    _selection,
                    ref _previewFrame,
                    ShowAddTrackMenu))
            {
                _serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(_selectedAction);
            }

            ActionNotifySelectionDrawer.Draw(rightBody, _serializedObject, _selection, _selectedAction);
        }
        else
        {
            GUI.Box(centerBody, "从左侧选择或创建 ActionDefinition");
            GUILayout.BeginArea(rightBody);
            EditorGUILayout.HelpBox("选中招式后可编辑窗口细节。", MessageType.Info);
            GUILayout.EndArea();
        }

        if (_timelineView.ConsumePendingRepaint())
            Repaint();
    }

    /// <summary>
    /// 按可用宽度分配左/中/右，保证中栏最小宽度，避免右栏遮挡时间轴。
    /// </summary>
    void ComputePanelRects(
        Rect content,
        out Rect left,
        out Rect splitterL,
        out Rect center,
        out Rect splitterR,
        out Rect right)
    {
        float splitter = ActionEditorStyles.SplitterWidth;
        float available = content.width - splitter * 2f;

        float leftW = Mathf.Clamp(_leftWidth, ActionEditorStyles.MinLeftWidth, ActionEditorStyles.MaxLeftWidth);
        float rightW = Mathf.Clamp(_rightWidth, ActionEditorStyles.MinRightWidth, ActionEditorStyles.MaxRightWidth);
        float minCenter = ActionEditorStyles.MinCenterWidth;

        // 空间不足时优先压缩左右，保住中栏。
        if (leftW + rightW + minCenter > available)
        {
            float overflow = leftW + rightW + minCenter - available;
            float leftRoom = Mathf.Max(0f, leftW - ActionEditorStyles.MinLeftWidth);
            float rightRoom = Mathf.Max(0f, rightW - ActionEditorStyles.MinRightWidth);
            float room = leftRoom + rightRoom;
            if (room > 0.01f)
            {
                leftW -= overflow * (leftRoom / room);
                rightW -= overflow * (rightRoom / room);
            }

            leftW = Mathf.Max(ActionEditorStyles.MinLeftWidth, leftW);
            rightW = Mathf.Max(ActionEditorStyles.MinRightWidth, rightW);
            if (leftW + rightW + minCenter > available)
            {
                // 极端窄窗：再等比压缩到可用宽。
                float side = Mathf.Max(0f, available - minCenter);
                float sum = leftW + rightW;
                if (sum > 0.01f)
                {
                    leftW = side * (leftW / sum);
                    rightW = side * (rightW / sum);
                }
            }
        }

        float centerW = Mathf.Max(minCenter, available - leftW - rightW);
        // 若仍溢出（窗口极窄），以实际剩余为准，允许中栏暂时小于理想最小值。
        if (leftW + rightW + centerW > available)
            centerW = Mathf.Max(40f, available - leftW - rightW);

        left = new Rect(content.x, content.y, leftW, content.height);
        splitterL = new Rect(left.xMax, content.y, splitter, content.height);
        center = new Rect(splitterL.xMax, content.y, centerW, content.height);
        splitterR = new Rect(center.xMax, content.y, splitter, content.height);
        right = new Rect(splitterR.xMax, content.y, rightW, content.height);

        _leftWidth = leftW;
        _rightWidth = rightW;
    }

    /// <summary>处理左右分隔条拖拽，写回宽度并持久化。</summary>
    void HandleSplitterDrag(Rect splitterL, Rect splitterR)
    {
        Event evt = Event.current;
        int leftId = GUIUtility.GetControlID(FocusType.Passive);
        int rightId = GUIUtility.GetControlID(FocusType.Passive);

        switch (evt.type)
        {
            case EventType.MouseDown when evt.button == 0:
                if (splitterL.Contains(evt.mousePosition))
                {
                    _splitterDrag = 1;
                    _splitterDragStartX = evt.mousePosition.x;
                    _splitterDragStartWidth = _leftWidth;
                    GUIUtility.hotControl = leftId;
                    evt.Use();
                }
                else if (splitterR.Contains(evt.mousePosition))
                {
                    _splitterDrag = 2;
                    _splitterDragStartX = evt.mousePosition.x;
                    _splitterDragStartWidth = _rightWidth;
                    GUIUtility.hotControl = rightId;
                    evt.Use();
                }

                break;

            case EventType.MouseDrag when _splitterDrag != 0:
                float delta = evt.mousePosition.x - _splitterDragStartX;
                if (_splitterDrag == 1)
                {
                    _leftWidth = Mathf.Clamp(
                        _splitterDragStartWidth + delta,
                        ActionEditorStyles.MinLeftWidth,
                        ActionEditorStyles.MaxLeftWidth);
                }
                else
                {
                    // 右分隔条：向右拖应减小右栏宽度。
                    _rightWidth = Mathf.Clamp(
                        _splitterDragStartWidth - delta,
                        ActionEditorStyles.MinRightWidth,
                        ActionEditorStyles.MaxRightWidth);
                }

                Repaint();
                evt.Use();
                break;

            case EventType.MouseUp when _splitterDrag != 0:
                _splitterDrag = 0;
                GUIUtility.hotControl = 0;
                EditorPrefs.SetFloat(LeftWidthPrefKey, _leftWidth);
                EditorPrefs.SetFloat(RightWidthPrefKey, _rightWidth);
                evt.Use();
                break;
        }
    }

    void DrawToolbar()
    {
        _toolbar.Draw(
            _selectedAction,
            ref _previewCharacter,
            ref _previewFrame,
            ref _isPlaying,
            ref _loop);

        if (_previewCharacter != null)
            SavePreviewCharacter();
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
        ActionDefinitionCreateWindow.Open(created =>
        {
            _listPanel.Refresh();
            SelectAction(created);
            Focus();
            Repaint();
        });
    }

    void SelectAction(ActionDefinition action)
    {
        _selectedAction = action;
        _selection.Clear();
        _isPlaying = false;
        _serializedObject = action != null ? new SerializedObject(action) : null;
        if (_serializedObject != null)
            ActionTimelineCommands.EnsureTracksFromWindows(_serializedObject);
        _previewFrame = 0;
        _hitboxWorldPreview.Clear();
        _previewSession?.SetAction(action);
        Repaint();
    }

    void OnEditorUpdate()
    {
        if (_isPlaying && _selectedAction != null)
        {
            double now = EditorApplication.timeSinceStartup;
            float step = 1f / _selectedAction.SampleRate;
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

        ActionMotionTrajectorySceneDrawing.DrawBakedTrajectories(
            _selectedAction,
            trajectoryOrigin,
            trajectoryRotation,
            _previewFrame);

        // Hitbox：仅在窗口激活时绘制；ParentToAttachPoint=false 时按 StartFrame 冻结世界盒
        ActionFrameQueryResult frameQuery =
            ActionFrameQuery.Query(_selectedAction, _previewFrame);
        HitboxNotifyState[] hitboxes = _selectedAction.HitboxStates;
        _hitboxWorldPreview.PruneInactive(hitboxes, _previewFrame);
        for (int i = 0; i < hitboxes.Length; i++)
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
        _previewCharacter = obj as Transform;
    }

    void SavePreviewCharacter()
    {
        EditorPrefs.SetInt(
            PreviewCharacterPrefKey,
            _previewCharacter != null ? _previewCharacter.GetInstanceID() : 0);
    }
}
