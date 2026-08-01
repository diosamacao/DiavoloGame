using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>单个整数动作帧的只读段映射、窗口集合与点事件集合。</summary>
public readonly struct ActionFrameQueryResult
{
    readonly IReadOnlyList<ActionNotifyState> _activeStates;
    readonly IReadOnlyList<ActionNotify> _pointEvents;

    /// <summary>创建不可变帧查询结果；调用方不得修改返回集合。</summary>
    public ActionFrameQueryResult(
        ActionDefinition action,
        int frame,
        int segmentIndex,
        ActionAnimationSegment segment,
        int segmentFrameOffset,
        IReadOnlyList<ActionNotifyState> activeStates,
        IReadOnlyList<ActionNotify> pointEvents)
    {
        Action = action;
        Frame = frame;
        SegmentIndex = segmentIndex;
        Segment = segment;
        SegmentFrameOffset = segmentFrameOffset;
        _activeStates = activeStates ?? Array.Empty<ActionNotifyState>();
        _pointEvents = pointEvents ?? Array.Empty<ActionNotify>();
    }

    /// <summary>被查询的动作。</summary>
    public ActionDefinition Action { get; }

    /// <summary>被查询的整数动作帧。</summary>
    public int Frame { get; }

    /// <summary>当前动画段索引；无有效段时为 -1。</summary>
    public int SegmentIndex { get; }

    /// <summary>当前动画段。</summary>
    public ActionAnimationSegment Segment { get; }

    /// <summary>当前动画段内的整数帧偏移。</summary>
    public int SegmentFrameOffset { get; }

    /// <summary>当前帧全部生效的区间窗口。</summary>
    public IReadOnlyList<ActionNotifyState> ActiveStates =>
        _activeStates ?? Array.Empty<ActionNotifyState>();

    /// <summary>恰好在当前帧触发的点事件。</summary>
    public IReadOnlyList<ActionNotify> PointEvents =>
        _pointEvents ?? Array.Empty<ActionNotify>();

    /// <summary>当前帧是否映射到有效动画段。</summary>
    public bool HasAnimationSegment =>
        SegmentIndex >= 0 && Segment.clip != null;

    /// <summary>当前段对应的 Clip 局部采样时间。</summary>
    public float SegmentLocalTime =>
        HasAnimationSegment && Action != null
            ? Segment.GetLocalTimeSeconds(SegmentFrameOffset, Action.SampleRate)
            : 0f;

    /// <summary>返回给定窗口是否包含在当前帧查询集合中。</summary>
    public bool IsStateActive(ActionNotifyState state)
    {
        if (state == null)
            return false;

        IReadOnlyList<ActionNotifyState> states = ActiveStates;
        for (int i = 0; i < states.Count; i++)
        {
            if (ReferenceEquals(states[i], state))
                return true;
        }

        return false;
    }

    /// <summary>返回给定点事件是否恰好在当前帧触发。</summary>
    public bool IsPointEvent(ActionNotify notify)
    {
        if (notify == null)
            return false;

        IReadOnlyList<ActionNotify> pointEvents = PointEvents;
        for (int i = 0; i < pointEvents.Count; i++)
        {
            if (ReferenceEquals(pointEvents[i], notify))
                return true;
        }

        return false;
    }
}

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
