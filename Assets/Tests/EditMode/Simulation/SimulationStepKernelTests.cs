using NUnit.Framework;

/// <summary>固定步追帧核：欠账保留且触及上限时标记 clamped。</summary>
public sealed class SimulationStepKernelTests
{
    /// <summary>毫秒时钟持续累计到整秒时，不得因 float 步长损失边界帧；Peek 不消费欠账。</summary>
    [Test]
    public void MillisecondClock_PeekAndConsumeMaintainSixtyHz()
    {
        var kernel = new SimulationStepKernel();
        int total = 0;
        for (int ms = 1; ms <= 1000; ms++)
        {
            int expected = ms * 60 / 1000 - total;
            Assert.That(kernel.PeekSteps(.001d), Is.EqualTo(expected));
            total += kernel.ConsumeSteps(.001d, out bool clamped);
            Assert.That(clamped, Is.False);
        }
        Assert.That(total, Is.EqualTo(60));
    }

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
