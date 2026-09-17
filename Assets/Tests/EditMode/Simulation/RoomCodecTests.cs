using System;
using NUnit.Framework;

/// <summary>ACT V2 房间消息编号、命令批与 MatchEnd 严格边界测试。</summary>
public sealed class RoomCodecTests
{
    /// <summary>V2 生命周期/快照分轨编号固定，且不占用 Session Kick。</summary>
    [Test]
    public void V2MessageKinds_AreStableAndDistinct()
    {
        Assert.That((byte)ActRoomMessageType.ClientCommand, Is.EqualTo(5));
        Assert.That((byte)ActRoomMessageType.ReplicationLifecycle, Is.EqualTo(6));
        Assert.That((byte)ActRoomMessageType.MatchEnd, Is.EqualTo(8));
        Assert.That((byte)ActRoomMessageType.ReplicationRecover, Is.EqualTo(10));
        Assert.That((byte)ActRoomMessageType.ReplicationSnapshot, Is.EqualTo(11));
        Assert.That((byte)SessionMessageKind.Kick, Is.EqualTo(7));
    }

    /// <summary>命令批按原序往返，并拒绝批正文尾随字节。</summary>
    [Test]
    public void ClientCommandBatch_RoundTripsAndRejectsTrailingByte()
    {
        var id = new SimActorId(2);
        ulong attack = InputButtonMask.Of(InputButton.Attack);
        var first = new ClientCommand(10, 2, new InputFrame(10, id, 0, 127, attack, attack, 0ul));
        var second = new ClientCommand(11, 2, new InputFrame(11, id, 20, 100, 0ul, attack, 0ul));
        byte[] body = RoomCodec.WriteClientCommandBatch(new[] { first, second });

        ClientCommand[] restored = RoomCodec.ReadClientCommandBatch(body);
        Assert.That(restored, Has.Length.EqualTo(2));
        Assert.That(restored[0].Equals(first), Is.True);
        Assert.That(restored[1].Equals(second), Is.True);

        Array.Resize(ref body, body.Length + 1);
        Assert.Catch<InvalidOperationException>(() => RoomCodec.ReadClientCommandBatch(body));
    }

    /// <summary>MatchEnd 经 Session 信封后保持原因与 Tick。</summary>
    [Test]
    public void MatchEnd_RoundTripsThroughSessionEnvelope()
    {
        var source = new MatchEndMessage(MatchEndReason.Completed, 42);
        byte[] packet = SessionCodec.WriteEnvelope(
            (byte)ActRoomMessageType.MatchEnd,
            RoomCodec.WriteMatchEnd(in source));

        SessionCodec.ReadEnvelope(packet, out byte kind, out byte[] body);
        MatchEndMessage restored = RoomCodec.ReadMatchEnd(body);
        Assert.That(kind, Is.EqualTo((byte)ActRoomMessageType.MatchEnd));
        Assert.That(restored.Reason, Is.EqualTo(source.Reason));
        Assert.That(restored.Tick, Is.EqualTo(source.Tick));
    }
}
