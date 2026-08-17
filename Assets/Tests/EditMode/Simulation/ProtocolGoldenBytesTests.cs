using System;
using NUnit.Framework;

/// <summary>冻结 W0 房间与复制协议的既有小端字节布局。</summary>
public sealed class ProtocolGoldenBytesTests
{
    const string JoinRequestGolden =
        "0101040302010D0C0B0A";

    const string ClientCommandBatchGolden =
        "0105010000003500000001080706050403020144332211181716151413121104030201FE7F"
        + "0807060504030201181716151413121128272625242322213412";

    const string EmptyReplicationFrameGolden =
        "0108070605040302011817161514131211"
        + "00000000000000000000000000000000";

    /// <summary>JoinRequest 的房间版本、消息类型和两个版本号保持固定。</summary>
    [Test]
    public void JoinRequest_GoldenBytes_FreezesEnvelopeAndVersionFields()
    {
        var request = new SessionJoinRequest(
            0x01020304,
            new NetworkProtocolVersion(0x0A0B0C0D));
        byte[] expected = ParseHex(JoinRequestGolden);

        byte[] actual = SessionCodec.WriteJoinRequest(in request);

        Assert.That(actual, Is.EqualTo(expected));
        SessionCodec.ReadEnvelope(expected, out byte kind, out byte[] body);
        SessionJoinRequest restored = SessionCodec.ReadJoinRequest(body);
        Assert.That(kind, Is.EqualTo((byte)SessionMessageKind.JoinRequest));
        Assert.That(restored.ContentVersion, Is.EqualTo(0x01020304));
        Assert.That(restored.ProtocolVersion.Value, Is.EqualTo(0x0A0B0C0D));
    }

    /// <summary>命令批信封、条目长度与 InputFrame 的完整固定布局保持不变。</summary>
    [Test]
    public void ClientCommandBatch_GoldenBytes_FreezesInputFrameLayout()
    {
        var input = new InputFrame(
            0x1112131415161718,
            new SimActorId(0x01020304),
            moveX: -2,
            moveY: 127,
            buttonsPressed: 0x0102030405060708ul,
            buttonsHeld: 0x1112131415161718ul,
            buttonsReleased: 0x2122232425262728ul,
            moveReferenceYawQuantized: 0x1234);
        var command = new ClientCommand(
            0x0102030405060708,
            senderPlayerId: 0x11223344,
            in input);
        byte[] expected = ParseHex(ClientCommandBatchGolden);

        byte[] actual = SessionCodec.WriteEnvelope(
            (byte)RoomMessageKind.ClientCommand,
            RoomCodec.WriteClientCommandBatch(new[] { command }));

        Assert.That(actual, Is.EqualTo(expected));
        SessionCodec.ReadEnvelope(expected, out byte kind, out byte[] body);
        ClientCommand[] restored = RoomCodec.ReadClientCommandBatch(body);
        Assert.That(kind, Is.EqualTo((byte)RoomMessageKind.ClientCommand));
        Assert.That(restored, Has.Length.EqualTo(1));
        Assert.That(restored[0].Equals(command), Is.True);
    }

    /// <summary>空 ReplicationFrame 的 Tick、Sequence、三区生命周期计数与应用长度保持固定。</summary>
    [Test]
    public void EmptyReplicationFrame_GoldenBytes_FreezesFrameHeader()
    {
        var frame = new ReplicationFrame(
            new NetTick(0x0102030405060708),
            new NetSequence(0x1112131415161718),
            Array.Empty<SpawnRecord>(),
            Array.Empty<EntityRecord>(),
            Array.Empty<DespawnRecord>(),
            Array.Empty<byte>());
        byte[] expected = ParseHex(EmptyReplicationFrameGolden);
        byte[] actual = ReplicationFrameCodec.Encode(frame);

        Assert.That(actual, Is.EqualTo(expected));
        ReplicationFrame restored = ReplicationFrameCodec.Decode(expected);
        Assert.That(restored.Tick, Is.EqualTo(frame.Tick));
        Assert.That(restored.Sequence, Is.EqualTo(frame.Sequence));
    }

    /// <summary>把紧凑十六进制协议样本转为断言使用的固定字节数组。</summary>
    static byte[] ParseHex(string value)
    {
        if (string.IsNullOrEmpty(value) || (value.Length & 1) != 0)
            throw new ArgumentException("Golden Bytes 必须是非空偶数长度十六进制字符串。", nameof(value));

        var bytes = new byte[value.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
        return bytes;
    }
}
