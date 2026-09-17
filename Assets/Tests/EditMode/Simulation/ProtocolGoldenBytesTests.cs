using System;
using NUnit.Framework;

/// <summary>冻结 Session、命令与 V2 复制的小端线格式。</summary>
public sealed class ProtocolGoldenBytesTests
{
    const string JoinRequestGolden =
        "0101040302010D0C0B0A00000000000000000000000000000000";
    const string LifecycleGolden =
        "02181716151413121108070605040302010000010000000000";

    /// <summary>JoinRequest 信封与版本字段保持固定。</summary>
    [Test]
    public void JoinRequest_GoldenBytes_FreezesEnvelopeAndVersionFields()
    {
        var request = new SessionJoinRequest(
            0x01020304,
            new NetworkProtocolVersion(0x0A0B0C0D));
        byte[] expected = ParseHex(JoinRequestGolden);

        Assert.That(SessionCodec.WriteJoinRequest(in request), Is.EqualTo(expected));
        SessionCodec.ReadEnvelope(expected, out byte kind, out byte[] body);
        SessionJoinRequest restored = SessionCodec.ReadJoinRequest(body);
        Assert.That(kind, Is.EqualTo((byte)SessionMessageKind.JoinRequest));
        Assert.That(restored.ContentVersion, Is.EqualTo(0x01020304));
        Assert.That(restored.ProtocolVersion.Value, Is.EqualTo(0x0A0B0C0D));
    }

    /// <summary>空 V2 Lifecycle 的版本、序列、Tick、批头和双计数保持固定。</summary>
    [Test]
    public void EmptyLifecycle_GoldenBytes_FreezesV2Header()
    {
        var source = new ReplicationLifecycle(
            0x1112131415161718,
            new NetTick(0x0102030405060708),
            0,
            1,
            Array.Empty<SpawnRecord>(),
            Array.Empty<DespawnRecord>());
        byte[] expected = ParseHex(LifecycleGolden);

        byte[] actual = ReplicationProtocolV2Codec.EncodeLifecycle(source, expected.Length);
        Assert.That(actual, Is.EqualTo(expected));
        ReplicationLifecycle restored = ReplicationProtocolV2Codec.DecodeLifecycle(expected);
        Assert.That(restored.LifecycleSequence, Is.EqualTo(source.LifecycleSequence));
        Assert.That(restored.Tick, Is.EqualTo(source.Tick));
    }

    /// <summary>把紧凑十六进制协议样本转为固定字节。</summary>
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
