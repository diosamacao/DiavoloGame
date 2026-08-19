/// <summary>可靠下发的对局结束通知；Tick 为结束时权威帧，尚无步进则为 0。</summary>
public readonly struct MatchEndMessage
{
    /// <summary>创建 MatchEnd 正文。</summary>
    public MatchEndMessage(MatchEndReason reason, long tick)
    {
        Reason = reason;
        Tick = tick < 0 ? 0 : tick;
    }

    /// <summary>结束原因。</summary>
    public MatchEndReason Reason { get; }

    /// <summary>结束时权威逻辑帧；未步进则为 0。</summary>
    public long Tick { get; }
}
