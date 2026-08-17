using System;
using System.Text;

/// <summary>验证载荷总长、字段长度和读取边界的纯 C# 小端网络读取器。</summary>
public sealed class NetBufferReader
{
    static readonly UTF8Encoding StrictUtf8 = new(false, true);

    readonly byte[] _buffer;
    int _offset;

    /// <summary>创建读取器并立即拒绝超过最大载荷的输入。</summary>
    public NetBufferReader(
        byte[] payload,
        int maxPayloadBytes = NetBufferWriter.DefaultMaxPayloadBytes)
    {
        _buffer = payload ?? throw new ArgumentNullException(nameof(payload));
        if (maxPayloadBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPayloadBytes));
        if (payload.Length > maxPayloadBytes)
            throw new NetBufferException(
                $"网络载荷长度 {payload.Length} 超过上限 {maxPayloadBytes}。");
    }

    /// <summary>当前已消费字节数。</summary>
    public int Position => _offset;

    /// <summary>尚未消费的字节数。</summary>
    public int Remaining => _buffer.Length - _offset;

    /// <summary>是否已恰好消费完整载荷。</summary>
    public bool IsComplete => _offset == _buffer.Length;

    /// <summary>读取单字节。</summary>
    public byte ReadByte()
    {
        EnsureAvailable(1);
        return _buffer[_offset++];
    }

    /// <summary>按补码读取有符号单字节。</summary>
    public sbyte ReadSByte() => unchecked((sbyte)ReadByte());

    /// <summary>按小端读取无符号 16 位整数。</summary>
    public ushort ReadUInt16()
    {
        EnsureAvailable(2);
        int lo = _buffer[_offset++];
        int hi = _buffer[_offset++];
        return (ushort)(lo | (hi << 8));
    }

    /// <summary>按小端读取有符号 32 位整数。</summary>
    public int ReadInt32() => unchecked((int)ReadUInt32());

    /// <summary>按小端读取无符号 32 位整数。</summary>
    public uint ReadUInt32()
    {
        EnsureAvailable(4);
        uint value = _buffer[_offset]
            | ((uint)_buffer[_offset + 1] << 8)
            | ((uint)_buffer[_offset + 2] << 16)
            | ((uint)_buffer[_offset + 3] << 24);
        _offset += 4;
        return value;
    }

    /// <summary>按小端读取有符号 64 位整数。</summary>
    public long ReadInt64() => unchecked((long)ReadUInt64());

    /// <summary>按小端读取无符号 64 位整数。</summary>
    public ulong ReadUInt64()
    {
        EnsureAvailable(8);
        ulong value = 0ul;
        for (int i = 0; i < 8; i++)
            value |= (ulong)_buffer[_offset++] << (i * 8);
        return value;
    }

    /// <summary>读取受业务上限约束的非负 int32 长度。</summary>
    public int ReadLength(int maxLength)
    {
        if (maxLength < 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength));

        int length = ReadInt32();
        if (length < 0 || length > maxLength)
            throw new NetBufferException($"字段长度 {length} 不在 [0,{maxLength}]。");
        return length;
    }

    /// <summary>读取指定长度原始字节并返回独立数组。</summary>
    public byte[] ReadBytes(int length)
    {
        if (length < 0)
            throw new NetBufferException($"字段长度不能为负：{length}。");

        EnsureAvailable(length);
        if (length == 0)
            return Array.Empty<byte>();

        var result = new byte[length];
        Buffer.BlockCopy(_buffer, _offset, result, 0, length);
        _offset += length;
        return result;
    }

    /// <summary>读取 int32 长度前缀和受上限约束的原始字节。</summary>
    public byte[] ReadLengthPrefixedBytes(int maxLength) =>
        ReadBytes(ReadLength(maxLength));

    /// <summary>读取 int32 UTF-8 字节长度并使用严格编码解码。</summary>
    public string ReadString(int maxByteLength)
    {
        int length = ReadLength(maxByteLength);
        if (length == 0)
            return string.Empty;

        EnsureAvailable(length);
        try
        {
            string value = StrictUtf8.GetString(_buffer, _offset, length);
            _offset += length;
            return value;
        }
        catch (DecoderFallbackException ex)
        {
            throw new NetBufferException("字符串包含无效 UTF-8 字节。", ex);
        }
    }

    /// <summary>要求载荷恰好消费完毕，拒绝未声明的尾随字节。</summary>
    public void EnsureComplete()
    {
        if (!IsComplete)
            throw new NetBufferException($"网络载荷仍有 {Remaining} 个未消费字节。");
    }

    /// <summary>确保接下来的读取不会越过载荷末尾。</summary>
    void EnsureAvailable(int additional)
    {
        if (additional < 0 || _offset > _buffer.Length - additional)
        {
            throw new NetBufferException(
                $"网络载荷长度不足：position={_offset}, need={additional}, total={_buffer.Length}。");
        }
    }
}
