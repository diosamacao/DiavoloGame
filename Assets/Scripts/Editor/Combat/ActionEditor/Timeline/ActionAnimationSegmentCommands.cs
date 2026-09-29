using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>动画插入与源帧修剪的原子命令；战斗窗口保持绝对帧，不自动重定时。</summary>
public static class ActionAnimationSegmentCommands
{
    /// <summary>初始化新动作草稿的完整有序片段及总帧数；只供创建前使用，不产生编辑 Undo。</summary>
    public static void InitializeDraft(SerializedObject so, IReadOnlyList<AnimationClip> clips)
    {
        if (clips == null) throw new System.ArgumentNullException(nameof(clips));
        foreach (var clip in clips)
            if (clip == null) throw new System.ArgumentException("动画段不能包含空 Clip。", nameof(clips));
        var segments = so.FindProperty("animationSegments");
        segments.arraySize = clips.Count;
        int total = 0;
        for (int i = 0; i < clips.Count; i++)
        {
            var item = segments.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("clip").objectReferenceValue = clips[i];
            item.FindPropertyRelative("startFrame").intValue = 0;
            item.FindPropertyRelative("endFrame").intValue = -1;
            item.FindPropertyRelative("hasCrossFadeOverride").boolValue = false;
            item.FindPropertyRelative("crossFadeDuration").floatValue = 0;
            total += new ActionAnimationSegment { clip = clips[i], startFrame = 0, endFrame = -1 }.GetFrameCount(ActionSim.LogicHz);
        }
        so.FindProperty("totalFrames").intValue = Mathf.Max(1, total);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>属性面板和删除命令共用提交检查；失败丢弃本次尚未应用的序列化更改。</summary>
    public static bool ApplyPending(SerializedObject so, out string error)
    {
        var array = so.FindProperty("animationSegments");
        int total = 0;
        for (int i = 0; i < array.arraySize; i++)
        {
            var item = array.GetArrayElementAtIndex(i);
            var segment = new ActionAnimationSegment
            {
                clip = item.FindPropertyRelative("clip").objectReferenceValue as AnimationClip,
                startFrame = item.FindPropertyRelative("startFrame").intValue,
                endFrame = item.FindPropertyRelative("endFrame").intValue,
            };
            total += segment.GetFrameCount(ActionSim.LogicHz);
        }
        if (!WindowsFit(so, Mathf.Max(1, total), out error)) { so.Update(); return false; }
        so.FindProperty("totalFrames").intValue = Mathf.Max(1, total);
        so.ApplyModifiedProperties();
        return true;
    }

    /// <summary>先检查所有窗口，再提交源帧范围；失败不写回任何字段。</summary>
    public static bool Trim(SerializedObject so, int index, int start, int end, out string error)
    {
        error = null;
        var action = so.targetObject as ActionDefinition;
        if (action == null || index < 0 || index >= action.AnimationSegments.Length)
        { error = "无效动画段。"; return false; }
        var segment = action.AnimationSegments[index];
        int last = segment.clip != null ? Mathf.Max(0, Mathf.RoundToInt(segment.clip.length * action.SampleRate) - 1) : -1;
        if (start < 0 || end < start || end > last) { error = "源帧范围超出动画或长度小于一帧。"; return false; }
        int total = action.TotalFrames - segment.GetFrameCount(action.SampleRate) + end - start + 1;
        if (!WindowsFit(so, total, out error)) return false;
        Undo.RecordObject(action, "Trim Animation Segment");
        var element = so.FindProperty("animationSegments").GetArrayElementAtIndex(index);
        element.FindPropertyRelative("startFrame").intValue = start;
        element.FindPropertyRelative("endFrame").intValue = end;
        so.FindProperty("totalFrames").intValue = total;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(action);
        return true;
    }

    /// <summary>只读边界检查，返回全部超出拟议动作长度的轨道/元素路径。</summary>
    public static bool WindowsFit(SerializedObject so, int total, out string error)
    {
        var failures = new List<string>();
        foreach (ActionTimelineTrackKind kind in System.Enum.GetValues(typeof(ActionTimelineTrackKind)))
        {
            string name = ActionTimelineCommands.GetArrayPropertyName(kind);
            if (name == null) continue;
            var array = so.FindProperty("timeline." + name);
            for (int i = 0; array != null && i < array.arraySize; i++)
            {
                var item = array.GetArrayElementAtIndex(i);
                int start = item.FindPropertyRelative("startFrame").intValue;
                int end = item.FindPropertyRelative("endFrame").intValue;
                if (start < 0 || end < start || end >= total)
                    failures.Add($"{kind}[{i}] ({start}–{end})");
            }
        }
        error = failures.Count == 0 ? null : $"未提交：总长 {total} 帧使以下窗口越界：" + string.Join("、", failures);
        return failures.Count == 0;
    }

    /// <summary>按拖拽引用顺序插入片段，所有字段显式初始化，一次 Undo。</summary>
    public static ActionEditorSelection Insert(SerializedObject so, int index, IReadOnlyList<AnimationClip> clips)
    {
        if (clips == null || clips.Count == 0) return default;
        foreach (var clip in clips) if (clip == null) return default;
        var array = so.FindProperty("animationSegments");
        index = Mathf.Clamp(index, 0, array.arraySize);
        Undo.RecordObject(so.targetObject, "Insert Animation Clips");
        for (int i = 0; i < clips.Count; i++)
        {
            array.InsertArrayElementAtIndex(index + i);
            var element = array.GetArrayElementAtIndex(index + i);
            element.FindPropertyRelative("clip").objectReferenceValue = clips[i];
            element.FindPropertyRelative("startFrame").intValue = 0;
            element.FindPropertyRelative("endFrame").intValue = -1;
            element.FindPropertyRelative("hasCrossFadeOverride").boolValue = false;
            element.FindPropertyRelative("crossFadeDuration").floatValue = 0;
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(so.targetObject);
        return new ActionEditorSelection(array, index, ActionTimelineTrackKind.Animation);
    }
}
