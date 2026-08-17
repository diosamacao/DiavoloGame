using System;
using NUnit.Framework;

/// <summary>验证 ACT 帧级应用载荷的唯一命中线布局与严格协议边界。</summary>
public sealed class ActReplicationApplicationPayloadCodecTests
{
    /// <summary>Hint 与全部命中字段往返保持一致。</summary>
    [Test]
    public void RoundTrip_PreservesHintAndHits()
    {
        ReplicatedHitEvent hit = CreateHit();
        var payload = new ActReplicationApplicationPayload(42, new[] { hit });

        ActReplicationApplicationPayload restored =
            ActReplicationApplicationPayloadCodec.Decode(
                ActReplicationApplicationPayloadCodec.Encode(payload));

        Assert.That(restored.AppliedClientFrameHint, Is.EqualTo(42));
        Assert.That(restored.Hits, Has.Length.EqualTo(1));
        Assert.That(restored.Hits[0].Equals(hit), Is.True);
    }

    /// <summary>null 命中数组编码为空数组，且不会在访问副本时泄漏内部状态。</summary>
    [Test]
    public void Empty_NullHits_RoundTripsAsEmpty()
    {
        var payload = new ActReplicationApplicationPayload(0, null);

        ActReplicationApplicationPayload restored =
            ActReplicationApplicationPayloadCodec.Decode(
                ActReplicationApplicationPayloadCodec.Encode(payload));

        Assert.That(restored.AppliedClientFrameHint, Is.Zero);
        Assert.That(restored.Hits, Is.Empty);
    }

    /// <summary>Codec 对 null 对象与 null 字节输入均明确拒绝。</summary>
    [Test]
    public void NullInputs_Throw()
    {
        Assert.Throws<ArgumentNullException>(
            () => ActReplicationApplicationPayloadCodec.Encode(null));
        Assert.Throws<ArgumentNullException>(
            () => ActReplicationApplicationPayloadCodec.Decode(null));
    }

    /// <summary>未知版本必须在读取后续字段前被拒绝。</summary>
    [Test]
    public void Decode_UnsupportedVersion_Throws()
    {
        byte[] bytes =
        {
            2,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0,
        };

        Assert.Throws<InvalidOperationException>(
            () => ActReplicationApplicationPayloadCodec.Decode(bytes));
    }

    /// <summary>编码端同样执行 MaxHits 门禁，不能生成客户端必拒绝的载荷。</summary>
    [Test]
    public void Encode_OverLimitHits_Throws()
    {
        var hits = new ReplicatedHitEvent[ActReplicationApplicationPayloadCodec.MaxHits + 1];
        var payload = new ActReplicationApplicationPayload(0, hits);

        Assert.Catch<InvalidOperationException>(
            () => ActReplicationApplicationPayloadCodec.Encode(payload));
    }

    /// <summary>负 count 在分配数组前被拒绝。</summary>
    [Test]
    public void Decode_NegativeCount_Throws()
    {
        byte[] bytes =
        {
            ActReplicationApplicationPayloadCodec.Version,
            0, 0, 0, 0, 0, 0, 0, 0,
            0xFF, 0xFF, 0xFF, 0xFF,
        };

        Assert.Catch<InvalidOperationException>(
            () => ActReplicationApplicationPayloadCodec.Decode(bytes));
    }

    /// <summary>超过 MaxHits 的 count 在分配数组前被拒绝。</summary>
    [Test]
    public void Decode_OverLimitCount_Throws()
    {
        int count = ActReplicationApplicationPayloadCodec.MaxHits + 1;
        byte[] bytes =
        {
            ActReplicationApplicationPayloadCodec.Version,
            0, 0, 0, 0, 0, 0, 0, 0,
            (byte)count, (byte)(count >> 8), (byte)(count >> 16), (byte)(count >> 24),
        };

        Assert.Catch<InvalidOperationException>(
            () => ActReplicationApplicationPayloadCodec.Decode(bytes));
    }

    /// <summary>命中字段截断必须失败，不能返回半条事件。</summary>
    [Test]
    public void Decode_TruncatedHit_Throws()
    {
        byte[] valid = ActReplicationApplicationPayloadCodec.Encode(
            new ActReplicationApplicationPayload(1, new[] { CreateHit() }));
        Array.Resize(ref valid, valid.Length - 1);

        Assert.Catch<InvalidOperationException>(
            () => ActReplicationApplicationPayloadCodec.Decode(valid));
    }

    /// <summary>完整载荷后的任意尾随字节必须失败。</summary>
    [Test]
    public void Decode_TrailingByte_Throws()
    {
        byte[] valid = ActReplicationApplicationPayloadCodec.Encode(
            new ActReplicationApplicationPayload(1, Array.Empty<ReplicatedHitEvent>()));
        Array.Resize(ref valid, valid.Length + 1);

        Assert.Catch<InvalidOperationException>(
            () => ActReplicationApplicationPayloadCodec.Decode(valid));
    }

    /// <summary>冻结 V1：版本字节后依次为 little-endian hint、count 与命中字段。</summary>
    [Test]
    public void GoldenBytes_FreezesVersionOneLayout()
    {
        const string golden =
            "01080706050403020101000000"
            + "0900000000000000090000000000000001000000020000000300000004000000"
            + "05000000FAFFFFFF07000000F8FFFFFFE803000018FCFFFF";
        byte[] actual = ActReplicationApplicationPayloadCodec.Encode(
            new ActReplicationApplicationPayload(
                0x0102030405060708,
                new[] { CreateHit() }));

        Assert.That(actual, Is.EqualTo(ParseHex(golden)));
    }

    // 返回覆盖全部命中线字段的固定样本。
    static ReplicatedHitEvent CreateHit() =>
        new(
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

    // 将紧凑十六进制 Golden 样本转换为断言字节。
    static byte[] ParseHex(string value)
    {
        var bytes = new byte[value.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
        return bytes;
    }
}
