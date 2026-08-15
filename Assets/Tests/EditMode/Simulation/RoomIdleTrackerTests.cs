using NUnit.Framework;

/// <summary>空闲 10 秒剔除的可测时钟。</summary>
public sealed class RoomIdleTrackerTests
{
    /// <summary>未 Touch 不得超时，避免入房前误踢。</summary>
    [Test]
    public void IsTimedOut_BeforeTouch_IsFalse()
    {
        var tracker = new RoomIdleTracker(10000);
        Assert.That(tracker.IsTimedOut(20000), Is.False);
    }

    /// <summary>超过超时阈值必须判定掉线。</summary>
    [Test]
    public void IsTimedOut_AfterIdleTimeout_IsTrue()
    {
        var tracker = new RoomIdleTracker(10000);
        tracker.Touch(1000);
        Assert.That(tracker.IsTimedOut(10999), Is.False);
        Assert.That(tracker.IsTimedOut(11000), Is.True);
    }

    /// <summary>中途 Touch 重新计时。</summary>
    [Test]
    public void Touch_ResetsIdleWindow()
    {
        var tracker = new RoomIdleTracker(10000);
        tracker.Touch(0);
        tracker.Touch(8000);
        Assert.That(tracker.IsTimedOut(17999), Is.False);
        Assert.That(tracker.IsTimedOut(18000), Is.True);
    }
}
