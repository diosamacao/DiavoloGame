using NUnit.Framework;

/// <summary>远端时间线按 Tick 丢旧，并按延迟取样。</summary>
public sealed class SnapshotTimelineTests
{
    /// <summary>更旧的 Tick 不得压入，也不能把 Latest 回滚。</summary>
    [Test]
    public void TryPush_OlderTick_IsRejected()
    {
        var timeline = new SnapshotTimeline<int>();
        Assert.That(timeline.TryPush(10, 100), Is.True);
        Assert.That(timeline.TryPush(8, 80), Is.False);
        Assert.That(timeline.TryPush(10, 101), Is.False);
        Assert.That(timeline.LatestTick, Is.EqualTo(10));
        Assert.That(timeline.Count, Is.EqualTo(1));
    }

    /// <summary>delay=0 取最新；delay 把取样点推到更早的 Tick。</summary>
    [Test]
    public void TrySample_DelaySelectsEarlierTick()
    {
        var timeline = new SnapshotTimeline<int>();
        Assert.That(timeline.TryPush(8, 8), Is.True);
        Assert.That(timeline.TryPush(9, 9), Is.True);
        Assert.That(timeline.TryPush(10, 10), Is.True);

        Assert.That(timeline.TrySample(0, out int fromLatest, out int toLatest, out float alphaLatest), Is.True);
        Assert.That(toLatest, Is.EqualTo(10));
        Assert.That(fromLatest, Is.EqualTo(9));
        Assert.That(alphaLatest, Is.EqualTo(1f));

        Assert.That(timeline.TrySample(2, out _, out int delayed, out _), Is.True);
        Assert.That(delayed, Is.EqualTo(8));
    }

    /// <summary>隔步样本必须把 to 放到目标之后，alpha 才能落在开区间。</summary>
    [Test]
    public void TrySample_SparseTicks_BracketsTarget()
    {
        var timeline = new SnapshotTimeline<int>();
        Assert.That(timeline.TryPush(8, 8), Is.True);
        Assert.That(timeline.TryPush(10, 10), Is.True);

        Assert.That(
            timeline.TrySample(1, 0f, out long fromTick, out long toTick, out int from, out int to, out float alpha),
            Is.True);
        Assert.That(fromTick, Is.EqualTo(8));
        Assert.That(toTick, Is.EqualTo(10));
        Assert.That(from, Is.EqualTo(8));
        Assert.That(to, Is.EqualTo(10));
        Assert.That(alpha, Is.EqualTo(0.5f).Within(0.001f));

        Assert.That(
            timeline.TrySample(1, 0.5f, out _, out _, out _, out _, out float mid),
            Is.True);
        Assert.That(mid, Is.EqualTo(0.75f).Within(0.001f));
    }

    /// <summary>绝对播放头在两份隔步样本之间给出连续 alpha。</summary>
    [Test]
    public void TrySampleAt_Midpoint_ReturnsHalfAlpha()
    {
        var timeline = new SnapshotTimeline<int>();
        Assert.That(timeline.TryPush(8, 8), Is.True);
        Assert.That(timeline.TryPush(10, 10), Is.True);
        Assert.That(
            timeline.TrySampleAt(9d, out long fromTick, out long toTick, out _, out _, out float alpha),
            Is.True);
        Assert.That(fromTick, Is.EqualTo(8));
        Assert.That(toTick, Is.EqualTo(10));
        Assert.That(alpha, Is.EqualTo(0.5f).Within(0.001f));
    }

    /// <summary>空时间线取样失败。</summary>
    [Test]
    public void TrySample_Empty_ReturnsFalse()
    {
        var timeline = new SnapshotTimeline<int>();
        Assert.That(timeline.TrySample(0, out _, out _, out _), Is.False);
    }
}
