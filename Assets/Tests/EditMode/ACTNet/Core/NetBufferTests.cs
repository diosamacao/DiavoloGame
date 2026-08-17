using NUnit.Framework;

/// <summary>冻结 ACTNet.Core 小端编码和恶意载荷边界检查。</summary>
public sealed class NetBufferTests
{
    /// <summary>所有整数原语必须使用与现有协议一致的小端布局。</summary>
    [Test]
    public void PrimitiveWrites_UseExplicitLittleEndianLayout()
    {
        var writer = new NetBufferWriter();
        writer.WriteByte(0xAB);
        writer.WriteSByte(-2);
        writer.WriteUInt16(0x1234);
        writer.WriteInt32(-2);
        writer.WriteUInt32(0x89ABCDEFu);
        writer.WriteInt64(0x0102030405060708L);
        writer.WriteUInt64(0xFEDCBA9876543210ul);

        Assert.That(writer.ToArray(), Is.EqualTo(new byte[]
        {
            0xAB,
            0xFE,
            0x34, 0x12,
            0xFE, 0xFF, 0xFF, 0xFF,
            0xEF, 0xCD, 0xAB, 0x89,
            0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01,
            0x10, 0x32, 0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE,
        }));
    }

    /// <summary>Reader 必须无损还原 Writer 输出并恰好消费完整载荷。</summary>
    [Test]
    public void PrimitiveAndVariableFields_RoundTrip()
    {
        var writer = new NetBufferWriter();
        writer.WriteByte(0xAB);
        writer.WriteSByte(-2);
        writer.WriteUInt16(0x1234);
        writer.WriteInt32(-2);
        writer.WriteUInt32(0x89ABCDEFu);
        writer.WriteInt64(-1234567890123456789L);
        writer.WriteUInt64(0xFEDCBA9876543210ul);
        writer.WriteLengthPrefixedBytes(new byte[] { 1, 2, 3 }, 3);
        writer.WriteString("ACT网络", 32);

        var reader = new NetBufferReader(writer.ToArray());

        Assert.That(reader.ReadByte(), Is.EqualTo(0xAB));
        Assert.That(reader.ReadSByte(), Is.EqualTo(-2));
        Assert.That(reader.ReadUInt16(), Is.EqualTo(0x1234));
        Assert.That(reader.ReadInt32(), Is.EqualTo(-2));
        Assert.That(reader.ReadUInt32(), Is.EqualTo(0x89ABCDEFu));
        Assert.That(reader.ReadInt64(), Is.EqualTo(-1234567890123456789L));
        Assert.That(reader.ReadUInt64(), Is.EqualTo(0xFEDCBA9876543210ul));
        Assert.That(reader.ReadLengthPrefixedBytes(3), Is.EqualTo(new byte[] { 1, 2, 3 }));
        Assert.That(reader.ReadString(32), Is.EqualTo("ACT网络"));
        reader.EnsureComplete();
    }

    /// <summary>Writer 必须在写入前拒绝超过单载荷硬上限的操作。</summary>
    [Test]
    public void Writer_RejectsPayloadOverflow()
    {
        var writer = new NetBufferWriter(initialCapacity: 4, maxPayloadBytes: 8);
        writer.WriteInt64(1);

        Assert.That(writer.Length, Is.EqualTo(8));
        Assert.Throws<NetBufferException>(() => writer.WriteByte(1));
    }

    /// <summary>Writer 必须拒绝超过业务字段上限的数组和 UTF-8 字符串。</summary>
    [Test]
    public void Writer_RejectsFieldLengthOverflow()
    {
        var writer = new NetBufferWriter();

        Assert.Throws<NetBufferException>(
            () => writer.WriteLengthPrefixedBytes(new byte[4], 3));
        Assert.Throws<NetBufferException>(
            () => writer.WriteString("网络", 5));
    }

    /// <summary>Reader 构造时必须拒绝超过配置上限的完整载荷。</summary>
    [Test]
    public void Reader_RejectsOversizedPayload()
    {
        Assert.Throws<NetBufferException>(
            () => new NetBufferReader(new byte[9], maxPayloadBytes: 8));
    }

    /// <summary>长度前缀为负或超过字段上限时不得继续读取。</summary>
    [Test]
    public void Reader_RejectsInvalidLengthPrefix()
    {
        var negative = new NetBufferReader(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
        var oversized = new NetBufferReader(new byte[] { 0x04, 0x00, 0x00, 0x00 });

        Assert.Throws<NetBufferException>(() => negative.ReadLength(32));
        Assert.Throws<NetBufferException>(() => oversized.ReadLength(3));
    }

    /// <summary>声明长度超过剩余字节时必须以协议异常失败。</summary>
    [Test]
    public void Reader_RejectsTruncatedField()
    {
        var reader = new NetBufferReader(new byte[]
        {
            0x03, 0x00, 0x00, 0x00,
            0x01, 0x02,
        });

        Assert.Throws<NetBufferException>(() => reader.ReadLengthPrefixedBytes(3));
    }

    /// <summary>严格 UTF-8 解码必须拒绝损坏字节而不是静默替换字符。</summary>
    [Test]
    public void Reader_RejectsMalformedUtf8()
    {
        var reader = new NetBufferReader(new byte[]
        {
            0x02, 0x00, 0x00, 0x00,
            0xC3, 0x28,
        });

        Assert.Throws<NetBufferException>(() => reader.ReadString(8));
    }

    /// <summary>消息解码完成后必须能显式拒绝未声明尾随字节。</summary>
    [Test]
    public void EnsureComplete_RejectsTrailingBytes()
    {
        var reader = new NetBufferReader(new byte[] { 1, 2 });
        Assert.That(reader.ReadByte(), Is.EqualTo(1));

        Assert.Throws<NetBufferException>(() => reader.EnsureComplete());
    }
}
