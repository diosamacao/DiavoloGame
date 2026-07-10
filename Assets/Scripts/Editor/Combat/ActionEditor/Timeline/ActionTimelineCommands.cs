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
        ActionTimelineTrackKind.Movement => "movementStates",
        ActionTimelineTrackKind.Rotation => "rotationStates",
        ActionTimelineTrackKind.Event => "actionEvents",
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
        anyAdded |= AppendMissingTracks(so, tracksProp, ActionTimelineTrackKind.Movement);
        anyAdded |= AppendMissingTracks(so, tracksProp, ActionTimelineTrackKind.Rotation);
        anyAdded |= AppendMissingTracks(so, tracksProp, ActionTimelineTrackKind.Event);

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

    /// <summary>添加一条空轨并写入默认 trackName。</summary>
    public static void AddTrack(SerializedObject so, ActionTimelineTrackKind kind)
    {
        SerializedProperty tracksProp = so.FindProperty("timeline.tracks");
        if (tracksProp == null)
            return;

        Undo.RecordObject(so.targetObject, "Add Action Track");
        int index = tracksProp.arraySize;
        tracksProp.arraySize++;
        SerializedProperty track = tracksProp.GetArrayElementAtIndex(index);
        string defaultName = $"{ActionEditorStyles.DisplayName(kind)}_{CountTracksOfKind(tracksProp, kind) + 1}";
        track.FindPropertyRelative("trackName").stringValue = defaultName;
        track.FindPropertyRelative("kind").enumValueIndex = (int)kind;
        track.FindPropertyRelative("visible").boolValue = true;
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

    /// <summary>在指定轨上添加窗口；VFX/SFX 按自然时长设默认长度。</summary>
    public static ActionEditorSelection AddWindow(
        SerializedObject so,
        ActionTimelineTrackKind kind,
        string trackName,
        int startFrame,
        float sampleRate,
        int totalFrames)
    {
        string arrayName = GetArrayPropertyName(kind);
        if (arrayName == null || kind == ActionTimelineTrackKind.Phase)
            return default;

        SerializedProperty arrayProp = so.FindProperty($"timeline.{arrayName}");
        if (arrayProp == null)
            return default;

        Undo.RecordObject(so.targetObject, "Add Action Window");
        int index = arrayProp.arraySize;
        arrayProp.arraySize++;
        SerializedProperty element = arrayProp.GetArrayElementAtIndex(index);

        int maxFrame = Mathf.Max(0, totalFrames - 1);
        startFrame = Mathf.Clamp(startFrame, 0, maxFrame);
        int endFrame = ResolveDefaultEndFrame(element, kind, startFrame, sampleRate, maxFrame);

        SetIfExists(element, "id", $"{ActionEditorStyles.DisplayName(kind)}_{index + 1}");
        SetIfExists(element, "startFrame", startFrame);
        SetIfExists(element, "endFrame", endFrame);
        SetIfExists(element, "priority", 0);
        SetIfExists(element, "trackName", trackName);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(so.targetObject);
        return new ActionEditorSelection(arrayProp, index, kind);
    }

    /// <summary>删除选中窗口。</summary>
    public static void RemoveWindow(SerializedObject so, ActionEditorSelection selection)
    {
        if (!selection.IsValid)
            return;

        Undo.RecordObject(so.targetObject, "Remove Action Window");
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

        int length = endProp.intValue - startProp.intValue;
        int maxFrame = Mathf.Max(0, totalFrames - 1);
        int newStart = Mathf.Clamp(startProp.intValue + deltaFrames, 0, maxFrame - length);
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
        float sampleRate,
        int maxFrame)
    {
        if (kind == ActionTimelineTrackKind.Event)
            return startFrame;

        int length = ActionEditorStyles.DefaultWindowFrames;
        if (kind == ActionTimelineTrackKind.Vfx)
        {
            GameObject prefab = element.FindPropertyRelative("prefab")?.objectReferenceValue as GameObject;
            float natural = ActionVfxPlayback.EstimateNaturalDurationSeconds(prefab);
            SetIfExists(element, "naturalDurationSeconds", natural);
            length = ActionVfxPlayback.DurationSecondsToFrameCount(natural, sampleRate);
        }
        else if (kind == ActionTimelineTrackKind.Sfx)
        {
            var clip = element.FindPropertyRelative("audioClip")?.objectReferenceValue as AudioClip;
            float natural = clip != null ? clip.length : 0.5f;
            SetIfExists(element, "naturalDurationSeconds", natural);
            length = ActionVfxPlayback.DurationSecondsToFrameCount(natural, sampleRate);
        }

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

    static void SetIfExists(SerializedProperty element, string field, string value)
    {
        SerializedProperty prop = element.FindPropertyRelative(field);
        if (prop != null)
            prop.stringValue = value;
    }
}
