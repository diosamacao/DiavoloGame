using UnityEditor;
using UnityEngine;

/// <summary>时间轴 Frameline：标尺、多轨绘制、窗口拖拽与选中。</summary>
public sealed class ActionTimelineView
{
    enum DragMode
    {
        None,
        Move,
        ResizeStart,
        ResizeEnd,
        Scrub,
    }

    Vector2 _scroll;
    DragMode _dragMode;
    ActionTimelineTrackKind _dragKind;
    int _dragIndex = -1;
    int _dragStartFrame;
    int _dragOriginalStart;
    int _dragOriginalEnd;
    int _dragControlId = -1;
    float _pixelsPerFrame = 8f;
    bool _pendingRepaint;

    ActionEditorSelection _pendingSelection;
    bool _hasPendingSelection;
    bool _clearSelection;

    /// <summary>是否有菜单等异步操作需要宿主窗口重绘。</summary>
    public bool ConsumePendingRepaint()
    {
        if (!_pendingRepaint)
            return false;

        _pendingRepaint = false;
        return true;
    }

    /// <summary>
    /// 绘制时间轴；返回是否修改了数据，并通过 selection/previewFrame 回写交互结果。
    /// </summary>
    public bool Draw(
        Rect rect,
        SerializedObject so,
        ActionDefinition action,
        ref ActionEditorSelection selection,
        ref int previewFrame)
    {
        if (action == null || so == null)
        {
            GUI.Box(rect, "Select an ActionDefinition");
            return false;
        }

        // GenericMenu 回调在下一帧生效，此处消费挂起的选中变更。
        if (_hasPendingSelection)
        {
            selection = _pendingSelection;
            _hasPendingSelection = false;
        }

        if (_clearSelection)
        {
            selection = default;
            _clearSelection = false;
        }

        bool changed = false;
        int totalFrames = Mathf.Max(1, action.TotalFrames);
        float contentWidth = ActionEditorStyles.TrackHeaderWidth + totalFrames * _pixelsPerFrame + 40f;

        SerializedProperty tracksProp = so.FindProperty("timeline.tracks");
        int trackCount = tracksProp != null ? tracksProp.arraySize : 0;
        float contentHeight = ActionEditorStyles.RulerHeight
            + Mathf.Max(1, trackCount) * (ActionEditorStyles.TrackHeight + 2f)
            + 40f;

        Rect toolbarRect = new(rect.x, rect.y, rect.width, 20f);
        Rect bodyRect = new(rect.x, rect.y + 22f, rect.width, rect.height - 22f);

        EditorGUI.BeginChangeCheck();
        _pixelsPerFrame = EditorGUI.Slider(toolbarRect, "Zoom", _pixelsPerFrame, 3f, 20f);
        if (EditorGUI.EndChangeCheck())
            changed = true;

        _scroll = GUI.BeginScrollView(bodyRect, _scroll, new Rect(0f, 0f, contentWidth, contentHeight));

        Rect rulerRect = new(ActionEditorStyles.TrackHeaderWidth, 0f, totalFrames * _pixelsPerFrame, ActionEditorStyles.RulerHeight);
        DrawRuler(rulerRect, totalFrames);
        HandleRulerInput(rulerRect, totalFrames, ref previewFrame, ref changed);

        float y = ActionEditorStyles.RulerHeight + 2f;
        if (trackCount == 0)
        {
            GUI.Label(
                new Rect(8f, y, 420f, 40f),
                "时间轴为空。使用工具栏 Add Track 手动添加轨道。");
        }

        for (int trackIndex = 0; trackIndex < trackCount; trackIndex++)
        {
            SerializedProperty trackProp = tracksProp.GetArrayElementAtIndex(trackIndex);
            if (!trackProp.FindPropertyRelative("visible").boolValue)
                continue;

            Rect trackRect = new(0f, y, contentWidth, ActionEditorStyles.TrackHeight);
            DrawTrack(trackRect, so, action, trackProp, trackIndex, totalFrames, ref selection, previewFrame, ref changed);
            y += ActionEditorStyles.TrackHeight + 2f;
        }

        // 拖拽期间在 ScrollView 内统一处理，避免依赖每帧重建的 SerializedProperty 引用。
        if (ProcessActiveWindowDrag(so, totalFrames, ref changed))
            changed = true;

        float playheadX = ActionEditorStyles.TrackHeaderWidth + previewFrame * _pixelsPerFrame;
        Handles.BeginGUI();
        Handles.color = ActionEditorStyles.Playhead;
        Handles.DrawLine(new Vector3(playheadX, 0f), new Vector3(playheadX, contentHeight));
        Handles.EndGUI();

        GUI.EndScrollView();
        HandleDeleteKey(so, ref selection, ref changed);
        return changed;
    }

    void DrawTrack(
        Rect trackRect,
        SerializedObject so,
        ActionDefinition action,
        SerializedProperty trackProp,
        int trackIndex,
        int totalFrames,
        ref ActionEditorSelection selection,
        int previewFrame,
        ref bool changed)
    {
        var kind = (ActionTimelineTrackKind)trackProp.FindPropertyRelative("kind").enumValueIndex;
        string trackName = trackProp.FindPropertyRelative("trackName").stringValue;

        Rect headerRect = new(trackRect.x, trackRect.y, ActionEditorStyles.TrackHeaderWidth, trackRect.height);
        Rect laneRect = new(
            trackRect.x + ActionEditorStyles.TrackHeaderWidth,
            trackRect.y,
            totalFrames * _pixelsPerFrame,
            trackRect.height);

        EditorGUI.DrawRect(headerRect, new Color(0.22f, 0.22f, 0.25f, 1f));
        EditorGUI.DrawRect(laneRect, ActionEditorStyles.Background);

        Rect nameRect = new(headerRect.x + 4f, headerRect.y + 4f, headerRect.width - 28f, headerRect.height - 8f);
        string newName = EditorGUI.DelayedTextField(nameRect, trackName);
        if (newName != trackName)
        {
            ActionTimelineCommands.RenameTrack(so, trackIndex, newName);
            trackName = newName;
            changed = true;
        }

        Rect menuRect = new(headerRect.xMax - 22f, headerRect.y + 4f, 18f, headerRect.height - 8f);
        if (GUI.Button(menuRect, "⋮", EditorStyles.miniButton))
            ShowTrackMenu(so, action, kind, trackName, trackIndex, previewFrame);

        Event evt = Event.current;
        if (evt.type == EventType.MouseDown
            && evt.clickCount == 2
            && evt.button == 0
            && laneRect.Contains(evt.mousePosition)
            && _dragMode == DragMode.None)
        {
            int frame = FrameAtX(evt.mousePosition.x, totalFrames);
            selection = ActionTimelineCommands.AddWindow(
                so, kind, trackName, frame, action.SampleRate, action.TotalFrames);
            changed = true;
            evt.Use();
        }

        string arrayName = ActionTimelineCommands.GetArrayPropertyName(kind);
        if (arrayName == null)
            return;

        SerializedProperty arrayProp = so.FindProperty($"timeline.{arrayName}");
        if (arrayProp == null)
            return;

        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            SerializedProperty element = arrayProp.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = element.FindPropertyRelative("trackName");
            if (nameProp != null && nameProp.stringValue != trackName)
                continue;

            var itemSelection = new ActionEditorSelection(arrayProp, i, kind);
            DrawWindow(laneRect, element, itemSelection, totalFrames, ref selection, ref changed);
        }
    }

    void ShowTrackMenu(
        SerializedObject so,
        ActionDefinition action,
        ActionTimelineTrackKind kind,
        string trackName,
        int trackIndex,
        int previewFrame)
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Add Window"), false, () =>
        {
            _pendingSelection = ActionTimelineCommands.AddWindow(
                so, kind, trackName, previewFrame, action.SampleRate, action.TotalFrames);
            _hasPendingSelection = true;
            _pendingRepaint = true;
        });
        menu.AddItem(new GUIContent("Delete Track"), false, () =>
        {
            if (!EditorUtility.DisplayDialog("Delete Track", $"删除轨道「{trackName}」及其窗口？", "Delete", "Cancel"))
                return;

            ActionTimelineCommands.RemoveTrack(so, trackIndex);
            _clearSelection = true;
            _pendingRepaint = true;
        });
        menu.ShowAsContext();
    }

    /// <summary>绘制窗口条块；仅在 MouseDown 时开始拖拽，实际改帧在 ProcessActiveWindowDrag。</summary>
    void DrawWindow(
        Rect laneRect,
        SerializedProperty element,
        ActionEditorSelection itemSelection,
        int totalFrames,
        ref ActionEditorSelection selection,
        ref bool changed)
    {
        SerializedProperty startProp = element.FindPropertyRelative("startFrame");
        SerializedProperty endProp = element.FindPropertyRelative("endFrame");
        SerializedProperty idProp = element.FindPropertyRelative("id");
        if (startProp == null || endProp == null)
            return;

        int start = startProp.intValue;
        int end = endProp.intValue;
        float x = laneRect.x + start * _pixelsPerFrame;
        float width = Mathf.Max(_pixelsPerFrame, (end - start + 1) * _pixelsPerFrame);
        Rect clipRect = new(x, laneRect.y + 3f, width, laneRect.height - 6f);

        bool selected = selection.Equals(itemSelection);
        Color color = selected
            ? ActionEditorStyles.ColorForSelectedTrack(itemSelection.Kind)
            : ActionEditorStyles.ColorForTrack(itemSelection.Kind);

        EditorGUI.DrawRect(clipRect, color);
        // 用 Label 仅作绘制，不参与控件焦点，避免抢走拖拽事件。
        GUI.Label(clipRect, idProp != null ? idProp.stringValue : itemSelection.Kind.ToString(), EditorStyles.miniLabel);

        float edge = Mathf.Min(ActionEditorStyles.EdgeHandleWidth, width * 0.35f);
        Rect leftHandle = new(clipRect.x, clipRect.y, edge, clipRect.height);
        Rect rightHandle = new(clipRect.xMax - edge, clipRect.y, edge, clipRect.height);

        EditorGUIUtility.AddCursorRect(leftHandle, MouseCursor.ResizeHorizontal);
        EditorGUIUtility.AddCursorRect(rightHandle, MouseCursor.ResizeHorizontal);
        EditorGUIUtility.AddCursorRect(clipRect, MouseCursor.MoveArrow);

        Event evt = Event.current;
        if (evt.type != EventType.MouseDown || evt.button != 0 || !clipRect.Contains(evt.mousePosition))
            return;

        // 已有其它拖拽（含 Scrub）时不抢占。
        if (_dragMode != DragMode.None && _dragMode != DragMode.Scrub)
            return;

        selection = itemSelection;
        _dragKind = itemSelection.Kind;
        _dragIndex = itemSelection.Index;
        _dragStartFrame = FrameAtX(evt.mousePosition.x, totalFrames);
        _dragOriginalStart = start;
        _dragOriginalEnd = end;
        _dragControlId = GUIUtility.GetControlID(FocusType.Passive);
        GUIUtility.hotControl = _dragControlId;

        if (leftHandle.Contains(evt.mousePosition))
            _dragMode = DragMode.ResizeStart;
        else if (rightHandle.Contains(evt.mousePosition))
            _dragMode = DragMode.ResizeEnd;
        else
            _dragMode = DragMode.Move;

        changed = true;
        evt.Use();
    }

    /// <summary>在持有 hotControl 期间处理窗口平移/缩放；用 Kind+Index 重新取属性。</summary>
    bool ProcessActiveWindowDrag(SerializedObject so, int totalFrames, ref bool changed)
    {
        if (_dragMode is DragMode.None or DragMode.Scrub || _dragIndex < 0)
            return false;

        Event evt = Event.current;
        if (evt.type != EventType.MouseDrag && evt.type != EventType.MouseUp && evt.type != EventType.Ignore)
            return false;

        // hotControl 被清掉时结束拖拽，避免残留状态。
        if (_dragControlId >= 0 && GUIUtility.hotControl != _dragControlId && evt.type != EventType.MouseUp)
        {
            EndWindowDrag();
            return false;
        }

        string arrayName = ActionTimelineCommands.GetArrayPropertyName(_dragKind);
        if (arrayName == null)
        {
            EndWindowDrag();
            return false;
        }

        SerializedProperty arrayProp = so.FindProperty($"timeline.{arrayName}");
        if (arrayProp == null || _dragIndex >= arrayProp.arraySize)
        {
            EndWindowDrag();
            return false;
        }

        SerializedProperty element = arrayProp.GetArrayElementAtIndex(_dragIndex);
        SerializedProperty startProp = element.FindPropertyRelative("startFrame");
        SerializedProperty endProp = element.FindPropertyRelative("endFrame");
        if (startProp == null || endProp == null)
        {
            EndWindowDrag();
            return false;
        }

        int frame = FrameAtX(evt.mousePosition.x, totalFrames);
        int delta = frame - _dragStartFrame;

        Undo.RecordObject(so.targetObject, "Edit Action Window");
        startProp.intValue = _dragOriginalStart;
        endProp.intValue = _dragOriginalEnd;

        switch (_dragMode)
        {
            case DragMode.Move:
                ActionTimelineCommands.MoveWindow(element, delta, totalFrames);
                break;
            case DragMode.ResizeStart:
                ActionTimelineCommands.ResizeWindowStart(element, _dragOriginalStart + delta, totalFrames);
                break;
            case DragMode.ResizeEnd:
                ActionTimelineCommands.ResizeWindowEnd(element, _dragOriginalEnd + delta, totalFrames);
                break;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(so.targetObject);
        changed = true;

        if (evt.type is EventType.MouseUp or EventType.Ignore)
            EndWindowDrag();

        evt.Use();
        return true;
    }

    void EndWindowDrag()
    {
        if (_dragControlId >= 0 && GUIUtility.hotControl == _dragControlId)
            GUIUtility.hotControl = 0;

        _dragMode = DragMode.None;
        _dragIndex = -1;
        _dragControlId = -1;
    }

    void DrawRuler(Rect rect, int totalFrames)
    {
        EditorGUI.DrawRect(rect, ActionEditorStyles.Ruler);
        Handles.BeginGUI();
        Handles.color = new Color(1f, 1f, 1f, 0.25f);
        for (int f = 0; f <= totalFrames; f++)
        {
            float x = rect.x + f * _pixelsPerFrame;
            float h = f % 5 == 0 ? rect.height : rect.height * 0.4f;
            Handles.DrawLine(new Vector3(x, rect.yMax - h), new Vector3(x, rect.yMax));
            if (f % 5 == 0)
                GUI.Label(new Rect(x + 2f, rect.y, 28f, 16f), f.ToString(), EditorStyles.miniLabel);
        }

        Handles.EndGUI();
    }

    void HandleRulerInput(Rect rulerRect, int totalFrames, ref int previewFrame, ref bool changed)
    {
        Event evt = Event.current;

        // 窗口拖拽进行中时不处理标尺。
        if (_dragMode is DragMode.Move or DragMode.ResizeStart or DragMode.ResizeEnd)
            return;

        if (!rulerRect.Contains(evt.mousePosition) && _dragMode != DragMode.Scrub)
            return;

        if (evt.type == EventType.MouseDown && evt.button == 0 && rulerRect.Contains(evt.mousePosition))
        {
            _dragMode = DragMode.Scrub;
            _dragControlId = GUIUtility.GetControlID(FocusType.Passive);
            GUIUtility.hotControl = _dragControlId;
            previewFrame = FrameAtX(evt.mousePosition.x, totalFrames);
            changed = true;
            evt.Use();
        }
        else if (_dragMode == DragMode.Scrub && evt.type == EventType.MouseDrag)
        {
            previewFrame = FrameAtX(evt.mousePosition.x, totalFrames);
            changed = true;
            evt.Use();
        }
        else if (_dragMode == DragMode.Scrub && evt.type is EventType.MouseUp or EventType.Ignore)
        {
            if (_dragControlId >= 0 && GUIUtility.hotControl == _dragControlId)
                GUIUtility.hotControl = 0;

            _dragMode = DragMode.None;
            _dragControlId = -1;
            evt.Use();
        }
    }

    void HandleDeleteKey(SerializedObject so, ref ActionEditorSelection selection, ref bool changed)
    {
        Event evt = Event.current;
        if (evt.type != EventType.KeyDown)
            return;

        if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace)
            return;

        if (!selection.IsValid)
            return;

        ActionTimelineCommands.RemoveWindow(so, selection);
        selection = default;
        changed = true;
        evt.Use();
    }

    int FrameAtX(float x, int totalFrames)
    {
        float local = x - ActionEditorStyles.TrackHeaderWidth;
        int frame = Mathf.FloorToInt(local / _pixelsPerFrame);
        return Mathf.Clamp(frame, 0, Mathf.Max(0, totalFrames - 1));
    }
}
