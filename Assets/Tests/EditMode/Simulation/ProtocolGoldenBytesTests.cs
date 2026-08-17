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

    const string EmptyAuthorityTickGolden =
        "01080706050403020100000000000000000000000000000000";

    const string HitAuthorityTickGolden =
        "01090000000000000000000000010000000900000000000000090000000000000001000000"
        + "02000000030000000400000005000000FAFFFFFF07000000F8FFFFFFE803000018FCFFFF"
        + "0000000000000000";

    /// <summary>JoinRequest 的房间版本、消息类型和两个版本号保持固定。</summary>
    [Test]
    public void JoinRequest_GoldenBytes_FreezesEnvelopeAndVersionFields()
    {
        var request = new RoomJoinRequest(0x01020304, 0x0A0B0C0D);
        byte[] expected = ParseHex(JoinRequestGolden);

        byte[] actual = RoomCodec.WriteJoinRequest(in request);

        Assert.That(actual, Is.EqualTo(expected));
        RoomCodec.ReadEnvelope(expected, out RoomMessageKind kind, out byte[] body);
        RoomJoinRequest restored = RoomCodec.ReadJoinRequest(body);
        Assert.That(kind, Is.EqualTo(RoomMessageKind.JoinRequest));
        Assert.That(restored.ContentVersion, Is.EqualTo(0x01020304));
        Assert.That(restored.ProtocolVersion, Is.EqualTo(0x0A0B0C0D));
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

        byte[] actual = RoomCodec.WriteClientCommandBatch(new[] { command });

        Assert.That(actual, Is.EqualTo(expected));
        RoomCodec.ReadEnvelope(expected, out RoomMessageKind kind, out byte[] body);
        ClientCommand[] restored = RoomCodec.ReadClientCommandBatch(body);
        Assert.That(kind, Is.EqualTo(RoomMessageKind.ClientCommand));
        Assert.That(restored, Has.Length.EqualTo(1));
        Assert.That(restored[0].Equals(command), Is.True);
    }

    /// <summary>无 Actor、Hit 与生命周期边沿的 AuthorityTick 头布局保持固定。</summary>
    [Test]
    public void EmptyAuthorityTick_GoldenBytes_FreezesTickHeader()
    {
        var tick = new AuthorityTick(
            0x0102030405060708,
            Array.Empty<ActorReplicationSnapshot>());
        byte[] expected = ParseHex(EmptyAuthorityTickGolden);

        byte[] actual = ReplicationCodec.WriteAuthorityTick(tick);

        Assert.That(actual, Is.EqualTo(expected));
        Assert.That(ReplicationCodec.ReadAuthorityTick(expected).Equals(tick), Is.True);
    }

    /// <summary>命中键、ActionId、落点、方向及生命周期计数的字节顺序保持固定。</summary>
    [Test]
    public void HitAuthorityTick_GoldenBytes_FreezesHitLayout()
    {
        var hit = new ReplicatedHitEvent(
            9,
            new SimHitKey(
                9,
                new SimActorId(1),
                2,
                3,
                new SimActorId(4)),
            actionId: 5,
            hitXMm: -6,
            hitYMm: 7,
            hitZMm: -8,
            dirXMm: 1000,
            dirZMm: -1000);
        var tick = new AuthorityTick(
            9,
            Array.Empty<ActorReplicationSnapshot>(),
            new[] { hit });
        byte[] expected = ParseHex(HitAuthorityTickGolden);

        byte[] actual = ReplicationCodec.WriteAuthorityTick(tick);

        Assert.That(actual, Is.EqualTo(expected));
        Assert.That(ReplicationCodec.ReadAuthorityTick(expected).Equals(tick), Is.True);
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
