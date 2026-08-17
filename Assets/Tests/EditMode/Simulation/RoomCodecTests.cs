using System;
using NUnit.Framework;

/// <summary>ACT 复制命令批与 AuthorityTick 应用正文往返。</summary>
public sealed class RoomCodecTests
{
    /// <summary>Tick 应用正文带着 appliedHint，外层 Session 类型保持 AuthorityTick。</summary>
    [Test]
    public void AuthorityTickEnvelope_Roundtrip_PreservesHintAndTick()
    {
        var motor = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, radiusMm: 280);
        var idle = new ActionSimSnapshot(null, null, null, 0, 0, false, false, 0);
        ActorReplicationSnapshot snapshot = ReplicationSnapshotBuilder.FromAuthority(
            new SimActorId(1),
            1,
            ReplicationActorKind.Player,
            motor,
            in idle,
            actionId: 0,
            SimActorId.Invalid,
            healthMilli: 100000,
            flagsPacked: 0,
            VitalityReplicationEdge.None);
        var tick = new AuthorityTick(9, new[] { snapshot });
        byte[] payload = SessionCodec.WriteEnvelope(
            (byte)RoomMessageKind.AuthorityTick,
            RoomCodec.WriteAuthorityTickEnvelope(15, ReplicationCodec.WriteAuthorityTick(tick)));

        SessionCodec.ReadEnvelope(payload, out byte kind, out byte[] body);
        Assert.That(kind, Is.EqualTo((byte)RoomMessageKind.AuthorityTick));
        RoomCodec.ReadAuthorityTickEnvelope(body, out long hint, out byte[] tickBytes);
        Assert.That(hint, Is.EqualTo(15));
        Assert.That(ReplicationCodec.ReadAuthorityTick(tickBytes).AuthorityFrame, Is.EqualTo(9));
    }

    /// <summary>命令批按原序往返，正文仍是 ReplicationCodec 单条命令。</summary>
    [Test]
    public void ClientCommandBatch_Roundtrip_PreservesHintsAndButtons()
    {
        var id = new SimActorId(2);
        ulong attack = InputButtonMask.Of(InputButton.Attack);
        var first = new ClientCommand(10, 2, new InputFrame(10, id, 0, 127, attack, attack, 0ul));
        var second = new ClientCommand(11, 2, new InputFrame(11, id, 20, 100, 0ul, attack, 0ul));

        byte[] payload = SessionCodec.WriteEnvelope(
            (byte)RoomMessageKind.ClientCommand,
            RoomCodec.WriteClientCommandBatch(new[] { first, second }));
        SessionCodec.ReadEnvelope(payload, out byte kind, out byte[] body);
        Assert.That(kind, Is.EqualTo((byte)RoomMessageKind.ClientCommand));

        ClientCommand[] restored = RoomCodec.ReadClientCommandBatch(body);
        Assert.That(restored.Length, Is.EqualTo(2));
        Assert.That(restored[0].FrameHint, Is.EqualTo(10));
        Assert.That(restored[0].Input.WasPressed(InputButton.Attack), Is.True);
        Assert.That(restored[1].FrameHint, Is.EqualTo(11));
        Assert.That(restored[1].Input.MoveX, Is.EqualTo((sbyte)20));
    }

    /// <summary>负 Actor count 必须在数组分配前被协议边界拒绝。</summary>
    [Test]
    public void AuthorityTick_NegativeActorCount_ThrowsProtocolError()
    {
        byte[] payload =
        {
            1,
            0, 0, 0, 0, 0, 0, 0, 0,
            0xFF, 0xFF, 0xFF, 0xFF,
        };

        Assert.Catch<InvalidOperationException>(
            () => ReplicationCodec.ReadAuthorityTick(payload));
    }

    /// <summary>单条 ClientCommand 解码后必须拒绝额外尾随字节。</summary>
    [Test]
    public void ClientCommand_TrailingByte_ThrowsProtocolError()
    {
        var id = new SimActorId(2);
        var command = new ClientCommand(
            10,
            2,
            new InputFrame(10, id, 0, 0, 0ul, 0ul, 0ul));
        byte[] valid = ReplicationCodec.WriteClientCommand(in command);
        var malformed = new byte[valid.Length + 1];
        Buffer.BlockCopy(valid, 0, malformed, 0, valid.Length);

        Assert.Catch<InvalidOperationException>(
            () => ReplicationCodec.ReadClientCommand(malformed));
    }
}
