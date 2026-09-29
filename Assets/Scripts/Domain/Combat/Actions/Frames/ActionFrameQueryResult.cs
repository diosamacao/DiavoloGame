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
