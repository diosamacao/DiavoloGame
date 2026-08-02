using System.Collections.Generic;
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
        /// <summary>拖动 Animation 轨片段以调整 animationSegments 数组顺序。</summary>
        ReorderAnimation,
        /// <summary>拖动手动轨道头以调整 timeline.tracks 数组顺序。</summary>
        ReorderTrack,
    }

    Vector2 _scroll;
    DragMode _dragMode;
    ActionTimelineTrackKind _dragKind;
    int _dragIndex = -1;
    int _dragStartFrame;
    int _dragOriginalStart;
    int _dragOriginalEnd;
    int _dragControlId = -1;
    /// <summary>动画段换序：鼠标按下位置，用于区分点击选中与真正拖拽。</summary>
    Vector2 _reorderMouseDownPos;
    /// <summary>动画段换序：是否已超过拖拽阈值并开始改序。</summary>
    bool _reorderDragActivated;
    /// <summary>本帧绘制的 Animation 轨 lane，供换序命中与指示线使用。</summary>
    Rect _animationLaneRect;
    /// <summary>当前帧可见手动轨道的数组下标与矩形，用于纵向换序命中。</summary>
    readonly List<int> _drawnTrackIndices = new();
    readonly List<Rect> _drawnTrackRects = new();
    Vector2 _trackReorderMouseDownPos;
    bool _trackReorderActivated;
    int _trackReorderTargetIndex = -1;
    /// <summary>每帧像素宽；由可视宽度铺满值 × 缩放倍率得到。</summary>
    float _pixelsPerFrame = 8f;
    /// <summary>时间轴水平缩放：1 = 铺满可视区，&gt;1 可横向滚动以精确拖帧。</summary>
    float _zoom = ActionEditorStyles.TimelineZoomMin;
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

        // 顶栏：帧数 + Zoom + Add Track（动画段在默认 Animation 轨上展示）。
        const float topBarHeight = 22f;
        const float addTrackWidth = 90f;
        const float zoomLabelWidth = 40f;
        const float zoomSliderWidth = 110f;
        const float zoomValueWidth = 36f;
        Rect topBarRect = new(rect.x, rect.y, rect.width, topBarHeight);
        Rect addTrackRect = new(topBarRect.xMax - addTrackWidth - 4f, topBarRect.y + 1f, addTrackWidth, 20f);
        Rect zoomValueRect = new(addTrackRect.x - zoomValueWidth - 4f, topBarRect.y + 1f, zoomValueWidth, 20f);
        Rect zoomSliderRect = new(zoomValueRect.x - zoomSliderWidth - 4f, topBarRect.y + 2f, zoomSliderWidth, 18f);
        Rect zoomLabelRect = new(zoomSliderRect.x - zoomLabelWidth - 2f, topBarRect.y + 1f, zoomLabelWidth, 20f);

        int totalFrames = Mathf.Max(1, so.FindProperty("totalFrames")?.intValue ?? action.TotalFrames);
        GUI.Label(
            new Rect(topBarRect.x + 8f, topBarRect.y + 1f, 160f, 20f),
            $"{totalFrames} frames",
            EditorStyles.miniLabel);

        GUI.Label(zoomLabelRect, new GUIContent("Zoom", "Ctrl/Cmd + 滚轮亦可缩放"), EditorStyles.miniLabel);
        EditorGUI.BeginChangeCheck();
        _zoom = GUI.HorizontalSlider(
            zoomSliderRect,
            _zoom,
            ActionEditorStyles.TimelineZoomMin,
            ActionEditorStyles.TimelineZoomMax);
        if (EditorGUI.EndChangeCheck())
            _pendingRepaint = true;
        GUI.Label(zoomValueRect, $"{_zoom:0.0}x", EditorStyles.miniLabel);

        if (GUI.Button(addTrackRect, "Add Track"))
            onAddTrack?.Invoke();

        Rect bodyRect = new(rect.x, rect.y + topBarHeight + 2f, rect.width, rect.height - topBarHeight - 2f);
        HandleTimelineZoomScroll(bodyRect, totalFrames, previewFrame);

        const float scrollBarSize = 14f;
        bool needsVerticalScroll = contentHeight > bodyRect.height;
        float usableWidth = Mathf.Max(1f, bodyRect.width - (needsVerticalScroll ? scrollBarSize : 0f));
        float fitLaneWidth = Mathf.Max(1f, usableWidth - ActionEditorStyles.TrackHeaderWidth);
        float fitPixelsPerFrame = fitLaneWidth / totalFrames;
        _pixelsPerFrame = fitPixelsPerFrame * Mathf.Max(ActionEditorStyles.TimelineZoomMin, _zoom);
        float contentLaneWidth = totalFrames * _pixelsPerFrame;
        float contentWidth = ActionEditorStyles.TrackHeaderWidth + contentLaneWidth;
        bool needsHorizontalScroll = contentWidth > usableWidth + 0.5f;
        // 横向滚动条占高时重新评估纵向是否溢出，避免铺满缩放时误留空白条。
        float usableHeight = Mathf.Max(1f, bodyRect.height - (needsHorizontalScroll ? scrollBarSize : 0f));
        needsVerticalScroll = contentHeight > usableHeight;

        _scroll = GUI.BeginScrollView(
            bodyRect,
            _scroll,
            new Rect(0f, 0f, contentWidth, contentHeight),
            needsHorizontalScroll,
            needsVerticalScroll);

        EditorGUI.DrawRect(
            new Rect(0f, 0f, ActionEditorStyles.TrackHeaderWidth, ActionEditorStyles.RulerHeight),
            ActionEditorStyles.PanelHeader);
        GUI.Label(
            new Rect(4f, 2f, ActionEditorStyles.TrackHeaderWidth - 8f, ActionEditorStyles.RulerHeight - 4f),
            "Tracks",
            EditorStyles.miniLabel);

        Rect rulerRect = new(ActionEditorStyles.TrackHeaderWidth, 0f, contentLaneWidth, ActionEditorStyles.RulerHeight);
        DrawRuler(rulerRect, totalFrames);
        HandleRulerInput(rulerRect, totalFrames, ref previewFrame, ref changed);

        float y = ActionEditorStyles.RulerHeight + 2f;

        // 默认动画轨：始终置顶，展示 animationSegments。
        Rect animationTrackRect = new(0f, y, contentWidth, ActionEditorStyles.TrackHeight);
        DrawAnimationTrack(animationTrackRect, so, action, totalFrames, ref selection, ref changed);
        y += ActionEditorStyles.TrackHeight + 2f;

        if (trackCount == 0)
            DrawEmptyTracksHint(new Rect(ActionEditorStyles.TrackHeaderWidth + 8f, y, Mathf.Max(200f, contentLaneWidth - 16f), 72f));

        _drawnTrackIndices.Clear();
        _drawnTrackRects.Clear();
        for (int trackIndex = 0; trackIndex < trackCount; trackIndex++)
        {
            SerializedProperty trackProp = tracksProp.GetArrayElementAtIndex(trackIndex);
            if (!trackProp.FindPropertyRelative("visible").boolValue)
                continue;

            Rect trackRect = new(0f, y, contentWidth, ActionEditorStyles.TrackHeight);
            DrawTrack(trackRect, so, action, trackProp, trackIndex, totalFrames, ref selection, previewFrame, ref changed);
            y += ActionEditorStyles.TrackHeight + 2f;
        }

        if (ProcessActiveTrackReorder(so, ref changed))
            changed = true;

        // 拖拽期间在 ScrollView 内统一处理，避免依赖每帧重建的 SerializedProperty 引用。
        if (ProcessActiveAnimationReorder(so, action, ref selection, ref changed))
            changed = true;

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

    /// <summary>绘制默认 Animation 轨：按全局帧铺开各 animationSegments；支持拖拽改序。</summary>
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
        _animationLaneRect = laneRect;

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
                ref selection,
                ref changed);
        }

        if (_dragMode == DragMode.ReorderAnimation && _reorderDragActivated)
            DrawAnimationReorderGhost(laneRect, segmentsProp, sampleRate, evt.mousePosition.x);

        if (segmentsProp.arraySize == 0)
        {
            GUI.Label(
                new Rect(laneRect.x + 8f, laneRect.y + 4f, laneRect.width - 16f, laneRect.height - 8f),
                "双击或菜单添加 Animation Segment；拖动片段可调整顺序",
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

    /// <summary>绘制单段动画条块；左键拖拽改序，选中后右侧改 Clip。</summary>
    void DrawAnimationSegmentClip(
        Rect laneRect,
        SerializedProperty element,
        ActionEditorSelection itemSelection,
        int globalStart,
        int globalEnd,
        ref ActionEditorSelection selection,
        ref bool changed)
    {
        float x = laneRect.x + globalStart * _pixelsPerFrame;
        float width = Mathf.Max(_pixelsPerFrame, (globalEnd - globalStart + 1) * _pixelsPerFrame);
        Rect clipRect = new(x, laneRect.y + 3f, width, laneRect.height - 6f);

        bool selected = selection.Equals(itemSelection);
        // 换序拖拽中：源片段半透明，提示正在移动。
        bool dimSource = _dragMode == DragMode.ReorderAnimation
            && _reorderDragActivated
            && itemSelection.Index == _dragIndex;
        Color color = selected
            ? ActionEditorStyles.ColorForSelectedTrack(ActionTimelineTrackKind.Animation)
            : ActionEditorStyles.ColorForTrack(ActionTimelineTrackKind.Animation);
        if (dimSource)
            color.a *= 0.35f;

        ActionEditorStyles.DrawRoundedWindowClip(clipRect, color, selected && !dimSource);

        var clip = element.FindPropertyRelative("clip").objectReferenceValue as AnimationClip;
        string label = clip != null ? clip.name : $"Segment {itemSelection.Index}";
        GUI.Label(clipRect, label, EditorStyles.miniLabel);
        EditorGUIUtility.AddCursorRect(clipRect, MouseCursor.MoveArrow);

        Event evt = Event.current;
        if (evt.type != EventType.MouseDown || evt.button != 0 || !clipRect.Contains(evt.mousePosition))
            return;

        if (_dragMode != DragMode.None && _dragMode != DragMode.Scrub)
            return;

        selection = itemSelection;
        _dragMode = DragMode.ReorderAnimation;
        _dragKind = ActionTimelineTrackKind.Animation;
        _dragIndex = itemSelection.Index;
        _reorderMouseDownPos = evt.mousePosition;
        _reorderDragActivated = false;
        _dragControlId = GUIUtility.GetControlID(FocusType.Passive);
        GUIUtility.hotControl = _dragControlId;
        changed = true;
        evt.Use();
    }

    /// <summary>换序拖拽时在鼠标处绘制半透明幽灵条，指示落点。</summary>
    void DrawAnimationReorderGhost(
        Rect laneRect,
        SerializedProperty segmentsProp,
        float sampleRate,
        float mouseX)
    {
        if (segmentsProp == null || _dragIndex < 0 || _dragIndex >= segmentsProp.arraySize)
            return;

        SerializedProperty element = segmentsProp.GetArrayElementAtIndex(_dragIndex);
        int frameCount = ResolveSegmentFrameCount(element, sampleRate);
        if (frameCount <= 0)
            return;

        float width = Mathf.Max(_pixelsPerFrame, frameCount * _pixelsPerFrame);
        float x = Mathf.Clamp(mouseX - width * 0.5f, laneRect.x, laneRect.xMax - width);
        Rect ghostRect = new(x, laneRect.y + 3f, width, laneRect.height - 6f);

        Color ghost = ActionEditorStyles.ColorForSelectedTrack(ActionTimelineTrackKind.Animation);
        ghost.a = 0.55f;
        ActionEditorStyles.DrawRoundedWindowClip(ghostRect, ghost, true);

        var clip = element.FindPropertyRelative("clip").objectReferenceValue as AnimationClip;
        string label = clip != null ? clip.name : $"Segment {_dragIndex}";
        GUI.Label(ghostRect, label, EditorStyles.miniLabel);

        // 落点指示线：按鼠标位置计算目标下标后画在目标槽左侧。
        int targetIndex = ResolveAnimationReorderTargetIndex(segmentsProp, sampleRate, mouseX, _dragIndex);
        float indicatorX = GetAnimationSegmentLeftX(laneRect, segmentsProp, sampleRate, targetIndex, _dragIndex);
        Handles.BeginGUI();
        Handles.color = new Color(1f, 0.9f, 0.35f, 0.95f);
        Handles.DrawLine(
            new Vector3(indicatorX, laneRect.y + 1f),
            new Vector3(indicatorX, laneRect.yMax - 1f));
        Handles.EndGUI();
    }

    /// <summary>
    /// 处理 Animation 段换序拖拽：超过像素阈值后按鼠标 X 相对各段中点决定目标下标并 MoveArrayElement。
    /// </summary>
    bool ProcessActiveAnimationReorder(
        SerializedObject so,
        ActionDefinition action,
        ref ActionEditorSelection selection,
        ref bool changed)
    {
        if (_dragMode != DragMode.ReorderAnimation || _dragIndex < 0)
            return false;

        Event evt = Event.current;
        if (evt.type != EventType.MouseDrag && evt.type != EventType.MouseUp && evt.type != EventType.Ignore)
            return false;

        if (_dragControlId >= 0 && GUIUtility.hotControl != _dragControlId && evt.type != EventType.MouseUp)
        {
            EndWindowDrag();
            return false;
        }

        SerializedProperty segmentsProp = so.FindProperty("animationSegments");
        if (segmentsProp == null)
        {
            EndWindowDrag();
            return false;
        }

        if (evt.type == EventType.MouseDrag)
        {
            bool reorderChanged = false;
            if (!_reorderDragActivated
                && (evt.mousePosition - _reorderMouseDownPos).sqrMagnitude >= 16f)
            {
                _reorderDragActivated = true;
            }

            if (_reorderDragActivated)
            {
                // 邻段中点穿越：一次最多与左右邻交换，避免整表重算导致抖动。
                int targetIndex = ResolveAnimationNeighborReorderTarget(
                    segmentsProp,
                    action.SampleRate,
                    evt.mousePosition.x,
                    _dragIndex);

                if (targetIndex != _dragIndex)
                {
                    ActionEditorSelection reordered =
                        ActionTimelineCommands.ReorderAnimationSegment(so, _dragIndex, targetIndex);
                    if (reordered.IsValid)
                    {
                        _dragIndex = reordered.Index;
                        selection = reordered;
                        reorderChanged = true;
                        changed = true;
                    }
                }

                // 幽灵条需要每帧重绘，但不代表数据一定变更。
                _pendingRepaint = true;
            }

            evt.Use();
            return reorderChanged;
        }

        // MouseUp / Ignore：结束换序。
        EndWindowDrag();
        _pendingRepaint = true;
        evt.Use();
        return false;
    }

    /// <summary>
    /// 相对当前拖拽段的左右邻段中点决定是否互换；无 Clip 空段跳过。
    /// </summary>
    int ResolveAnimationNeighborReorderTarget(
        SerializedProperty segmentsProp,
        float sampleRate,
        float mouseX,
        int draggedIndex)
    {
        int size = segmentsProp.arraySize;
        if (draggedIndex < 0 || draggedIndex >= size)
            return draggedIndex;

        if (!TryGetAnimationSegmentMidX(segmentsProp, sampleRate, draggedIndex, out float draggedMid))
            return draggedIndex;

        int prevIndex = FindPreviousDrawableSegmentIndex(segmentsProp, sampleRate, draggedIndex);
        if (prevIndex >= 0
            && TryGetAnimationSegmentMidX(segmentsProp, sampleRate, prevIndex, out float prevMid)
            && mouseX < (prevMid + draggedMid) * 0.5f)
        {
            return prevIndex;
        }

        int nextIndex = FindNextDrawableSegmentIndex(segmentsProp, sampleRate, draggedIndex);
        if (nextIndex >= 0
            && TryGetAnimationSegmentMidX(segmentsProp, sampleRate, nextIndex, out float nextMid)
            && mouseX > (draggedMid + nextMid) * 0.5f)
        {
            return nextIndex;
        }

        return draggedIndex;
    }

    /// <summary>幽灵条落点指示：仍按全局中点扫描给出目标下标（仅用于绘制）。</summary>
    int ResolveAnimationReorderTargetIndex(
        SerializedProperty segmentsProp,
        float sampleRate,
        float mouseX,
        int draggedIndex)
    {
        int size = segmentsProp.arraySize;
        if (size <= 1)
            return Mathf.Clamp(draggedIndex, 0, Mathf.Max(0, size - 1));

        int cursor = 0;
        int target = size - 1;
        for (int i = 0; i < size; i++)
        {
            int frameCount = ResolveSegmentFrameCount(segmentsProp.GetArrayElementAtIndex(i), sampleRate);
            if (frameCount <= 0)
                continue;

            float startX = _animationLaneRect.x + cursor * _pixelsPerFrame;
            float midX = startX + frameCount * _pixelsPerFrame * 0.5f;
            if (mouseX < midX)
            {
                target = i;
                break;
            }

            cursor += frameCount;
        }

        return Mathf.Clamp(target, 0, size - 1);
    }

    /// <summary>计算换序指示线 X：目标下标左侧。</summary>
    float GetAnimationSegmentLeftX(
        Rect laneRect,
        SerializedProperty segmentsProp,
        float sampleRate,
        int targetIndex,
        int draggedIndex)
    {
        int cursor = 0;
        for (int i = 0; i < segmentsProp.arraySize; i++)
        {
            if (i == targetIndex)
                break;

            if (i == draggedIndex && draggedIndex < targetIndex)
                continue;

            int frameCount = ResolveSegmentFrameCount(segmentsProp.GetArrayElementAtIndex(i), sampleRate);
            if (frameCount > 0)
                cursor += frameCount;
        }

        return laneRect.x + cursor * _pixelsPerFrame;
    }

    static int FindPreviousDrawableSegmentIndex(SerializedProperty segmentsProp, float sampleRate, int fromIndex)
    {
        for (int i = fromIndex - 1; i >= 0; i--)
        {
            if (ResolveSegmentFrameCount(segmentsProp.GetArrayElementAtIndex(i), sampleRate) > 0)
                return i;
        }

        return -1;
    }

    static int FindNextDrawableSegmentIndex(SerializedProperty segmentsProp, float sampleRate, int fromIndex)
    {
        for (int i = fromIndex + 1; i < segmentsProp.arraySize; i++)
        {
            if (ResolveSegmentFrameCount(segmentsProp.GetArrayElementAtIndex(i), sampleRate) > 0)
                return i;
        }

        return -1;
    }

    /// <summary>返回指定段在 Animation 轨上的中心 X；无帧贡献时失败。</summary>
    bool TryGetAnimationSegmentMidX(
        SerializedProperty segmentsProp,
        float sampleRate,
        int index,
        out float midX)
    {
        midX = 0f;
        int cursor = 0;
        for (int i = 0; i < segmentsProp.arraySize; i++)
        {
            int frameCount = ResolveSegmentFrameCount(segmentsProp.GetArrayElementAtIndex(i), sampleRate);
            if (frameCount <= 0)
                continue;

            if (i == index)
            {
                midX = _animationLaneRect.x + (cursor + frameCount * 0.5f) * _pixelsPerFrame;
                return true;
            }

            cursor += frameCount;
        }

        return false;
    }

    /// <summary>从 SerializedProperty 计算段贡献帧数（与 ActionAnimationSegment.GetFrameCount 对齐）。</summary>
    static int ResolveSegmentFrameCount(SerializedProperty element, float sampleRate)
    {
        var clip = element.FindPropertyRelative("clip").objectReferenceValue as AnimationClip;
        if (clip == null)
            return 0;

        float rate = sampleRate > 0f ? sampleRate : ActionSim.LogicHz;
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
        _drawnTrackIndices.Add(trackIndex);
        _drawnTrackRects.Add(trackRect);

        Rect headerRect = new(trackRect.x, trackRect.y, ActionEditorStyles.TrackHeaderWidth, trackRect.height);
        // 轨道路面宽度铺满中栏剩余区域（与标尺同宽）。
        Rect laneRect = new(
            trackRect.x + ActionEditorStyles.TrackHeaderWidth,
            trackRect.y,
            Mathf.Max(1f, trackRect.width - ActionEditorStyles.TrackHeaderWidth),
            trackRect.height);

        EditorGUI.DrawRect(headerRect, new Color(0.22f, 0.22f, 0.25f, 1f));
        EditorGUI.DrawRect(laneRect, ActionEditorStyles.Background);

        Rect reorderHandleRect = new(headerRect.x + 2f, headerRect.y + 4f, 18f, headerRect.height - 8f);
        GUI.Label(reorderHandleRect, "≡", EditorStyles.miniLabel);
        EditorGUIUtility.AddCursorRect(reorderHandleRect, MouseCursor.ResizeVertical);

        Rect nameRect = new(headerRect.x + 22f, headerRect.y + 4f, headerRect.width - 46f, headerRect.height - 8f);
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
            && evt.button == 0
            && reorderHandleRect.Contains(evt.mousePosition)
            && _dragMode == DragMode.None)
        {
            _dragMode = DragMode.ReorderTrack;
            _dragIndex = trackIndex;
            _trackReorderTargetIndex = trackIndex;
            _trackReorderMouseDownPos = evt.mousePosition;
            _trackReorderActivated = false;
            _dragControlId = GUIUtility.GetControlID(FocusType.Passive);
            GUIUtility.hotControl = _dragControlId;
            evt.Use();
            return;
        }

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

    /// <summary>处理轨头纵向拖拽；松开时一次性 MoveArrayElement，并绘制黄色插入线。</summary>
    bool ProcessActiveTrackReorder(SerializedObject so, ref bool changed)
    {
        if (_dragMode != DragMode.ReorderTrack || _dragIndex < 0)
            return false;

        Event evt = Event.current;
        if (_trackReorderActivated && evt.type == EventType.Repaint)
            DrawTrackReorderIndicator(_trackReorderTargetIndex, evt.mousePosition.y);

        if (evt.type != EventType.MouseDrag
            && evt.type != EventType.MouseUp
            && evt.type != EventType.Ignore)
        {
            return false;
        }

        if (_dragControlId >= 0
            && GUIUtility.hotControl != _dragControlId
            && evt.type != EventType.MouseUp)
        {
            EndWindowDrag();
            return false;
        }

        if (evt.type == EventType.MouseDrag)
        {
            if (!_trackReorderActivated
                && (evt.mousePosition - _trackReorderMouseDownPos).sqrMagnitude >= 16f)
            {
                _trackReorderActivated = true;
            }

            if (_trackReorderActivated)
                _trackReorderTargetIndex = ResolveTrackReorderTarget(evt.mousePosition.y);

            _pendingRepaint = true;
            evt.Use();
            return false;
        }

        if (_trackReorderActivated
            && _trackReorderTargetIndex >= 0
            && ActionTimelineCommands.ReorderTrack(so, _dragIndex, _trackReorderTargetIndex))
        {
            changed = true;
        }

        EndWindowDrag();
        _pendingRepaint = true;
        evt.Use();
        return changed;
    }

    /// <summary>按鼠标纵坐标与可见轨道中点解析目标数组下标。</summary>
    int ResolveTrackReorderTarget(float mouseY)
    {
        if (_drawnTrackIndices.Count == 0)
            return _dragIndex;

        int target = _drawnTrackIndices[_drawnTrackIndices.Count - 1];
        for (int i = 0; i < _drawnTrackRects.Count; i++)
        {
            if (mouseY < _drawnTrackRects[i].center.y)
            {
                target = _drawnTrackIndices[i];
                break;
            }
        }

        return target;
    }

    /// <summary>在当前目标轨道上方或下方绘制换序落点。</summary>
    void DrawTrackReorderIndicator(int targetIndex, float mouseY)
    {
        int visibleIndex = _drawnTrackIndices.IndexOf(targetIndex);
        if (visibleIndex < 0 || visibleIndex >= _drawnTrackRects.Count)
            return;

        Rect targetRect = _drawnTrackRects[visibleIndex];
        float y = mouseY < targetRect.center.y ? targetRect.yMin : targetRect.yMax;
        Handles.BeginGUI();
        Handles.color = new Color(1f, 0.85f, 0.2f, 1f);
        Handles.DrawAAPolyLine(
            3f,
            new Vector3(targetRect.xMin, y),
            new Vector3(targetRect.xMax, y));
        Handles.EndGUI();
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

    /// <summary>绘制窗口条块或点事件菱形；仅在 MouseDown 时开始拖拽，实际改帧在 ProcessActiveWindowDrag。</summary>
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
        bool pointEvent = ActionEditorStyles.IsPointEventTrack(itemSelection.Kind);
        bool selected = selection.Equals(itemSelection);
        Color color = selected
            ? ActionEditorStyles.ColorForSelectedTrack(itemSelection.Kind)
            : ActionEditorStyles.ColorForTrack(itemSelection.Kind);

        // 点事件用菱形热区，避免 1 帧宽条难以点选；区间窗仍用圆角条块。
        Rect hitRect;
        if (pointEvent)
        {
            hitRect = ActionEditorStyles.GetPointEventDiamondRect(laneRect, start, _pixelsPerFrame);
            ActionEditorStyles.DrawPointEventDiamond(hitRect, color, selected);
            EditorGUIUtility.AddCursorRect(hitRect, MouseCursor.MoveArrow);
        }
        else
        {
            float x = laneRect.x + start * _pixelsPerFrame;
            float width = Mathf.Max(_pixelsPerFrame, (end - start + 1) * _pixelsPerFrame);
            hitRect = new Rect(x, laneRect.y + 3f, width, laneRect.height - 6f);
            ActionEditorStyles.DrawRoundedWindowClip(hitRect, color, selected);

            // 用 Label 仅作绘制，不参与控件焦点，避免抢走拖拽事件。
            string clipLabel = idProp != null ? idProp.stringValue : itemSelection.Kind.ToString();
            if (itemSelection.Kind == ActionTimelineTrackKind.Cancel)
            {
                SerializedProperty typeProp = element.FindPropertyRelative("windowType");
                if (typeProp != null)
                    clipLabel = $"{typeProp.enumDisplayNames[typeProp.enumValueIndex]} · {clipLabel}";
            }

            GUI.Label(hitRect, clipLabel, EditorStyles.miniLabel);

            float edge = Mathf.Min(ActionEditorStyles.EdgeHandleWidth, hitRect.width * 0.35f);
            Rect leftHandle = new(hitRect.x, hitRect.y, edge, hitRect.height);
            Rect rightHandle = new(hitRect.xMax - edge, hitRect.y, edge, hitRect.height);
            EditorGUIUtility.AddCursorRect(leftHandle, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(rightHandle, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(hitRect, MouseCursor.MoveArrow);
        }

        Event evt = Event.current;
        if (evt.type != EventType.MouseDown || evt.button != 0 || !hitRect.Contains(evt.mousePosition))
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

        // 点事件只允许平移触发帧；区间窗可命中左右边柄改时长。
        if (!pointEvent)
        {
            float edge = Mathf.Min(ActionEditorStyles.EdgeHandleWidth, hitRect.width * 0.35f);
            Rect leftHandle = new(hitRect.x, hitRect.y, edge, hitRect.height);
            Rect rightHandle = new(hitRect.xMax - edge, hitRect.y, edge, hitRect.height);
            if (leftHandle.Contains(evt.mousePosition))
                _dragMode = DragMode.ResizeStart;
            else if (rightHandle.Contains(evt.mousePosition))
                _dragMode = DragMode.ResizeEnd;
            else
                _dragMode = DragMode.Move;
        }
        else
            _dragMode = DragMode.Move;

        changed = true;
        evt.Use();
    }

    /// <summary>Ctrl/Cmd + 滚轮缩放时间轴；以预览帧为锚，尽量保持 playhead 可见。</summary>
    void HandleTimelineZoomScroll(Rect bodyRect, int totalFrames, int previewFrame)
    {
        Event evt = Event.current;
        if (evt.type != EventType.ScrollWheel || !bodyRect.Contains(evt.mousePosition))
            return;

        bool zoomModifier = evt.control || evt.command;
        if (!zoomModifier)
            return;

        float oldZoom = _zoom;
        float factor = evt.delta.y > 0f ? 0.9f : 1.1f;
        _zoom = Mathf.Clamp(
            _zoom * factor,
            ActionEditorStyles.TimelineZoomMin,
            ActionEditorStyles.TimelineZoomMax);

        if (!Mathf.Approximately(oldZoom, _zoom))
        {
            // 缩放后把预览帧大致保持在视口中部附近。
            float fitLaneWidth = Mathf.Max(1f, bodyRect.width - ActionEditorStyles.TrackHeaderWidth);
            float newPpf = (fitLaneWidth / Mathf.Max(1, totalFrames)) * _zoom;
            float playheadX = previewFrame * newPpf;
            _scroll.x = Mathf.Max(0f, playheadX - fitLaneWidth * 0.5f);
            _pendingRepaint = true;
        }

        evt.Use();
    }

    /// <summary>在持有 hotControl 期间处理窗口平移/缩放；用 Kind+Index 重新取属性。</summary>
    bool ProcessActiveWindowDrag(SerializedObject so, int totalFrames, ref bool changed)
    {
        // Animation 片段与轨道换序分别由专用处理器消费。
        if (_dragMode is DragMode.None
            or DragMode.Scrub
            or DragMode.ReorderAnimation
            or DragMode.ReorderTrack
            || _dragIndex < 0)
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
        _reorderDragActivated = false;
        _reorderMouseDownPos = default;
        _trackReorderActivated = false;
        _trackReorderMouseDownPos = default;
        _trackReorderTargetIndex = -1;
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

        // 窗口/动画段拖拽进行中时不处理标尺。
        if (_dragMode is DragMode.Move
            or DragMode.ResizeStart
            or DragMode.ResizeEnd
            or DragMode.ReorderAnimation
            or DragMode.ReorderTrack)
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

    /// <summary>
    /// Delete 删除当前选中窗口；正在 Inspector 文本框编辑时忽略，避免改 Id 时 Backspace 误删整段窗口。
    /// </summary>
    void HandleDeleteKey(SerializedObject so, ref ActionEditorSelection selection, ref bool changed)
    {
        Event evt = Event.current;
        if (evt.type != EventType.KeyDown)
            return;

        // Backspace 留给文本编辑；仅 Delete 删除时间轴窗口。
        if (evt.keyCode != KeyCode.Delete)
            return;

        // 右侧详情/任意文本字段获得焦点时，不拦截按键。
        if (EditorGUIUtility.editingTextField)
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
