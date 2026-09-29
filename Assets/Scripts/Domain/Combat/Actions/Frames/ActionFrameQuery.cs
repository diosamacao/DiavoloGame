using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Runtime 与 Action Editor 共用的无副作用整数帧查询入口。</summary>
public static class ActionFrameQuery
{
    /// <summary>查询指定动作帧；不会执行 Timeline Hook、物理检测或表现副作用。</summary>
    public static ActionFrameQueryResult Query(ActionDefinition action, int frame)
    {
        if (action == null)
            return default;

        int clampedFrame = Mathf.Clamp(frame, 0, Mathf.Max(0, action.TotalFrames - 1));
        action.TryGetSegmentAtFrame(
            clampedFrame,
            out int segmentIndex,
            out ActionAnimationSegment segment,
            out int segmentFrameOffset);

        var activeStates = new List<ActionNotifyState>();
        foreach (ActionNotifyState state in action.Timeline.EnumerateStates())
        {
            if (state.IsActiveAtFrame(clampedFrame))
                activeStates.Add(state);
        }

        var pointEvents = new List<ActionNotify>();
        foreach (ActionNotify notify in action.Timeline.EnumerateNotifies())
        {
            if (notify.TriggerFrame == clampedFrame)
                pointEvents.Add(notify);
        }

        return new ActionFrameQueryResult(
            action,
            clampedFrame,
            segmentIndex,
            segment,
            segmentFrameOffset,
            activeStates,
            pointEvents);
    }

    /// <summary>返回点事件在指定 Scrub 帧是否已经发生，用于持续表现预览。</summary>
    public static bool HasPointEventOccurred(ActionNotify notify, int frame) =>
        notify != null && frame >= notify.TriggerFrame;

    /// <summary>计算点事件发生后经过的秒数；帧率无效时返回零。</summary>
    public static float GetElapsedSecondsSincePoint(int triggerFrame, int frame, int sampleRate)
    {
        if (sampleRate <= 0 || frame <= triggerFrame)
            return 0f;

        return (frame - triggerFrame) / (float)sampleRate;
    }
}
