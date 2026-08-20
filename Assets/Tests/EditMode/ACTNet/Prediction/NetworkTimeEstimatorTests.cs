using NUnit.Framework;

/// <summary>网络时钟估计：RTT/jitter、Tick 偏移与插值延迟。</summary>
public sealed class NetworkTimeEstimatorTests
{
    /// <summary>首次 RTT 写入后 jitter 为 0，随后按差值收敛。</summary>
    [Test]
    public void ObserveRtt_UpdatesJitter()
    {
        var clock = new NetworkTimeEstimator();
        clock.ObserveRtt(100);
        Assert.That(clock.RttMs, Is.EqualTo(100));
        Assert.That(clock.JitterMs, Is.Zero);

        clock.ObserveRtt(140);
        Assert.That(clock.RttMs, Is.EqualTo(140));
        Assert.That(clock.JitterMs, Is.GreaterThan(0));
    }

    /// <summary>插值延迟至少一格，且随 RTT 增加。</summary>
    [Test]
    public void InterpolationDelay_GrowsWithRtt()
    {
        var low = new NetworkTimeEstimator();
        low.ObserveRtt(0);
        var high = new NetworkTimeEstimator();
        high.ObserveRtt(100);

        Assert.That(low.InterpolationDelayTicks, Is.GreaterThanOrEqualTo(1));
        Assert.That(high.InterpolationDelayMs, Is.GreaterThan(low.InterpolationDelayMs));
        Assert.That(high.InterpolationDelayMs, Is.LessThanOrEqualTo(150));
    }

    /// <summary>权威 Tick 与本地时钟差写入 TickOffset。</summary>
    [Test]
    public void ObserveAuthorityTick_WritesOffset()
    {
        var clock = new NetworkTimeEstimator();
        clock.ObserveRtt(40);
        clock.ObserveAuthorityTick(localNowMs: 1000, authorityTick: 80, logicHz: 60);

        Assert.That(clock.TickOffset, Is.EqualTo(80 - 60));
        Assert.That(clock.ServerTimeOffsetMs, Is.EqualTo(-20));
    }
}
