using NUnit.Framework;

/// <summary>远端播放头只向前追 latest-delay，不会因本机 alpha 清零而回绕。</summary>
public sealed class RemotePlaybackClockTests
{
    /// <summary>1/60 秒推进约 1 个逻辑 Tick，并停在 delay 目标。</summary>
    [Test]
    public void Advance_OneSixtieth_MovesOneTickThenClamps()
    {
        double play = RemotePlaybackClock.Advance(
            current: 8d,
            hasCurrent: true,
            firstTick: 8,
            latestTick: 10,
            delayTicks: 1,
            deltaSeconds: 1d / 60d,
            logicHz: 60);
        Assert.That(play, Is.EqualTo(9d).Within(0.001d));

        double held = RemotePlaybackClock.Advance(
            current: play,
            hasCurrent: true,
            firstTick: 8,
            latestTick: 10,
            delayTicks: 1,
            deltaSeconds: 1d / 60d,
            logicHz: 60);
        Assert.That(held, Is.EqualTo(9d).Within(0.001d));
    }

    /// <summary>尚无播放头时从 desired 起，而不是从 0 快进。</summary>
    [Test]
    public void Advance_WithoutCurrent_StartsAtDesired()
    {
        double play = RemotePlaybackClock.Advance(
            current: 0d,
            hasCurrent: false,
            firstTick: 8,
            latestTick: 10,
            delayTicks: 1,
            deltaSeconds: 0d,
            logicHz: 60);
        Assert.That(play, Is.EqualTo(9d).Within(0.001d));
    }

    /// <summary>轻度积压只按 1.2 倍有限追赶，不直接吸到 latest-delay。</summary>
    [Test]
    public void Advance_SmallBacklog_UsesBoundedCatchUp()
    {
        double play = RemotePlaybackClock.Advance(
            current: 90d,
            hasCurrent: true,
            firstTick: 80,
            latestTick: 101,
            delayTicks: 1,
            deltaSeconds: 1d / 60d,
            logicHz: 60);

        Assert.That(play, Is.EqualTo(91.2d).Within(0.001d));
    }

    /// <summary>落后超过时间线安全窗时才恢复到稳定延迟目标。</summary>
    [Test]
    public void Advance_OutsideTimelineSafety_SnapsToDesired()
    {
        double play = RemotePlaybackClock.Advance(
            current: 80d,
            hasCurrent: true,
            firstTick: 80,
            latestTick: 101,
            delayTicks: 1,
            deltaSeconds: 1d / 60d,
            logicHz: 60);

        Assert.That(play, Is.EqualTo(100d).Within(0.001d));
    }
}
