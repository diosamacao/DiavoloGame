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
    /// <summary>每帧像素宽；每帧按中栏可用宽度 / totalFrames 自动计算，铺满时间轴。</summary>
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
    /// onAddTrack：中栏顶部右侧 Add Track 按钮回调。
    /// </summary>
    public bool Draw(
        Rect rect,
        SerializedObject so,
        ActionDefinition action,
        ref ActionEditorSelection selection,
        ref int previewFrame,
        System.Action onAddTrack)
    {
        if (action == null || so == null)
        {
            GUI.Box(rect, "从左侧选择或创建 ActionDefinition");
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

        SerializedProperty tracksProp = so.FindProperty("timeline.tracks");
        int trackCount = tracksProp != null ? tracksProp.arraySize : 0;
        // +1：默认 Animation 轨始终占一行。
        int visibleLaneCount = 1 + Mathf.Max(0, trackCount);
        float contentHeight = ActionEditorStyles.RulerHeight
            + visibleLaneCount * (ActionEditorStyles.TrackHeight + 2f)
            + 80f;

        // 顶栏：帧数 + Add Track（动画段在默认 Animation 轨上展示）。
        const float topBarHeight = 22f;
        const float addTrackWidth = 90f;
        Rect topBarRect = new(rect.x, rect.y, rect.width, topBarHeight);
        Rect addTrackRect = new(topBarRect.xMax - addTrackWidth - 4f, topBarRect.y + 1f, addTrackWidth, 20f);

        int totalFrames = Mathf.Max(1, so.FindProperty("totalFrames")?.intValue ?? action.TotalFrames);
        GUI.Label(
            new Rect(topBarRect.x + 8f, topBarRect.y + 1f, 160f, 20f),
            $"{totalFrames} frames",
            EditorStyles.miniLabel);

        if (GUI.Button(addTrackRect, "Add Track"))
            onAddTrack?.Invoke();

        Rect bodyRect = new(rect.x, rect.y + topBarHeight + 2f, rect.width, rect.height - topBarHeight - 2f);

        const float verticalScrollBarWidth = 14f;
        bool needsVerticalScroll = contentHeight > bodyRect.height;
        float usableWidth = Mathf.Max(1f, bodyRect.width - (needsVerticalScroll ? verticalScrollBarWidth : 0f));
        float contentWidth = usableWidth;
        float laneWidth = Mathf.Max(1f, usableWidth - ActionEditorStyles.TrackHeaderWidth);
        _pixelsPerFrame = laneWidth / totalFrames;

        _scroll = GUI.BeginScrollView(
            bodyRect,
            _scroll,
            new Rect(0f, 0f, contentWidth, contentHeight),
            false,
            needsVerticalScroll);

        EditorGUI.DrawRect(
            new Rect(0f, 0f, ActionEditorStyles.TrackHeaderWidth, ActionEditorStyles.RulerHeight),
            ActionEditorStyles.PanelHeader);
        GUI.Label(
            new Rect(4f, 2f, ActionEditorStyles.TrackHeaderWidth - 8f, ActionEditorStyles.RulerHeight - 4f),
            "Tracks",
            EditorStyles.miniLabel);

        Rect rulerRect = new(ActionEditorStyles.TrackHeaderWidth, 0f, laneWidth, ActionEditorStyles.RulerHeight);
        DrawRuler(rulerRect, totalFrames);
        HandleRulerInput(rulerRect, totalFrames, ref previewFrame, ref changed);

        float y = ActionEditorStyles.RulerHeight + 2f;

        // 默认动画轨：始终置顶，展示 animationSegments。
        Rect animationTrackRect = new(0f, y, contentWidth, ActionEditorStyles.TrackHeight);
        DrawAnimationTrack(animationTrackRect, so, action, totalFrames, ref selection, ref changed);
        y += ActionEditorStyles.TrackHeight + 2f;

        if (trackCount == 0)
            DrawEmptyTracksHint(new Rect(ActionEditorStyles.TrackHeaderWidth + 8f, y, Mathf.Max(200f, laneWidth - 16f), 72f));

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

    /// <summary>无轨道时的空态提示，引导使用右上角 Add Track（Animation 轨始终存在）。</summary>
    void DrawEmptyTracksHint(Rect rect)
    {
        EditorGUI.DrawRect(rect, ActionEditorStyles.EmptyStateBox);
        GUI.Label(
            new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, 48f),
            "尚无 Hitbox/Cancel 等业务轨道。\n点击右上角 Add Track 添加；上方 Animation 轨用于展示多段 Clip。",
            EditorStyles.wordWrappedLabel);
    }

    /// <summary>绘制默认 Animation 轨：按全局帧铺开各 animationSegments。</summary>
    void DrawAnimationTrack(
        Rect trackRect,
        SerializedObject so,
        ActionDefinition action,
        int totalFrames,
        ref ActionEditorSelection selection,
        ref bool changed)
    {
        Rect headerRect = new(trackRect.x, trackRect.y, ActionEditorStyles.TrackHeaderWidth, trackRect.height);
        Rect laneRect = new(
            trackRect.x + ActionEditorStyles.TrackHeaderWidth,
            trackRect.y,
            Mathf.Max(1f, trackRect.width - ActionEditorStyles.TrackHeaderWidth),
            trackRect.height);

        EditorGUI.DrawRect(headerRect, new Color(0.22f, 0.22f, 0.25f, 1f));
        EditorGUI.DrawRect(laneRect, ActionEditorStyles.Background);
        GUI.Label(
            new Rect(headerRect.x + 4f, headerRect.y + 4f, headerRect.width - 28f, headerRect.height - 8f),
            ActionEditorStyles.DisplayName(ActionTimelineTrackKind.Animation),
            EditorStyles.miniLabel);

        Rect menuRect = new(headerRect.xMax - 22f, headerRect.y + 4f, 18f, headerRect.height - 8f);
        if (GUI.Button(menuRect, "⋮", EditorStyles.miniButton))
            ShowAnimationTrackMenu(so);

        SerializedProperty segmentsProp = so.FindProperty("animationSegments");
        if (segmentsProp == null)
            return;

        Event evt = Event.current;
        if (evt.type == EventType.MouseDown
            && evt.clickCount == 2
            && evt.button == 0
            && laneRect.Contains(evt.mousePosition)
            && _dragMode == DragMode.None)
        {
            selection = ActionTimelineCommands.AddAnimationSegment(so);
            changed = true;
            evt.Use();
        }

        float sampleRate = action.SampleRate;
        int cursor = 0;
        for (int i = 0; i < segmentsProp.arraySize; i++)
        {
            SerializedProperty element = segmentsProp.GetArrayElementAtIndex(i);
            int frameCount = ResolveSegmentFrameCount(element, sampleRate);
            if (frameCount <= 0)
                continue;

            int globalStart = cursor;
            int globalEnd = cursor + frameCount - 1;
            cursor += frameCount;

            DrawAnimationSegmentClip(
                laneRect,
                element,
                new ActionEditorSelection(segmentsProp, i, ActionTimelineTrackKind.Animation),
                globalStart,
                globalEnd,
                totalFrames,
                ref selection,
                ref changed);
        }

        if (segmentsProp.arraySize == 0)
        {
            GUI.Label(
                new Rect(laneRect.x + 8f, laneRect.y + 4f, laneRect.width - 16f, laneRect.height - 8f),
                "双击或菜单添加 Animation Segment",
                EditorStyles.miniLabel);
        }
    }

    void ShowAnimationTrackMenu(SerializedObject so)
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Add Segment"), false, () =>
        {
            _pendingSelection = ActionTimelineCommands.AddAnimationSegment(so);
            _hasPendingSelection = true;
            _pendingRepaint = true;
        });
        menu.ShowAsContext();
    }

    /// <summary>绘制单段动画条块（只读展示全局帧范围；选中后右侧改 Clip）。</summary>
    void DrawAnimationSegmentClip(
        Rect laneRect,
        SerializedProperty element,
        ActionEditorSelection itemSelection,
        int globalStart,
        int globalEnd,
        int totalFrames,
        ref ActionEditorSelection selection,
        ref bool changed)
    {
        float x = laneRect.x + globalStart * _pixelsPerFrame;
        float width = Mathf.Max(_pixelsPerFrame, (globalEnd - globalStart + 1) * _pixelsPerFrame);
        Rect clipRect = new(x, laneRect.y + 3f, width, laneRect.height - 6f);

        bool selected = selection.Equals(itemSelection);
        Color color = selected
            ? ActionEditorStyles.ColorForSelectedTrack(ActionTimelineTrackKind.Animation)
            : ActionEditorStyles.ColorForTrack(ActionTimelineTrackKind.Animation);

        ActionEditorStyles.DrawRoundedWindowClip(clipRect, color, selected);

        var clip = element.FindPropertyRelative("clip").objectReferenceValue as AnimationClip;
        string label = clip != null ? clip.name : $"Segment {itemSelection.Index}";
        GUI.Label(clipRect, label, EditorStyles.miniLabel);

        Event evt = Event.current;
        if (evt.type != EventType.MouseDown || evt.button != 0 || !clipRect.Contains(evt.mousePosition))
            return;

        if (_dragMode != DragMode.None && _dragMode != DragMode.Scrub)
            return;

        selection = itemSelection;
        changed = true;
        evt.Use();
    }

    /// <summary>从 SerializedProperty 计算段贡献帧数（与 ActionAnimationSegment.GetFrameCount 对齐）。</summary>
    static int ResolveSegmentFrameCount(SerializedProperty element, float sampleRate)
    {
        var clip = element.FindPropertyRelative("clip").objectReferenceValue as AnimationClip;
        if (clip == null)
            return 0;

        float rate = sampleRate > 0f ? sampleRate : 30f;
        int clipLastFrame = Mathf.Max(0, Mathf.RoundToInt(clip.length * rate) - 1);
        int start = Mathf.Clamp(element.FindPropertyRelative("startFrame").intValue, 0, clipLastFrame);
        int endField = element.FindPropertyRelative("endFrame").intValue;
        int end = endField < 0 ? clipLastFrame : Mathf.Clamp(endField, start, clipLastFrame);
        return Mathf.Max(1, end - start + 1);
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
        // 轨道路面宽度铺满中栏剩余区域（与标尺同宽）。
        Rect laneRect = new(
            trackRect.x + ActionEditorStyles.TrackHeaderWidth,
            trackRect.y,
            Mathf.Max(1f, trackRect.width - ActionEditorStyles.TrackHeaderWidth),
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

        ActionEditorStyles.DrawRoundedWindowClip(clipRect, color, selected);
        // 用 Label 仅作绘制，不参与控件焦点，避免抢走拖拽事件。
        GUI.Label(clipRect, idProp != null ? idProp.stringValue : itemSelection.Kind.ToString(), EditorStyles.miniLabel);

        bool pointEvent = ActionEditorStyles.IsPointEventTrack(itemSelection.Kind);
        float edge = Mathf.Min(ActionEditorStyles.EdgeHandleWidth, width * 0.35f);
        Rect leftHandle = new(clipRect.x, clipRect.y, edge, clipRect.height);
        Rect rightHandle = new(clipRect.xMax - edge, clipRect.y, edge, clipRect.height);

        if (!pointEvent)
        {
            EditorGUIUtility.AddCursorRect(leftHandle, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(rightHandle, MouseCursor.ResizeHorizontal);
        }

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

        // 点事件只允许平移触发帧，禁止拉边改时长。
        if (!pointEvent && leftHandle.Contains(evt.mousePosition))
            _dragMode = DragMode.ResizeStart;
        else if (!pointEvent && rightHandle.Contains(evt.mousePosition))
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

        // 按每帧像素宽自适应刻度密度，避免铺满后文字挤成一团。
        int majorStep = _pixelsPerFrame >= 12f ? 5
            : _pixelsPerFrame >= 6f ? 10
            : _pixelsPerFrame >= 3f ? 20
            : 30;
        int minorStep = Mathf.Max(1, majorStep / 5);

        for (int f = 0; f <= totalFrames; f++)
        {
            if (f != 0 && f != totalFrames && f % minorStep != 0)
                continue;

            float x = rect.x + f * _pixelsPerFrame;
            bool major = f % majorStep == 0 || f == totalFrames;
            float h = major ? rect.height : rect.height * 0.4f;
            Handles.DrawLine(new Vector3(x, rect.yMax - h), new Vector3(x, rect.yMax));
            if (major)
                GUI.Label(new Rect(x + 2f, rect.y, 36f, 16f), f.ToString(), EditorStyles.miniLabel);
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
