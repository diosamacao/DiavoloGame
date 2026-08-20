/// <summary>记录最近一次权威 Ack Tick；Coordinator 用它丢弃已确认历史。</summary>
public sealed class PredictionAckTracker
{
    /// <summary>最近成功对照的权威 Tick；尚未 Ack 为 -1。</summary>
    public long LastAckTick { get; private set; } = -1;

    /// <summary>是否已经收到过至少一次 Ack。</summary>
    public bool HasAck => LastAckTick >= 0;

    /// <summary>写入新的 Ack Tick；旧值不会回退。</summary>
    public void Acknowledge(long ackTick)
    {
        if (ackTick > LastAckTick)
            LastAckTick = ackTick;
    }
}
