using System.Collections.Generic;
using UnityEngine;

/// <summary>Runtime 整数帧时间轴触发器；按稳定跨帧规则派发点事件与区间状态。</summary>
public sealed class ActionTimelineRunner
{
    /// <summary>按跨帧规则派发点事件与区间窗口事件。</summary>
    public void Dispatch(
        in CombatFrameContext frameContext,
        Transform attachPoint,
        IReadOnlyList<IActionNotifyConsumer> consumers)
    {
        ActionTimeline timeline = frameContext.Action != null ? frameContext.Action.Timeline : null;
        if (timeline == null)
            return;

        DispatchNotifies(timeline, in frameContext, attachPoint, consumers);
        DispatchStates(timeline, in frameContext, attachPoint, consumers);
    }

    /// <summary>派发 previousFrame &lt; triggerFrame &lt;= currentFrame 的点事件。</summary>
    void DispatchNotifies(
        ActionTimeline timeline,
        in CombatFrameContext frameContext,
        Transform attachPoint,
        IReadOnlyList<IActionNotifyConsumer> consumers)
    {
        IReadOnlyList<ActionNotify> notifies = timeline.GetTriggeredNotifies(
            frameContext.PreviousFrameIndex,
            frameContext.FrameIndex);

        foreach (ActionNotify notify in notifies)
        {
            var context = new ActionNotifyContext(
                frameContext.Action,
                frameContext.FrameIndex,
                frameContext.PreviousFrameIndex,
                frameContext.ActorRoot,
                attachPoint,
                notify);

            notify.OnNotify(in context);
            NotifyConsumers(in context, consumers, isState: false);
        }
    }

    /// <summary>派发区间窗口 Enter / Tick / Exit；Tick 与当前帧是否在区间内严格绑定。</summary>
    void DispatchStates(
        ActionTimeline timeline,
        in CombatFrameContext frameContext,
        Transform attachPoint,
        IReadOnlyList<IActionNotifyConsumer> consumers)
    {
        IReadOnlyList<ActionNotifyStateFrameEvent> stateEvents = timeline.GetStateFrameEvents(
            frameContext.PreviousFrameIndex,
            frameContext.FrameIndex);

        foreach (ActionNotifyStateFrameEvent stateEvent in stateEvents)
        {
            var context = new ActionNotifyContext(
                frameContext.Action,
                frameContext.FrameIndex,
                frameContext.PreviousFrameIndex,
                frameContext.ActorRoot,
                attachPoint,
                null,
                stateEvent.State,
                stateEvent.Phase);

            InvokeStateHook(in context);
            NotifyConsumers(in context, consumers, isState: true);
        }
    }

    static void InvokeStateHook(in ActionNotifyContext context)
    {
        switch (context.StatePhase)
        {
            case ActionNotifyStatePhase.Enter:
                context.State.OnEnter(in context);
                break;
            case ActionNotifyStatePhase.Tick:
                context.State.OnTick(in context);
                break;
            case ActionNotifyStatePhase.Exit:
                context.State.OnExit(in context);
                break;
        }
    }

    static void NotifyConsumers(
        in ActionNotifyContext context,
        IReadOnlyList<IActionNotifyConsumer> consumers,
        bool isState)
    {
        if (consumers == null)
            return;

        for (int i = 0; i < consumers.Count; i++)
        {
            if (isState)
                consumers[i].OnActionNotifyState(in context);
            else
                consumers[i].OnActionNotify(in context);
        }
    }
}
