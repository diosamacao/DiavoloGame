using UnityEditor;
using UnityEngine;

/// <summary>
/// ACT Action Editor 主窗口：左侧资产列表、中部时间轴、右侧细节、顶部预览工具栏。
/// 菜单：ACT / Action Editor
/// </summary>
public sealed class ActionEditorWindow : EditorWindow
{
    const string PreviewCharacterPrefKey = "ACTGame.ActionEditor.PreviewCharacter";
    const float LeftWidth = 220f;
    const float RightWidth = 300f;

    readonly ActionListPanel _listPanel = new();
    readonly ActionToolbar _toolbar = new();
    readonly ActionTimelineView _timelineView = new();

    ActionDefinition _selectedAction;
    SerializedObject _serializedObject;
    ActionEditorSelection _selection;
    ActionEditorPreviewSession _previewSession;
    ActionEditorVfxPreviewExtension _vfxPreviewExtension;

    Transform _previewCharacter;
    int _previewFrame;
    bool _isPlaying;
    bool _loop = true;
    double _lastPlayTime;

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

        _vfxPreviewExtension = new ActionEditorVfxPreviewExtension();
        _vfxPreviewExtension.Bind(GetSelectedVfxProperty);
        _previewSession = new ActionEditorPreviewSession(this);
        _previewSession.RegisterExtension(_vfxPreviewExtension);

        EditorApplication.update += OnEditorUpdate;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        SceneView.duringSceneGui -= OnSceneGUI;
        SavePreviewCharacter();
        _previewSession?.Dispose();
        _previewSession = null;
    }

    void OnGUI()
    {
        if (_timelineView.ConsumePendingRepaint())
            Repaint();

        DrawToolbar();

        Rect content = new(0f, 22f, position.width, position.height - 22f);
        Rect left = new(content.x, content.y, LeftWidth, content.height);
        Rect right = new(content.xMax - RightWidth, content.y, RightWidth, content.height);
        Rect center = new(left.xMax + 4f, content.y, content.width - LeftWidth - RightWidth - 8f, content.height);

        ActionDefinition next = _listPanel.Draw(left, _selectedAction);
        if (next != _selectedAction)
            SelectAction(next);

        if (_selectedAction != null && _serializedObject != null)
        {
            _serializedObject.Update();
            if (_timelineView.Draw(center, _serializedObject, _selectedAction, ref _selection, ref _previewFrame))
            {
                _serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(_selectedAction);
            }

            ActionNotifySelectionDrawer.Draw(right, _serializedObject, _selection, _selectedAction);
        }
        else
        {
            GUI.Box(center, "从左侧选择 ActionDefinition");
            GUI.Box(right, string.Empty);
        }

        // 菜单回调可能在下一帧才改选中，这里再消费一次。
        if (_timelineView.ConsumePendingRepaint())
            Repaint();
    }

    void DrawToolbar()
    {
        _toolbar.Draw(
            _selectedAction,
            ref _previewCharacter,
            ref _previewFrame,
            ref _isPlaying,
            ref _loop,
            ShowAddTrackMenu);

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
            if (kind == ActionTimelineTrackKind.Phase)
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

    void SelectAction(ActionDefinition action)
    {
        _selectedAction = action;
        _selection = default;
        _isPlaying = false;
        _serializedObject = action != null ? new SerializedObject(action) : null;
        if (_serializedObject != null)
            ActionTimelineCommands.EnsureTracksFromWindows(_serializedObject);
        _previewFrame = 0;
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

        Transform anchor = ActionEditorPreviewAttachPoint.Resolve(_previewCharacter);
        HitboxNotifyState[] hitboxes = _selectedAction.HitboxStates;
        for (int i = 0; i < hitboxes.Length; i++)
        {
            HitboxNotifyState hitbox = hitboxes[i];
            if (hitbox == null)
                continue;

            bool active = hitbox.IsActiveAtFrame(_previewFrame);
            Color color = active
                ? new Color(1f, 0.35f, 0.15f, 0.95f)
                : new Color(0.6f, 0.6f, 0.6f, 0.35f);
            HitboxOrientedBox box = HitboxMath.BuildFromHitbox(_previewCharacter, anchor, hitbox);
            HitboxSceneDrawing.DrawWireOrientedBox(box, color);
        }

        PlayVfxNotify[] vfxList = _selectedAction.PlayVfxNotifies;
        for (int i = 0; i < vfxList.Length; i++)
        {
            PlayVfxNotify vfx = vfxList[i];
            if (vfx == null)
                continue;

            bool active = vfx.IsActiveAtFrame(_previewFrame);
            Color color = active
                ? new Color(0.35f, 0.75f, 1f, 0.95f)
                : new Color(0.5f, 0.5f, 0.55f, 0.4f);
            ActionVfxSceneDrawing.DrawVfxMarker(anchor, vfx, color);
        }
    }

    SerializedProperty GetSelectedVfxProperty()
    {
        if (_selection.Kind != ActionTimelineTrackKind.Vfx || !_selection.IsValid)
            return null;

        return _selection.ElementProperty;
    }

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
