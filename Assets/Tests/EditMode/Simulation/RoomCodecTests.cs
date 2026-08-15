using System;
using NUnit.Framework;

/// <summary>房间信封往返与版本拒绝。</summary>
public sealed class RoomCodecTests
{
    /// <summary>JoinAccept 字段必须原样往返。</summary>
    [Test]
    public void JoinAccept_Roundtrip_PreservesFields()
    {
        var accept = new RoomJoinAccept(2, 7, 1, 3, 42);
        byte[] payload = RoomCodec.WriteJoinAccept(in accept);
        RoomCodec.ReadEnvelope(payload, out RoomMessageKind kind, out byte[] body);

        Assert.That(kind, Is.EqualTo(RoomMessageKind.JoinAccept));
        RoomJoinAccept restored = RoomCodec.ReadJoinAccept(body);
        Assert.That(restored.AssignedPlayerId, Is.EqualTo(2));
        Assert.That(restored.AssignedActorId, Is.EqualTo(7));
        Assert.That(restored.HostActorId, Is.EqualTo(1));
        Assert.That(restored.ContentVersion, Is.EqualTo(3));
        Assert.That(restored.AuthorityFrame, Is.EqualTo(42));
    }

    /// <summary>信封版本不匹配必须抛错，避免当 Tick 解。</summary>
    [Test]
    public void ReadEnvelope_UnsupportedVersion_Throws()
    {
        byte[] payload = { 99, (byte)RoomMessageKind.JoinRequest };
        Assert.Throws<InvalidOperationException>(() =>
            RoomCodec.ReadEnvelope(payload, out _, out _));
    }

    /// <summary>Tick 信封带着 appliedHint，正文仍是 ReplicationCodec。</summary>
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
        byte[] payload = RoomCodec.WriteAuthorityTickEnvelope(15, ReplicationCodec.WriteAuthorityTick(tick));

        RoomCodec.ReadEnvelope(payload, out RoomMessageKind kind, out byte[] body);
        Assert.That(kind, Is.EqualTo(RoomMessageKind.AuthorityTick));
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

        byte[] payload = RoomCodec.WriteClientCommandBatch(new[] { first, second });
        RoomCodec.ReadEnvelope(payload, out RoomMessageKind kind, out byte[] body);
        Assert.That(kind, Is.EqualTo(RoomMessageKind.ClientCommand));

        ClientCommand[] restored = RoomCodec.ReadClientCommandBatch(body);
        Assert.That(restored.Length, Is.EqualTo(2));
        Assert.That(restored[0].FrameHint, Is.EqualTo(10));
        Assert.That(restored[0].Input.WasPressed(InputButton.Attack), Is.True);
        Assert.That(restored[1].FrameHint, Is.EqualTo(11));
        Assert.That(restored[1].Input.MoveX, Is.EqualTo((sbyte)20));
    }
}
