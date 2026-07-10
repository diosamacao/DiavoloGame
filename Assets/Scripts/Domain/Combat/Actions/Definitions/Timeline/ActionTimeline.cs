using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>ActionDefinition 的统一帧时间轴；使用明确类型列表作为当前唯一数据真源。</summary>
[Serializable]
public class ActionTimeline
{
    [SerializeField] ActionEvent[] actionEvents = Array.Empty<ActionEvent>();
    [SerializeField] PlayVfxNotify[] playVfxNotifies = Array.Empty<PlayVfxNotify>();
    [SerializeField] PlaySfxNotifyState[] playSfxStates = Array.Empty<PlaySfxNotifyState>();
    [SerializeField] HitboxNotifyState[] hitboxStates = Array.Empty<HitboxNotifyState>();
    [SerializeField] HurtboxNotifyState[] hurtboxStates = Array.Empty<HurtboxNotifyState>();
    [SerializeField] CancelWindowNotifyState[] cancelWindowStates = Array.Empty<CancelWindowNotifyState>();
    [SerializeField] MovementNotifyState[] movementStates = Array.Empty<MovementNotifyState>();
    [SerializeField] RotationNotifyState[] rotationStates = Array.Empty<RotationNotifyState>();
    [SerializeField] ActionTimelineTrack[] tracks = Array.Empty<ActionTimelineTrack>();

    /// <summary>通用点事件列表，用于自定义信号、镜头等非专用事件。</summary>
    public ActionEvent[] ActionEvents => actionEvents ?? Array.Empty<ActionEvent>();

    /// <summary>VFX 区间窗口列表（可拖时长，倍率由自然时长派生）。</summary>
    public PlayVfxNotify[] PlayVfxNotifies => playVfxNotifies ?? Array.Empty<PlayVfxNotify>();

    /// <summary>SFX 区间窗口列表。</summary>
    public PlaySfxNotifyState[] PlaySfxStates => playSfxStates ?? Array.Empty<PlaySfxNotifyState>();

    /// <summary>攻击判定框区间列表。</summary>
    public HitboxNotifyState[] HitboxStates => hitboxStates ?? Array.Empty<HitboxNotifyState>();

    /// <summary>受击框区间列表，供后续部位/弱点时间轴使用。</summary>
    public HurtboxNotifyState[] HurtboxStates => hurtboxStates ?? Array.Empty<HurtboxNotifyState>();

    /// <summary>取消窗口区间列表。</summary>
    public CancelWindowNotifyState[] CancelWindowStates => cancelWindowStates ?? Array.Empty<CancelWindowNotifyState>();

    /// <summary>脚本位移区间列表。</summary>
    public MovementNotifyState[] MovementStates => movementStates ?? Array.Empty<MovementNotifyState>();

    /// <summary>旋转修正区间列表。</summary>
    public RotationNotifyState[] RotationStates => rotationStates ?? Array.Empty<RotationNotifyState>();

    /// <summary>手动添加的轨道列表；可为空轨，窗口通过 trackName 归属。</summary>
    public ActionTimelineTrack[] Tracks => tracks ?? Array.Empty<ActionTimelineTrack>();

    /// <summary>是否配置了任意非零脚本位移窗口。</summary>
    public bool HasScriptedMovement
    {
        get
        {
            foreach (MovementNotifyState state in MovementStates)
            {
                if (state != null && state.HasDisplacement)
                    return true;
            }

            return false;
        }
    }

    /// <summary>枚举全部点事件，供 Runner 与编辑器轨道统一处理。</summary>
    public IEnumerable<ActionNotify> EnumerateNotifies()
    {
        foreach (ActionEvent actionEvent in ActionEvents)
        {
            if (actionEvent != null)
                yield return actionEvent;
        }
    }

    /// <summary>枚举全部区间窗口，供 Runner 与编辑器轨道统一处理。</summary>
    public IEnumerable<ActionNotifyState> EnumerateStates()
    {
        foreach (HitboxNotifyState state in HitboxStates)
        {
            if (state != null)
                yield return state;
        }

        foreach (HurtboxNotifyState state in HurtboxStates)
        {
            if (state != null)
                yield return state;
        }

        foreach (PlayVfxNotify state in PlayVfxNotifies)
        {
            if (state != null)
                yield return state;
        }

        foreach (PlaySfxNotifyState state in PlaySfxStates)
        {
            if (state != null)
                yield return state;
        }

        foreach (CancelWindowNotifyState state in CancelWindowStates)
        {
            if (state != null)
                yield return state;
        }

        foreach (MovementNotifyState state in MovementStates)
        {
            if (state != null)
                yield return state;
        }

        foreach (RotationNotifyState state in RotationStates)
        {
            if (state != null)
                yield return state;
        }
    }

    /// <summary>查询跨帧推进中应触发的点事件。</summary>
    public IReadOnlyList<ActionNotify> GetTriggeredNotifies(int previousFrame, int currentFrame)
    {
        var triggered = new List<ActionNotify>();
        foreach (ActionNotify notify in EnumerateNotifies())
        {
            if (notify.ShouldFireBetweenFrames(previousFrame, currentFrame))
                triggered.Add(notify);
        }

        triggered.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        return triggered;
    }

    /// <summary>查询跨帧推进中产生的区间 Enter / Tick / Exit 事件。</summary>
    public IReadOnlyList<ActionNotifyStateFrameEvent> GetStateFrameEvents(int previousFrame, int currentFrame)
    {
        var events = new List<ActionNotifyStateFrameEvent>();
        foreach (ActionNotifyState state in EnumerateStates())
        {
            if (state.ShouldEnter(previousFrame, currentFrame))
                events.Add(new ActionNotifyStateFrameEvent(state, ActionNotifyStatePhase.Enter));

            if (state.IsActiveAtFrame(currentFrame))
                events.Add(new ActionNotifyStateFrameEvent(state, ActionNotifyStatePhase.Tick));

            if (state.ShouldExit(previousFrame, currentFrame))
                events.Add(new ActionNotifyStateFrameEvent(state, ActionNotifyStatePhase.Exit));
        }

        events.Sort((a, b) => b.State.Priority.CompareTo(a.State.Priority));
        return events;
    }

    /// <summary>返回指定帧生效的 Hitbox 状态窗口。</summary>
    public IReadOnlyList<HitboxNotifyState> GetActiveHitboxesAtFrame(int frame)
    {
        var active = new List<HitboxNotifyState>();
        foreach (HitboxNotifyState hitbox in HitboxStates)
        {
            if (hitbox != null && hitbox.IsActiveAtFrame(frame))
                active.Add(hitbox);
        }

        return active;
    }

    /// <summary>按优先级返回全部 CancelWindow 状态窗口。</summary>
    public IReadOnlyList<ResolvedCancelWindow> GetCancelWindowsSorted()
    {
        var sorted = new List<ResolvedCancelWindow>();
        foreach (CancelWindowNotifyState state in CancelWindowStates)
        {
            if (state != null)
                sorted.Add(state.ToResolved());
        }

        sorted.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        return sorted;
    }

    /// <summary>查询指定帧的最高优先级脚本位移窗口。</summary>
    public MovementNotifyState GetActiveMovementStateAtFrame(int frame)
    {
        MovementNotifyState best = null;
        foreach (MovementNotifyState state in MovementStates)
        {
            if (state == null || !state.IsActiveAtFrame(frame))
                continue;

            if (best == null || state.Priority > best.Priority)
                best = state;
        }

        return best;
    }

    /// <summary>查询指定帧的最高优先级旋转窗口。</summary>
    public RotationNotifyState GetActiveRotationStateAtFrame(int frame)
    {
        RotationNotifyState best = null;
        foreach (RotationNotifyState state in RotationStates)
        {
            if (state == null || !state.IsActiveAtFrame(frame))
                continue;

            if (best == null || state.Priority > best.Priority)
                best = state;
        }

        return best;
    }

    /// <summary>验证所有时间轴条目帧范围，保持编辑器拖拽后的数据有效。</summary>
    public void ClampToTotalFrames(int totalFrames)
    {
        foreach (ActionNotify notify in EnumerateNotifies())
            notify.ClampToTotalFrames(totalFrames);

        foreach (ActionNotifyState state in EnumerateStates())
            state.ClampToTotalFrames(totalFrames);
    }
}
