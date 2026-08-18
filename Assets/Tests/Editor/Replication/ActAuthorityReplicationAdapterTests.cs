using NUnit.Framework;

/// <summary>验证 ACT 权威适配器把远端命令映射到下一权威输入帧的规则。</summary>
public sealed class ActAuthorityReplicationAdapterTests
{
    /// <summary>适配器应跳过已确认 Hint，并把新边沿与最新连续状态写入下一帧。</summary>
    [Test]
    public void ApplyGuestCommands_MergesUnappliedCommandsIntoNextAuthorityFrame()
    {
        var adapter = new ActAuthorityReplicationAdapter(new ActContentRegistry());
        var buffer = new InputFrameBuffer();
        var actorId = new SimActorId(8);
        ulong attack = InputButtonMask.Of(InputButton.Attack);
        var commands = new[]
        {
            new ClientCommand(10, 2, new InputFrame(10, actorId, 0, 20, 0ul, 0ul, 0ul)),
            new ClientCommand(11, 2, new InputFrame(11, actorId, 0, 50, attack, attack, 0ul)),
            new ClientCommand(12, 2, new InputFrame(12, actorId, 30, 80, 0ul, 0ul, 0ul)),
        };

        ActAuthorityInputApplyResult result = adapter.ApplyGuestCommands(
            buffer,
            currentFrame: 39,
            actorId,
            commands,
            lastAppliedHint: 10);

        Assert.That(result.Applied, Is.True);
        Assert.That(result.NewestHint, Is.EqualTo(12));
        Assert.That(buffer.TryGetExact(40, actorId, out InputFrame input), Is.True);
        Assert.That(input.WasPressed(InputButton.Attack), Is.True);
        Assert.That(input.MoveX, Is.EqualTo((sbyte)30));
        Assert.That(input.MoveY, Is.EqualTo((sbyte)80));
    }

    /// <summary>全为旧 Hint 时不得覆盖已为下一帧采集的输入。</summary>
    [Test]
    public void ApplyGuestCommands_AllHintsApplied_PreservesExistingInput()
    {
        var adapter = new ActAuthorityReplicationAdapter(new ActContentRegistry());
        var buffer = new InputFrameBuffer();
        var actorId = new SimActorId(3);
        var existing = new InputFrame(9, actorId, 7, 9, 0ul, 0ul, 0ul);
        buffer.Set(in existing);
        var commands = new[]
        {
            new ClientCommand(4, 2, new InputFrame(4, actorId, 1, 2, 0ul, 0ul, 0ul)),
        };

        ActAuthorityInputApplyResult result = adapter.ApplyGuestCommands(
            buffer,
            currentFrame: 8,
            actorId,
            commands,
            lastAppliedHint: 4);

        Assert.That(result.Applied, Is.False);
        Assert.That(result.NewestHint, Is.EqualTo(4));
        Assert.That(buffer.TryGetExact(9, actorId, out InputFrame preserved), Is.True);
        Assert.That(preserved, Is.EqualTo(existing));
    }
}
