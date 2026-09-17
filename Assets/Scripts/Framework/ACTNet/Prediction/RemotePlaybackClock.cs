/// <summary>
/// 远端单调播放头：按真实时间推进，钳在 [first, latest-delay]。
/// 禁止使用会每逻辑步清零的 InterpolationAlpha，避免位移回绕。
/// </summary>
public static class RemotePlaybackClock
{
    /// <summary>积压时的有限追赶倍率；避免长期慢放，又不突然快进。</summary>
    public const double CatchUpRate = 1.2d;

    /// <summary>播放头落后超过缓存安全窗时直接回到稳定延迟目标。</summary>
    public const double TimelineSafetyTicks = 12d;

    /// <summary>单渲染帧允许推进的上限，防止长卡顿把动画瞬间冲完。</summary>
    public const double MaxAdvancePerRender = 4d;

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
        if (hasCurrent
            && (play < first || desired - play > TimelineSafetyTicks))
        {
            // 已无法在安全缓存内连续追上；一次性恢复稳定延迟，后续再平滑推进。
            return desired;
        }

        int hz = logicHz > 0 ? logicHz : 60;
        double normalAdvance = deltaSeconds * hz;
        double advance = desired > play + normalAdvance
            ? normalAdvance * CatchUpRate
            : normalAdvance;
        if (advance < 0d)
            advance = 0d;
        if (advance > MaxAdvancePerRender)
            advance = MaxAdvancePerRender;

        play += advance;
        if (play > desired)
            play = desired;
        if (play < first)
            play = first;
        return play;
    }
}
