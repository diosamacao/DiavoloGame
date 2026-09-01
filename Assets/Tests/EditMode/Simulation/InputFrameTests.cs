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
        Assert.That(InputQuantizer.QuantizeYaw(190f), Is.EqualTo((ushort)1900));
        Assert.That(InputQuantizer.QuantizeYaw(-170f), Is.EqualTo((ushort)1900));
        Assert.That(InputQuantizer.QuantizeYaw(360f), Is.Zero);
        Assert.That(InputQuantizer.DequantizeYaw(3599), Is.EqualTo(359.9f).Within(0.001f));
    }

    /// <summary>按钮查询必须严格读取各自生命周期 bitset。</summary>
    [Test]
    public void ButtonQueries_ReadStableBitPositions()
    {
        ulong attack = InputButtonMask.Of(InputButton.Attack);
        ulong dodge = InputButtonMask.Of(InputButton.Dodge);
        ulong switchCharacter = InputButtonMask.Of(InputButton.SwitchCharacter);
        var frame = new InputFrame(
            4,
            new SimActorId(2),
            0,
            0,
            attack | switchCharacter,
            attack | dodge,
            dodge);

        Assert.That(frame.WasPressed(InputButton.Attack), Is.True);
        Assert.That(frame.WasPressed(InputButton.SwitchCharacter), Is.True);
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
        var first = new InputFrame(0, id, 10, 20, attack, attack, 0ul, 100);
        var latest = new InputFrame(0, id, 30, 40, dodge, dodge, attack, 900);

        InputFrame merged = first.MergeSample(in latest);

        Assert.That(merged.MoveX, Is.EqualTo((sbyte)30));
        Assert.That(merged.MoveY, Is.EqualTo((sbyte)40));
        Assert.That(merged.ButtonsPressed, Is.EqualTo(attack | dodge));
        Assert.That(merged.ButtonsHeld, Is.EqualTo(dodge));
        Assert.That(merged.ButtonsReleased, Is.EqualTo(attack));
        Assert.That(merged.MoveReferenceYawQuantized, Is.EqualTo((ushort)900));
    }

    /// <summary>权威改写帧号与 Actor 时不得改动轴和按钮。</summary>
    [Test]
    public void WithIdentity_RewritesFrameAndActor_KeepsAxes()
    {
        var source = new InputFrame(3, new SimActorId(1), 7, -4, 1ul, 2ul, 4ul, 120);
        InputFrame rewritten = source.WithIdentity(11, new SimActorId(8));

        Assert.That(rewritten.Frame, Is.EqualTo(11));
        Assert.That(rewritten.ActorId.Value, Is.EqualTo(8));
        Assert.That(rewritten.MoveX, Is.EqualTo(source.MoveX));
        Assert.That(rewritten.MoveY, Is.EqualTo(source.MoveY));
        Assert.That(rewritten.ButtonsPressed, Is.EqualTo(source.ButtonsPressed));
        Assert.That(rewritten.ButtonsHeld, Is.EqualTo(source.ButtonsHeld));
        Assert.That(rewritten.ButtonsReleased, Is.EqualTo(source.ButtonsReleased));
        Assert.That(rewritten.MoveReferenceYawQuantized, Is.EqualTo(source.MoveReferenceYawQuantized));
    }

    /// <summary>座位级按钮被消费后应从三个生命周期 bitset 移除，并保留其它输入。</summary>
    [Test]
    public void WithoutButton_RemovesOnlyRequestedButton()
    {
        ulong attack = InputButtonMask.Of(InputButton.Attack);
        ulong switchCharacter = InputButtonMask.Of(InputButton.SwitchCharacter);
        var source = new InputFrame(
            5,
            new SimActorId(2),
            10,
            -20,
            attack | switchCharacter,
            switchCharacter,
            switchCharacter,
            90);

        InputFrame filtered = source.WithoutButton(InputButton.SwitchCharacter);

        Assert.That(filtered.WasPressed(InputButton.SwitchCharacter), Is.False);
        Assert.That(filtered.IsHeld(InputButton.SwitchCharacter), Is.False);
        Assert.That(filtered.WasReleased(InputButton.SwitchCharacter), Is.False);
        Assert.That(filtered.WasPressed(InputButton.Attack), Is.True);
        Assert.That(filtered.MoveX, Is.EqualTo(source.MoveX));
        Assert.That(filtered.MoveY, Is.EqualTo(source.MoveY));
    }
}
