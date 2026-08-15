using NUnit.Framework;

/// <summary>冗余命令批必须合并未应用边沿，不能只留下最后一帧。</summary>
public sealed class RoomRemoteInputMergeTests
{
    /// <summary>已应用 Hint 的重发跳过；中间帧 Attack 与最新轴一并写入。</summary>
    [Test]
    public void TryMergeUnapplied_SkipsAckedAndOrsAttackEdge()
    {
        var actorId = new SimActorId(8);
        ulong attack = InputButtonMask.Of(InputButton.Attack);
        var commands = new[]
        {
            new ClientCommand(10, 2, new InputFrame(10, actorId, 0, 40, 0ul, 0ul, 0ul)),
            new ClientCommand(11, 2, new InputFrame(11, actorId, 0, 50, attack, attack, 0ul)),
            new ClientCommand(12, 2, new InputFrame(12, actorId, 30, 80, 0ul, 0ul, 0ul)),
        };

        bool merged = RoomRemoteInputMerge.TryMergeUnapplied(
            commands,
            lastAppliedHint: 10,
            targetFrame: 40,
            actorId,
            out InputFrame input,
            out long newestHint);

        Assert.That(merged, Is.True);
        Assert.That(newestHint, Is.EqualTo(12));
        Assert.That(input.Frame, Is.EqualTo(40));
        Assert.That(input.ActorId, Is.EqualTo(actorId));
        Assert.That(input.WasPressed(InputButton.Attack), Is.True);
        Assert.That(input.MoveX, Is.EqualTo((sbyte)30));
        Assert.That(input.MoveY, Is.EqualTo((sbyte)80));
    }

    /// <summary>整批都是已应用 Hint 时不得改下一帧输入。</summary>
    [Test]
    public void TryMergeUnapplied_AllAcked_ReturnsFalse()
    {
        var actorId = new SimActorId(3);
        var commands = new[]
        {
            new ClientCommand(4, 2, new InputFrame(4, actorId, 1, 2, 0ul, 0ul, 0ul)),
        };

        bool merged = RoomRemoteInputMerge.TryMergeUnapplied(
            commands,
            lastAppliedHint: 4,
            targetFrame: 9,
            actorId,
            out _,
            out long newestHint);

        Assert.That(merged, Is.False);
        Assert.That(newestHint, Is.EqualTo(4));
    }
}
