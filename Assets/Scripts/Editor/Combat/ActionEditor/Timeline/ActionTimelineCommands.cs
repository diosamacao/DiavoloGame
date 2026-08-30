using UnityEditor;
using UnityEngine;

/// <summary>时间轴增删改命令；全部经 Undo + SerializedProperty 写回。</summary>
public static class ActionTimelineCommands
{
    /// <summary>按类型解析窗口所在数组属性名。</summary>
    public static string GetArrayPropertyName(ActionTimelineTrackKind kind) => kind switch
    {
        ActionTimelineTrackKind.Hitbox => "hitboxStates",
        ActionTimelineTrackKind.Hurtbox => "hurtboxStates",
        ActionTimelineTrackKind.Vfx => "playVfxNotifies",
        ActionTimelineTrackKind.Sfx => "playSfxStates",
        ActionTimelineTrackKind.Cancel => "cancelWindowStates",
        ActionTimelineTrackKind.Phase => "phaseStates",
        ActionTimelineTrackKind.Movement => "movementStates",
        ActionTimelineTrackKind.Rotation => "rotationStates",
        ActionTimelineTrackKind.Event => "actionEvents",
        ActionTimelineTrackKind.Animation => null,
        ActionTimelineTrackKind.PerfectDodgeWindow => "perfectDodgeWindowStates",
        ActionTimelineTrackKind.MotionModifier => "motionModifierStates",
        ActionTimelineTrackKind.MotionCommand => "motionCommandNotifies",
        ActionTimelineTrackKind.Camera => "cameraShotStates",
        _ => null,
    };

    /// <summary>
    /// 若 tracks 为空但已有窗口，按窗口 trackName 补建轨道（迁移旧资产，不铺满空类型轨）。
    /// </summary>
    public static void EnsureTracksFromWindows(SerializedObject so)
    {
        SerializedProperty tracksProp = so.FindProperty("timeline.tracks");
        if (tracksProp == null || tracksProp.arraySize > 0)
            return;

        bool anyAdded = false;
        anyAdded |= AppendMissingTracks(so, tracksProp, ActionTimelineTrackKind.Hitbox);
        anyAdded |= AppendMissingTracks(so, tracksProp, ActionTimelineTrackKind.Hurtbox);
        anyAdded |= AppendMissingTracks(so, tracksProp, ActionTimelineTrackKind.Vfx);
        anyAdded |= AppendMissingTracks(so, tracksProp, ActionTimelineTrackKind.Sfx);
        anyAdded |= AppendMissingTracks(so, tracksProp, ActionTimelineTrackKind.Cancel);
        anyAdded |= AppendMissingTracks(so, tracksProp, ActionTimelineTrackKind.Phase);
        anyAdded |= AppendMissingTracks(so, tracksProp, ActionTimelineTrackKind.Movement);
        anyAdded |= AppendMissingTracks(so, tracksProp, ActionTimelineTrackKind.Rotation);
        anyAdded |= AppendMissingTracks(so, tracksProp, ActionTimelineTrackKind.Event);
        anyAdded |= AppendMissingTracks(so, tracksProp, ActionTimelineTrackKind.PerfectDodgeWindow);
        anyAdded |= AppendMissingTracks(so, tracksProp, ActionTimelineTrackKind.MotionModifier);
        anyAdded |= AppendMissingTracks(so, tracksProp, ActionTimelineTrackKind.MotionCommand);
        anyAdded |= AppendMissingTracks(so, tracksProp, ActionTimelineTrackKind.Camera);

        if (!anyAdded)
            return;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(so.targetObject);
    }

    static bool AppendMissingTracks(
        SerializedObject so,
        SerializedProperty tracksProp,
        ActionTimelineTrackKind kind)
    {
        string arrayName = GetArrayPropertyName(kind);
        if (arrayName == null)
            return false;

        SerializedProperty arrayProp = so.FindProperty($"timeline.{arrayName}");
        if (arrayProp == null || arrayProp.arraySize == 0)
            return false;

        var names = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            SerializedProperty nameProp = arrayProp.GetArrayElementAtIndex(i).FindPropertyRelative("trackName");
            string name = nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue)
                ? nameProp.stringValue
                : "Default";
            names.Add(name);
        }

        if (names.Count == 0)
            return false;

        Undo.RecordObject(so.targetObject, "Sync Action Tracks");
        foreach (string name in names)
        {
            int index = tracksProp.arraySize;
            tracksProp.arraySize++;
            SerializedProperty track = tracksProp.GetArrayElementAtIndex(index);
            track.FindPropertyRelative("trackName").stringValue = name;
            track.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            track.FindPropertyRelative("visible").boolValue = true;
        }

        return true;
    }

    /// <summary>添加一条空轨并写入默认 trackName；仅默认 Animation 轨不可手动添加。</summary>
    public static void AddTrack(SerializedObject so, ActionTimelineTrackKind kind)
    {
        if (kind == ActionTimelineTrackKind.Animation)
            return;

        SerializedProperty tracksProp = so.FindProperty("timeline.tracks");
        if (tracksProp == null)
            return;

        Undo.RecordObject(so.targetObject, "Add Action Track");
        string defaultName = $"{ActionEditorStyles.DisplayName(kind)}_{CountTracksOfKind(tracksProp, kind) + 1}";
        AppendTrack(tracksProp, kind, defaultName);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(so.targetObject);
    }

    /// <summary>粘贴时确保存在指定 kind+trackName 的轨道；已存在则不改。</summary>
    public static void EnsureTrack(SerializedObject so, ActionTimelineTrackKind kind, string trackName)
    {
        if (so == null || kind == ActionTimelineTrackKind.Animation)
            return;

        SerializedProperty tracksProp = so.FindProperty("timeline.tracks");
        if (tracksProp == null)
            return;

        string name = string.IsNullOrEmpty(trackName) ? "Default" : trackName;
        for (int i = 0; i < tracksProp.arraySize; i++)
        {
            SerializedProperty track = tracksProp.GetArrayElementAtIndex(i);
            if (track.FindPropertyRelative("kind").enumValueIndex != (int)kind)
                continue;
            if (track.FindPropertyRelative("trackName").stringValue == name)
                return;
        }

        AppendTrack(tracksProp, kind, name);
    }

    static void AppendTrack(SerializedProperty tracksProp, ActionTimelineTrackKind kind, string trackName)
    {
        int index = tracksProp.arraySize;
        tracksProp.arraySize++;
        SerializedProperty track = tracksProp.GetArrayElementAtIndex(index);
        track.FindPropertyRelative("trackName").stringValue = trackName;
        track.FindPropertyRelative("kind").enumValueIndex = (int)kind;
        track.FindPropertyRelative("visible").boolValue = true;
    }

    /// <summary>删除多个窗口；按 Kind 分组并从高下标到低下标删除，避免索引错位。</summary>
    public static void RemoveWindows(SerializedObject so, ActionEditorSelectionSet selection)
    {
        if (so == null || selection == null || !selection.HasSelection)
            return;

        var byKind = new System.Collections.Generic.Dictionary<ActionTimelineTrackKind, System.Collections.Generic.List<int>>();
        for (int i = 0; i < selection.Items.Count; i++)
        {
            ActionEditorSelection item = selection.Items[i];
            if (!item.IsValid)
                continue;

            if (!byKind.TryGetValue(item.Kind, out System.Collections.Generic.List<int> indices))
            {
                indices = new System.Collections.Generic.List<int>();
                byKind.Add(item.Kind, indices);
            }

            if (!indices.Contains(item.Index))
                indices.Add(item.Index);
        }

        if (byKind.Count == 0)
            return;

        Undo.RecordObject(so.targetObject, "Remove Action Windows");
        foreach (System.Collections.Generic.KeyValuePair<ActionTimelineTrackKind, System.Collections.Generic.List<int>> pair in byKind)
        {
            string arrayName = pair.Key == ActionTimelineTrackKind.Animation
                ? null
                : GetArrayPropertyName(pair.Key);
            SerializedProperty arrayProp = pair.Key == ActionTimelineTrackKind.Animation
                ? so.FindProperty("animationSegments")
                : arrayName != null ? so.FindProperty($"timeline.{arrayName}") : null;
            if (arrayProp == null)
                continue;

            pair.Value.Sort((a, b) => b.CompareTo(a));
            for (int i = 0; i < pair.Value.Count; i++)
            {
                int index = pair.Value[i];
                if (index >= 0 && index < arrayProp.arraySize)
                    arrayProp.DeleteArrayElementAtIndex(index);
            }
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(so.targetObject);
    }

    /// <summary>删除指定下标轨道；同名窗口一并删除。</summary>
    public static void RemoveTrack(SerializedObject so, int trackIndex)
    {
        SerializedProperty tracksProp = so.FindProperty("timeline.tracks");
        if (tracksProp == null || trackIndex < 0 || trackIndex >= tracksProp.arraySize)
            return;

        SerializedProperty track = tracksProp.GetArrayElementAtIndex(trackIndex);
        string trackName = track.FindPropertyRelative("trackName").stringValue;
        var kind = (ActionTimelineTrackKind)track.FindPropertyRelative("kind").enumValueIndex;

        Undo.RecordObject(so.targetObject, "Remove Action Track");
        RemoveWindowsOnTrack(so, kind, trackName);
        tracksProp.DeleteArrayElementAtIndex(trackIndex);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(so.targetObject);
    }

    /// <summary>调整手动轨道顺序；窗口仍按 trackName 归属，不修改任何窗口数据。</summary>
    public static bool ReorderTrack(SerializedObject so, int fromIndex, int toIndex)
    {
        SerializedProperty tracksProp = so.FindProperty("timeline.tracks");
        if (tracksProp == null
            || fromIndex < 0
            || toIndex < 0
            || fromIndex >= tracksProp.arraySize
            || toIndex >= tracksProp.arraySize
            || fromIndex == toIndex)
        {
            return false;
        }

        Undo.RecordObject(so.targetObject, "Reorder Action Track");
        tracksProp.MoveArrayElement(fromIndex, toIndex);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(so.targetObject);
        return true;
    }

    /// <summary>重命名轨道，并同步该轨上全部窗口的 trackName。</summary>
    public static void RenameTrack(SerializedObject so, int trackIndex, string newName)
    {
        SerializedProperty tracksProp = so.FindProperty("timeline.tracks");
        if (tracksProp == null || trackIndex < 0 || trackIndex >= tracksProp.arraySize)
            return;

        if (string.IsNullOrWhiteSpace(newName))
            return;

        SerializedProperty track = tracksProp.GetArrayElementAtIndex(trackIndex);
        string oldName = track.FindPropertyRelative("trackName").stringValue;
        if (oldName == newName)
            return;

        var kind = (ActionTimelineTrackKind)track.FindPropertyRelative("kind").enumValueIndex;
        Undo.RecordObject(so.targetObject, "Rename Action Track");
        track.FindPropertyRelative("trackName").stringValue = newName;
        RenameWindowsTrack(so, kind, oldName, newName);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(so.targetObject);
    }

    /// <summary>在指定轨上添加窗口；VFX/SFX/Event 为单帧点事件。</summary>
    public static ActionEditorSelection AddWindow(
        SerializedObject so,
        ActionTimelineTrackKind kind,
        string trackName,
        int startFrame,
        float sampleRate,
        int totalFrames)
    {
        _ = sampleRate;
        string arrayName = GetArrayPropertyName(kind);
        if (arrayName == null)
            return default;

        SerializedProperty arrayProp = so.FindProperty($"timeline.{arrayName}");
        if (arrayProp == null)
            return default;
        CancelWindowType newWindowType = CancelWindowType.Normal;
        if (kind == ActionTimelineTrackKind.Cancel)
        {
            if (arrayProp.arraySize >= 2)
                return new ActionEditorSelection(arrayProp, arrayProp.arraySize - 1, kind);

            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                SerializedProperty typeProp = arrayProp
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("windowType");
                if (typeProp != null && typeProp.enumValueIndex == (int)CancelWindowType.Normal)
                    newWindowType = CancelWindowType.Perfect;
            }
        }

        Undo.RecordObject(so.targetObject, "Add Action Window");
        int index = arrayProp.arraySize;
        arrayProp.arraySize++;
        SerializedProperty element = arrayProp.GetArrayElementAtIndex(index);

        int maxFrame = Mathf.Max(0, totalFrames - 1);
        startFrame = Mathf.Clamp(startFrame, 0, maxFrame);
        int endFrame = ResolveDefaultEndFrame(element, kind, startFrame, maxFrame);

        string itemId = kind == ActionTimelineTrackKind.Cancel
            ? $"{newWindowType}Cancel"
            : $"{ActionEditorStyles.DisplayName(kind)}_{index + 1}";
        SetIfExists(element, "id", itemId);
        SetIfExists(element, "startFrame", startFrame);
        SetIfExists(element, "endFrame", endFrame);
        SetIfExists(element, "priority", 0);
        SetIfExists(element, "trackName", trackName);
        if (kind == ActionTimelineTrackKind.Cancel)
            SetIfExists(element, "windowType", (int)newWindowType);
        if (kind == ActionTimelineTrackKind.Phase)
        {
            SetIfExists(element, "interruptible", true);
            SetIfExists(element, "allowMovementCancel", true);
            SetIfExists(element, "allowEntryRestart", true);
        }

        if (kind == ActionTimelineTrackKind.MotionModifier)
        {
            // 默认 TargetAdhesion + Branch_02 常用偏移；可在 Inspector 改 SoftBodySuppress
            SetIfExists(element, "mode", (int)MotionModifierMode.TargetAdhesion);
            SetIfExists(element, "targetSource", (int)MotionTargetSource.SelectedTarget);
            SetIfExists(element, "horizontalOffsetMm", 1000);
            SetIfExists(element, "lateralOffsetMm", 0);
            SetIfExists(element, "maxCorrectionMmPerFrame", 250);
            SetIfExists(element, "maxAcquireDistanceMm", 4500);
            SetIfExists(element, "maxAngleMilliDeg", 0);
            SetIfExists(element, "stopOnTargetLost", true);
        }

        if (kind == ActionTimelineTrackKind.Camera)
        {
            SetIfExists(element, "overrideCameraPose", true);
            SetIfExists(element, "constantSpeed", true);
            SetIfExists(element, "blendInSeconds", 0.08f);
            SetIfExists(element, "inheritPosition", true);
        }

        so.ApplyModifiedProperties();
        if (kind == ActionTimelineTrackKind.Camera
            && so.targetObject is ActionDefinition action
            && index < action.CameraShotStates.Length)
        {
            // Unity 扩容数组会复制上一项，必须在 Apply 后替换为独立 Spline 实例。
            action.CameraShotStates[index].ResetSplineDefaults();
            EditorUtility.SetDirty(action);
            so.Update();
        }
        EditorUtility.SetDirty(so.targetObject);
        return new ActionEditorSelection(arrayProp, index, kind);
    }

    /// <summary>在 animationSegments 末尾追加一段空 Clip 槽。</summary>
    public static ActionEditorSelection AddAnimationSegment(SerializedObject so)
    {
        SerializedProperty segmentsProp = so.FindProperty("animationSegments");
        if (segmentsProp == null)
            return default;

        Undo.RecordObject(so.targetObject, "Add Animation Segment");
        int index = segmentsProp.arraySize;
        segmentsProp.arraySize++;
        SerializedProperty element = segmentsProp.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("clip").objectReferenceValue = null;
        element.FindPropertyRelative("startFrame").intValue = 0;
        element.FindPropertyRelative("endFrame").intValue = -1;
        element.FindPropertyRelative("crossFadeDuration").floatValue = 0f;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(so.targetObject);
        return new ActionEditorSelection(segmentsProp, index, ActionTimelineTrackKind.Animation);
    }

    /// <summary>
    /// 调整 animationSegments 顺序；fromIndex/toIndex 为数组下标。
    /// 成功时返回新下标对应的选中项，失败返回 default。
    /// </summary>
    public static ActionEditorSelection ReorderAnimationSegment(SerializedObject so, int fromIndex, int toIndex)
    {
        SerializedProperty segmentsProp = so.FindProperty("animationSegments");
        if (segmentsProp == null || fromIndex == toIndex)
            return default;

        if (fromIndex < 0 || toIndex < 0 || fromIndex >= segmentsProp.arraySize || toIndex >= segmentsProp.arraySize)
            return default;

        Undo.RecordObject(so.targetObject, "Reorder Animation Segment");
        segmentsProp.MoveArrayElement(fromIndex, toIndex);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(so.targetObject);
        return new ActionEditorSelection(segmentsProp, toIndex, ActionTimelineTrackKind.Animation);
    }

    /// <summary>删除选中窗口。</summary>
    public static void RemoveWindow(SerializedObject so, ActionEditorSelection selection)
    {
        if (!selection.IsValid)
            return;

        string undoName = selection.Kind == ActionTimelineTrackKind.Animation
            ? "Remove Animation Segment"
            : "Remove Action Window";
        Undo.RecordObject(so.targetObject, undoName);
        selection.ArrayProperty.DeleteArrayElementAtIndex(selection.Index);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(so.targetObject);
    }

    /// <summary>平移窗口，保持长度不变。</summary>
    public static void MoveWindow(SerializedProperty element, int deltaFrames, int totalFrames)
    {
        if (element == null || deltaFrames == 0)
            return;

        SerializedProperty startProp = element.FindPropertyRelative("startFrame");
        SerializedProperty endProp = element.FindPropertyRelative("endFrame");
        if (startProp == null || endProp == null)
            return;

        int oldStart = startProp.intValue;
        int length = endProp.intValue - oldStart;
        int maxFrame = Mathf.Max(0, totalFrames - 1);
        int newStart = Mathf.Clamp(oldStart + deltaFrames, 0, maxFrame - length);
        startProp.intValue = newStart;
        endProp.intValue = newStart + length;
    }

    /// <summary>改左边缘（startFrame），保持最小 1 帧。</summary>
    public static void ResizeWindowStart(SerializedProperty element, int newStart, int totalFrames)
    {
        if (element == null)
            return;

        SerializedProperty startProp = element.FindPropertyRelative("startFrame");
        SerializedProperty endProp = element.FindPropertyRelative("endFrame");
        if (startProp == null || endProp == null)
            return;

        int maxFrame = Mathf.Max(0, totalFrames - 1);
        startProp.intValue = Mathf.Clamp(newStart, 0, Mathf.Min(endProp.intValue, maxFrame));
    }

    /// <summary>改右边缘（endFrame），保持最小 1 帧。</summary>
    public static void ResizeWindowEnd(SerializedProperty element, int newEnd, int totalFrames)
    {
        if (element == null)
            return;

        SerializedProperty startProp = element.FindPropertyRelative("startFrame");
        SerializedProperty endProp = element.FindPropertyRelative("endFrame");
        if (startProp == null || endProp == null)
            return;

        int maxFrame = Mathf.Max(0, totalFrames - 1);
        endProp.intValue = Mathf.Clamp(newEnd, startProp.intValue, maxFrame);
    }

    static int ResolveDefaultEndFrame(
        SerializedProperty element,
        ActionTimelineTrackKind kind,
        int startFrame,
        int maxFrame)
    {
        // 点事件轨：触发帧单帧；区间轨：默认若干帧。
        if (ActionEditorStyles.IsPointEventTrack(kind))
        {
            if (kind == ActionTimelineTrackKind.Vfx || kind == ActionTimelineTrackKind.Sfx)
                SetIfExists(element, "playbackSpeed", 1f);

            return startFrame;
        }

        int length = ActionEditorStyles.DefaultWindowFrames;
        return Mathf.Min(maxFrame, startFrame + Mathf.Max(1, length) - 1);
    }

    static void RemoveWindowsOnTrack(SerializedObject so, ActionTimelineTrackKind kind, string trackName)
    {
        string arrayName = GetArrayPropertyName(kind);
        if (arrayName == null)
            return;

        SerializedProperty arrayProp = so.FindProperty($"timeline.{arrayName}");
        if (arrayProp == null)
            return;

        for (int i = arrayProp.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty element = arrayProp.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = element.FindPropertyRelative("trackName");
            if (nameProp != null && nameProp.stringValue == trackName)
                arrayProp.DeleteArrayElementAtIndex(i);
        }
    }

    static void RenameWindowsTrack(
        SerializedObject so,
        ActionTimelineTrackKind kind,
        string oldName,
        string newName)
    {
        string arrayName = GetArrayPropertyName(kind);
        if (arrayName == null)
            return;

        SerializedProperty arrayProp = so.FindProperty($"timeline.{arrayName}");
        if (arrayProp == null)
            return;

        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            SerializedProperty nameProp = arrayProp.GetArrayElementAtIndex(i).FindPropertyRelative("trackName");
            if (nameProp != null && nameProp.stringValue == oldName)
                nameProp.stringValue = newName;
        }
    }

    static int CountTracksOfKind(SerializedProperty tracksProp, ActionTimelineTrackKind kind)
    {
        int count = 0;
        for (int i = 0; i < tracksProp.arraySize; i++)
        {
            if (tracksProp.GetArrayElementAtIndex(i).FindPropertyRelative("kind").enumValueIndex == (int)kind)
                count++;
        }

        return count;
    }

    static void SetIfExists(SerializedProperty element, string field, int value)
    {
        SerializedProperty prop = element.FindPropertyRelative(field);
        if (prop != null)
            prop.intValue = value;
    }

    static void SetIfExists(SerializedProperty element, string field, float value)
    {
        SerializedProperty prop = element.FindPropertyRelative(field);
        if (prop != null)
            prop.floatValue = value;
    }

    static void SetIfExists(SerializedProperty element, string field, bool value)
    {
        SerializedProperty prop = element.FindPropertyRelative(field);
        if (prop != null)
            prop.boolValue = value;
    }

    static void SetIfExists(SerializedProperty element, string field, string value)
    {
        SerializedProperty prop = element.FindPropertyRelative(field);
        if (prop != null)
            prop.stringValue = value;
    }
}
