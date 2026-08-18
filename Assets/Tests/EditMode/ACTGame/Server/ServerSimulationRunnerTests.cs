using NUnit.Framework;

/// <summary>Dedicated 单调时钟 Runner：首拍对齐、其后按 dt 追帧。</summary>
public sealed class ServerSimulationRunnerTests
{
    /// <summary>首拍只对齐时钟，第二拍 50ms 应推进 3 个 60Hz 步。</summary>
    [Test]
    public void Advance_SecondTick_StepsFixedFrames()
    {
        int stepped = 0;
        var runner = new ServerSimulationRunner(
            new SimulationStepKernel(new SimulationConfig(logicHz: 60, maxFrameCatchUp: 8)),
            () => stepped++);

        runner.Advance(0);
        Assert.That(stepped, Is.EqualTo(0));

        runner.Advance(50);
        Assert.That(stepped, Is.EqualTo(3));
        Assert.That(runner.Metrics.StepsTaken, Is.EqualTo(3));
        Assert.That(runner.Metrics.CatchUpClamped, Is.False);
    }
}
