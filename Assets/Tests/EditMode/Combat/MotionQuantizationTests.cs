using NUnit.Framework;

/// <summary>验证米/度与毫米/毫度量化往返。</summary>
public sealed class MotionQuantizationTests
{
    /// <summary>米到毫米按 AwayFromZero 四舍五入。</summary>
    [Test]
    public void MetersToMm_RoundsAwayFromZero()
    {
        Assert.That(MotionQuantization.MetersToMm(1f), Is.EqualTo(1000));
        Assert.That(MotionQuantization.MetersToMm(0.001f), Is.EqualTo(1));
        // 0.0025f*1000 在 float 里是 2.4999… 不是中点，不能用来验 AwayFromZero。
        Assert.That(MotionQuantization.MetersToMm(0.0016f), Is.EqualTo(2));
        Assert.That(MotionQuantization.MetersToMm(0.0014f), Is.EqualTo(1));
        Assert.That(MotionQuantization.MetersToMm(-0.0016f), Is.EqualTo(-2));
    }

    /// <summary>偏航包装后再量化，避免 ±360 漂移。</summary>
    [Test]
    public void WrapDegreesToMilliDeg_NormalizesAroundZero()
    {
        Assert.That(MotionQuantization.WrapDegreesToMilliDeg(190f), Is.EqualTo(-170000));
        Assert.That(MotionQuantization.WrapDegreesToMilliDeg(-190f), Is.EqualTo(170000));
    }

    /// <summary>毫度转回度保持比例。</summary>
    [Test]
    public void MilliDegToDegrees_DividesByThousand()
    {
        Assert.That(MotionQuantization.MilliDegToDegrees(1500), Is.EqualTo(1.5f).Within(0.0001f));
    }
}
