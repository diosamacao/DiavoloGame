using NUnit.Framework;

/// <summary>验证输入历史、渲染样本合并与追帧连续状态展开规则。</summary>
public sealed class InputFrameBufferTests
{
    /// <summary>多次渲染采样写同一逻辑帧时不得丢失任一按钮边沿。</summary>
    [Test]
    public void MergeLocalSample_PreservesEdgesUntilLogicFrame()
    {
        var buffer = new InputFrameBuffer();
        var id = new SimActorId(1);
        ulong attack = InputButtonMask.Of(InputButton.Attack);
        var pressed = new InputFrame(0, id, 0, 127, attack, attack, 0ul);
        var released = new InputFrame(0, id, 0, 0, 0ul, 0ul, attack);

        buffer.MergeLocalSample(in pressed);
        buffer.MergeLocalSample(in released);
        InputFrame resolved = buffer.ResolveLocal(0, id);

        Assert.That(resolved.ButtonsPressed, Is.EqualTo(attack));
        Assert.That(resolved.ButtonsReleased, Is.EqualTo(attack));
        Assert.That(resolved.ButtonsHeld, Is.Zero);
    }

    /// <summary>单渲染帧追多个逻辑帧时只延续 Move/Held，不重复 Pressed/Released。</summary>
    [Test]
    public void ResolveLocal_CarriesContinuousStateWithoutRepeatingEdges()
    {
        var buffer = new InputFrameBuffer();
        var id = new SimActorId(3);
        ulong attack = InputButtonMask.Of(InputButton.Attack);
        var first = new InputFrame(0, id, 12, 127, attack, attack, 0ul);
        buffer.Set(in first);

        InputFrame frame0 = buffer.ResolveLocal(0, id);
        InputFrame frame1 = buffer.ResolveLocal(1, id);

        Assert.That(frame0.WasPressed(InputButton.Attack), Is.True);
        Assert.That(frame1.WasPressed(InputButton.Attack), Is.False);
        Assert.That(frame1.IsHeld(InputButton.Attack), Is.True);
        Assert.That(frame1.MoveY, Is.EqualTo((sbyte)127));
    }

    /// <summary>同帧多 Actor 输入必须按 SimActorId 隔离。</summary>
    [Test]
    public void Set_IsolatesActorsAtSameFrame()
    {
        var buffer = new InputFrameBuffer();
        var first = new InputFrame(2, new SimActorId(1), 10, 0, 0ul, 0ul, 0ul);
        var second = new InputFrame(2, new SimActorId(2), 20, 0, 0ul, 0ul, 0ul);
        buffer.Set(in first);
        buffer.Set(in second);

        Assert.That(buffer.TryGetExact(2, first.ActorId, out InputFrame firstRead), Is.True);
        Assert.That(buffer.TryGetExact(2, second.ActorId, out InputFrame secondRead), Is.True);
        Assert.That(firstRead.MoveX, Is.EqualTo((sbyte)10));
        Assert.That(secondRead.MoveX, Is.EqualTo((sbyte)20));
    }

    /// <summary>回放可按逻辑帧写入后逐帧精确读回，不依赖设备。</summary>
    [Test]
    public void RecordedSequence_RoundTripsWithoutDevice()
    {
        var buffer = new InputFrameBuffer();
        var id = new SimActorId(5);
        for (int frame = 0; frame < 4; frame++)
        {
            var input = new InputFrame(frame, id, (sbyte)frame, 0, 0ul, 0ul, 0ul);
            buffer.Set(in input);
        }

        for (int frame = 0; frame < 4; frame++)
        {
            Assert.That(buffer.TryGetExact(frame, id, out InputFrame replayed), Is.True);
            Assert.That(replayed.Frame, Is.EqualTo(frame));
            Assert.That(replayed.MoveX, Is.EqualTo((sbyte)frame));
        }
    }
}
