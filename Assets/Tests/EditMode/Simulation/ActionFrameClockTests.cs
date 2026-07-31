using System;
using NUnit.Framework;

/// <summary>验证整数模拟帧到动作采样帧的确定性换算与终止边界。</summary>
public sealed class ActionFrameClockTests
{
    /// <summary>30Hz 动作在 60Hz 模拟中必须每两个模拟帧推进一帧。</summary>
    [Test]
    public void Advance_ThirtyHzActionPreservesDurationAtSixtyHz()
    {
        var clock = new ActionFrameClock();

        Assert.That(clock.Advance(30, 60, 30), Is.Zero);
        Assert.That(clock.CurrentFrame, Is.Zero);
        Assert.That(clock.Advance(30, 60, 30), Is.EqualTo(1));
        Assert.That(clock.CurrentFrame, Is.EqualTo(1));
    }

    /// <summary>动作应在完整持续时间后进入 TotalFrames 终止哨兵，而不是提前停在末帧。</summary>
    [Test]
    public void Advance_ReachesTerminalFrameAfterFullDuration()
    {
        var clock = new ActionFrameClock();

        for (int i = 0; i < 59; i++)
            clock.Advance(30, 60, 30);

        Assert.That(clock.CurrentFrame, Is.EqualTo(29));
        Assert.That(clock.Advance(30, 60, 30), Is.EqualTo(1));
        Assert.That(clock.CurrentFrame, Is.EqualTo(30));
        Assert.That(clock.Advance(30, 60, 30), Is.Zero);
    }

    /// <summary>不能整除的采样率也必须仅靠整数余数产生可重复序列。</summary>
    [Test]
    public void Advance_NonDivisibleRateCarriesIntegerRemainder()
    {
        var clock = new ActionFrameClock();

        for (int i = 0; i < 5; i++)
            clock.Advance(24, 60, 100);

        Assert.That(clock.CurrentFrame, Is.EqualTo(2));
    }

    /// <summary>非法采样率必须立即拒绝，避免静默产生不同步时钟。</summary>
    [Test]
    public void Advance_RejectsInvalidRates()
    {
        var clock = new ActionFrameClock();

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(0, 60, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(30, 0, 10));
    }
}
