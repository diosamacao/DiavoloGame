using System;
using NUnit.Framework;

/// <summary>ACT 上行复制命令批的往返与严格协议边界。</summary>
public sealed class RoomCodecTests
{
    /// <summary>下行 ReplicationFrame 继续占用既有 Session 应用消息数值 6。</summary>
    [Test]
    public void ReplicationFrame_MessageKind_RemainsSix()
    {
        Assert.That((byte)RoomMessageKind.ReplicationFrame, Is.EqualTo(6));
    }

    /// <summary>MatchEnd 占用 8，避开 Session Kick=7，正文可往返。</summary>
    [Test]
    public void MatchEnd_MessageKind_IsEightAndRoundTrips()
    {
        Assert.That((byte)RoomMessageKind.MatchEnd, Is.EqualTo(8));
        Assert.That((byte)SessionMessageKind.Kick, Is.EqualTo(7));

        var message = new MatchEndMessage(MatchEndReason.Completed, 42);
        byte[] payload = SessionCodec.WriteEnvelope(
            (byte)RoomMessageKind.MatchEnd,
            RoomCodec.WriteMatchEnd(in message));
        SessionCodec.ReadEnvelope(payload, out byte kind, out byte[] body);
        MatchEndMessage restored = RoomCodec.ReadMatchEnd(body);

        Assert.That(kind, Is.EqualTo((byte)RoomMessageKind.MatchEnd));
        Assert.That(restored.Reason, Is.EqualTo(MatchEndReason.Completed));
        Assert.That(restored.Tick, Is.EqualTo(42));
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
