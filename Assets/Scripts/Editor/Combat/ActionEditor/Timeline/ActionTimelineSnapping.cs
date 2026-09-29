using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>统一整数落帧与屏幕空间磁吸；不持有或修改动作数据。</summary>
public static class ActionTimelineSnapping
{
    /// <summary>8px 内选最近候选；同距按优先级、较小帧确定。Alt 关闭磁吸仍夹紧整数帧。</summary>
    public static int Resolve(int frame, float pixelsPerFrame, int lastFrame,
        IReadOnlyList<(int frame, int priority)> candidates, bool disabled = false)
    {
        int result = Mathf.Clamp(frame, 0, Mathf.Max(0, lastFrame));
        if (disabled || pixelsPerFrame <= 0 || candidates == null) return result;
        float best = float.MaxValue;
        int priority = int.MaxValue;
        foreach (var candidate in candidates)
        {
            if (candidate.frame < 0 || candidate.frame > lastFrame) continue;
            float distance = Mathf.Abs(candidate.frame - result) * pixelsPerFrame;
            if (distance > 8) continue;
            if (distance < best || (Mathf.Approximately(distance, best)
                && (candidate.priority < priority || (candidate.priority == priority && candidate.frame < frame))))
            { best = distance; priority = candidate.priority; frame = candidate.frame; }
        }
        return best < float.MaxValue ? frame : result;
    }

    /// <summary>收集合法端点；排除正在拖动的整组选中项，避免吸回自身。</summary>
    public static void Collect(SerializedObject so, ActionDefinition action, ActionEditorSelectionSet excluded,
        int playhead, List<(int frame, int priority)> output)
    {
        output.Clear();
        output.Add((0, 1)); output.Add((Mathf.Max(0, action.TotalFrames - 1), 1));
        if (playhead >= 0) output.Add((playhead, 0));
        int cursor = 0;
        for (int i = 0; i < action.AnimationSegments.Length; i++)
        {
            var segment = action.AnimationSegments[i];
            bool skip = false;
            if (excluded != null)
                foreach (var item in excluded.Items)
                    if (item.Kind == ActionTimelineTrackKind.Animation && item.Index == i) skip = true;
            int count = segment.GetFrameCount(action.SampleRate);
            if (!skip && count > 0) { output.Add((cursor, 1)); output.Add((cursor + count - 1, 1)); }
            cursor += count;
        }
        foreach (ActionTimelineTrackKind kind in Enum.GetValues(typeof(ActionTimelineTrackKind)))
        {
            string name = ActionTimelineCommands.GetArrayPropertyName(kind);
            if (name == null) continue;
            var array = so.FindProperty("timeline." + name);
            for (int i = 0; array != null && i < array.arraySize; i++)
            {
                if (excluded != null && excluded.Contains(new ActionEditorSelection(array, i, kind))) continue;
                var item = array.GetArrayElementAtIndex(i);
                int priority = kind == ActionTimelineTrackKind.Phase ? 1 : 2;
                output.Add((item.FindPropertyRelative("startFrame").intValue, priority));
                output.Add((item.FindPropertyRelative("endFrame").intValue, priority));
            }
        }
    }

    /// <summary>整组选中窗共用一个合法偏移，保持全部相对距离。</summary>
    public static int ClampGroupDelta(int delta, int earliest, int latest, int totalFrames) =>
        Mathf.Clamp(delta, -earliest, Mathf.Max(0, totalFrames - 1) - latest);
}
