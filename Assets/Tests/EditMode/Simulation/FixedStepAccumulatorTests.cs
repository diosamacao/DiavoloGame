using NUnit.Framework;

/// <summary>验证可变渲染时间到固定逻辑步数的累积与追帧行为。</summary>
public sealed class FixedStepAccumulatorTests
{
    const double FixedDelta = 1d / 60d;

    /// <summary>Peek 不得改欠账，且与随后 Consume 的步数一致。</summary>
    [Test]
    public void PeekSteps_DoesNotMutateAndMatchesConsume()
    {
        var accumulator = new FixedStepAccumulator(FixedDelta, 8);
        accumulator.ConsumeSteps(FixedDelta * 0.5d);

        int peeked = accumulator.PeekSteps(FixedDelta * 0.5d);
        Assert.That(accumulator.AccumulatedSeconds, Is.EqualTo(FixedDelta * 0.5d).Within(1e-8d));
        Assert.That(peeked, Is.EqualTo(1));
        Assert.That(accumulator.ConsumeSteps(FixedDelta * 0.5d), Is.EqualTo(peeked));
    }

    /// <summary>两个半帧时间必须合并为一个完整逻辑步。</summary>
    [Test]
    public void ConsumeSteps_CarriesSubFrameRemainder()
    {
        var accumulator = new FixedStepAccumulator(FixedDelta, 8);

        Assert.That(accumulator.ConsumeSteps(FixedDelta * 0.5d), Is.Zero);
        Assert.That(accumulator.ConsumeSteps(FixedDelta * 0.5d), Is.EqualTo(1));
        Assert.That(accumulator.AccumulatedSeconds, Is.EqualTo(0d).Within(1e-8d));
    }

    /// <summary>30FPS 渲染帧必须稳定产生两个 60Hz 逻辑步。</summary>
    [Test]
    public void ConsumeSteps_ConvertsThirtyFpsToTwoLogicSteps()
    {
        var accumulator = new FixedStepAccumulator(FixedDelta, 8);

        int steps = accumulator.ConsumeSteps(1d / 30d);

        Assert.That(steps, Is.EqualTo(2));
    }

    /// <summary>超过单次追帧预算的欠账必须保留给后续调用而不是丢弃。</summary>
    [Test]
    public void ConsumeSteps_PreservesDebtBeyondCatchUpLimit()
    {
        var accumulator = new FixedStepAccumulator(FixedDelta, 2);

        int first = accumulator.ConsumeSteps(FixedDelta * 5d);
        int second = accumulator.ConsumeSteps(0d);
        int third = accumulator.ConsumeSteps(0d);

        Assert.That(first, Is.EqualTo(2));
        Assert.That(second, Is.EqualTo(2));
        Assert.That(third, Is.EqualTo(1));
    }

    /// <summary>负渲染时间不得倒退或产生逻辑步。</summary>
    [Test]
    public void ConsumeSteps_ClampsNegativeDeltaToZero()
    {
        var accumulator = new FixedStepAccumulator(FixedDelta, 8);

        Assert.That(accumulator.ConsumeSteps(-1d), Is.Zero);
        Assert.That(accumulator.AccumulatedSeconds, Is.Zero);
    }

    /// <summary>不足一个逻辑帧的余量必须转换为前后 Pose 的插值比例。</summary>
    [Test]
    public void InterpolationAlpha_UsesRemainingSubFrameTime()
    {
        var accumulator = new FixedStepAccumulator(FixedDelta, 8);

        accumulator.ConsumeSteps(FixedDelta * 0.25d);

        Assert.That(accumulator.InterpolationAlpha, Is.EqualTo(0.25f).Within(0.0001f));
    }

    /// <summary>追帧欠账超过一帧时插值比例必须钳制，避免表现外插。</summary>
    [Test]
    public void InterpolationAlpha_ClampsCatchUpDebtToOne()
    {
        var accumulator = new FixedStepAccumulator(FixedDelta, 1);

        accumulator.ConsumeSteps(FixedDelta * 3d);

        Assert.That(accumulator.InterpolationAlpha, Is.EqualTo(1f));
    }
}
