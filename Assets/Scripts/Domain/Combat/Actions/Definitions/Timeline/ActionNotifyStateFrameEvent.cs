/// <summary>一次逻辑帧推进中产生的 NotifyState Enter/Tick/Exit 事件。</summary>
public readonly struct ActionNotifyStateFrameEvent
{
    public ActionNotifyStateFrameEvent(ActionNotifyState state, ActionNotifyStatePhase phase)
    {
        State = state;
        Phase = phase;
    }

    /// <summary>产生事件的区间 NotifyState。</summary>
    public ActionNotifyState State { get; }

    /// <summary>本帧推进对应的 Enter/Tick/Exit 阶段。</summary>
    public ActionNotifyStatePhase Phase { get; }
}
