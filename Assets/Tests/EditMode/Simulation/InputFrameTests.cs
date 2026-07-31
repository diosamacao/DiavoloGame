using NUnit.Framework;

/// <summary>验证量化输入帧的固定 bit、数值边界与值语义。</summary>
public sealed class InputFrameTests
{
    /// <summary>轴量化必须钳制越界输入并保持端点对称。</summary>
    [Test]
    public void QuantizeAxis_ClampsAndPreservesEndpoints()
    {
        Assert.That(InputQuantizer.QuantizeAxis(2f), Is.EqualTo((sbyte)127));
        Assert.That(InputQuantizer.QuantizeAxis(-2f), Is.EqualTo((sbyte)-127));
        Assert.That(InputQuantizer.QuantizeAxis(0f), Is.Zero);
    }

    /// <summary>偏航量化必须把等价角包裹到同一稳定范围。</summary>
    [Test]
    public void QuantizeYaw_WrapsEquivalentAngles()
    {
        Assert.That(InputQuantizer.QuantizeYaw(190f), Is.EqualTo((short)-1700));
        Assert.That(InputQuantizer.QuantizeYaw(-170f), Is.EqualTo((short)-1700));
    }

    /// <summary>按钮查询必须严格读取各自生命周期 bitset。</summary>
    [Test]
    public void ButtonQueries_ReadStableBitPositions()
    {
        ulong attack = InputButtonMask.Of(InputButton.Attack);
        ulong dodge = InputButtonMask.Of(InputButton.Dodge);
        var frame = new InputFrame(
            4,
            new SimActorId(2),
            0,
            0,
            attack,
            attack | dodge,
            dodge);

        Assert.That(frame.WasPressed(InputButton.Attack), Is.True);
        Assert.That(frame.IsHeld(InputButton.Dodge), Is.True);
        Assert.That(frame.WasReleased(InputButton.Dodge), Is.True);
        Assert.That(frame.WasReleased(InputButton.Attack), Is.False);
    }

    /// <summary>合并高渲染帧样本时边沿累积，连续轴与 Held 取最后采样。</summary>
    [Test]
    public void MergeSample_AccumulatesEdgesAndUsesLatestContinuousState()
    {
        var id = new SimActorId(1);
        ulong attack = InputButtonMask.Of(InputButton.Attack);
        ulong dodge = InputButtonMask.Of(InputButton.Dodge);
        var first = new InputFrame(0, id, 10, 20, attack, attack, 0ul);
        var latest = new InputFrame(0, id, 30, 40, dodge, dodge, attack);

        InputFrame merged = first.MergeSample(in latest);

        Assert.That(merged.MoveX, Is.EqualTo((sbyte)30));
        Assert.That(merged.MoveY, Is.EqualTo((sbyte)40));
        Assert.That(merged.ButtonsPressed, Is.EqualTo(attack | dodge));
        Assert.That(merged.ButtonsHeld, Is.EqualTo(dodge));
        Assert.That(merged.ButtonsReleased, Is.EqualTo(attack));
    }
}
