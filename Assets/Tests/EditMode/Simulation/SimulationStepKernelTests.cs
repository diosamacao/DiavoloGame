using NUnit.Framework;

/// <summary>固定步追帧核：欠账保留且触及上限时标记 clamped。</summary>
public sealed class SimulationStepKernelTests
{
    /// <summary>一次注入超过追帧上限的时间只走上限步，并标 clamped。</summary>
    [Test]
    public void ConsumeSteps_ClampsToMaxCatchUp()
    {
        var kernel = new SimulationStepKernel(new SimulationConfig(logicHz: 60, maxFrameCatchUp: 3));
        int steps = kernel.ConsumeSteps(1.0d, out bool clamped);

        Assert.That(steps, Is.EqualTo(3));
        Assert.That(clamped, Is.True);
    }

    /// <summary>不足一步的时间不步进，也不标 clamped。</summary>
    [Test]
    public void ConsumeSteps_PartialFrame_DoesNotStep()
    {
        var kernel = new SimulationStepKernel(new SimulationConfig(logicHz: 60, maxFrameCatchUp: 8));
        int steps = kernel.ConsumeSteps(1.0d / 120.0d, out bool clamped);

        Assert.That(steps, Is.EqualTo(0));
        Assert.That(clamped, Is.False);
    }
}
