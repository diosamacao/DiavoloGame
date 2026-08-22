/// <summary>
/// 远端单调播放头：按真实时间推进，钳在 [first, latest-delay]。
/// 禁止使用会每逻辑步清零的 InterpolationAlpha，避免位移回绕。
/// </summary>
public static class RemotePlaybackClock
{
    /// <summary>单帧最多追赶的逻辑 Tick，防止卡顿后片子快进。</summary>
    public const double MaxCatchUpTicks = 4d;

    /// <summary>把播放头从 current 推进 dt×hz，且不超过 latest-delay。</summary>
    public static double Advance(
        double current,
        bool hasCurrent,
        long firstTick,
        long latestTick,
        int delayTicks,
        double deltaSeconds,
        int logicHz)
    {
        if (firstTick < 0 || latestTick < 0)
            return hasCurrent ? current : 0d;

        int delay = delayTicks < 0 ? 0 : delayTicks;
        double first = firstTick;
        double latest = latestTick;
        double desired = latest - delay;
        if (desired < first)
            desired = first;

        double play = hasCurrent ? current : desired;
        int hz = logicHz > 0 ? logicHz : 60;
        double advance = deltaSeconds * hz;
        if (advance < 0d)
            advance = 0d;
        if (advance > MaxCatchUpTicks)
            advance = MaxCatchUpTicks;

        play += advance;
        if (play > desired)
            play = desired;
        if (play < first)
            play = first;
        return play;
    }
}
