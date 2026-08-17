using System;
using System.Text;

/// <summary>有最大载荷门禁的纯 C# 小端网络写入器。</summary>
public sealed class NetBufferWriter
{
    /// <summary>未显式配置时允许的最大单载荷字节数。</summary>
    public const int DefaultMaxPayloadBytes = 64 * 1024;

    static readonly UTF8Encoding StrictUtf8 = new(false, true);

    readonly int _maxPayloadBytes;
    byte[] _buffer;
    int _count;

    /// <summary>创建指定初始容量和最大载荷的写入器。</summary>
    public NetBufferWriter(int initialCapacity = 256, int maxPayloadBytes = DefaultMaxPayloadBytes)
    {
        if (maxPayloadBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPayloadBytes));
        if (initialCapacity < 0 || initialCapacity > maxPayloadBytes)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));

        _maxPayloadBytes = maxPayloadBytes;
        _buffer = new byte[Math.Max(1, initialCapacity)];
    }

    /// <summary>当前已写入字节数。</summary>
    public int Length => _count;

    /// <summary>当前载荷允许的最大字节数。</summary>
    public int MaxPayloadBytes => _maxPayloadBytes;

    /// <summary>清空已写长度并复用底层 Buffer。</summary>
    public void Reset() => _count = 0;

    /// <summary>写入单字节。</summary>
    public void WriteByte(byte value)
    {
        EnsureCapacity(1);
        _buffer[_count++] = value;
    }

    /// <summary>按补码写入有符号单字节。</summary>
    public void WriteSByte(sbyte value) => WriteByte(unchecked((byte)value));

    /// <summary>按小端写入无符号 16 位整数。</summary>
    public void WriteUInt16(ushort value)
    {
        EnsureCapacity(2);
        _buffer[_count++] = (byte)value;
        _buffer[_count++] = (byte)(value >> 8);
    }

    /// <summary>按小端写入有符号 32 位整数。</summary>
    public void WriteInt32(int value) => WriteUInt32(unchecked((uint)value));

    /// <summary>按小端写入无符号 32 位整数。</summary>
    public void WriteUInt32(uint value)
    {
        EnsureCapacity(4);
        _buffer[_count++] = (byte)value;
        _buffer[_count++] = (byte)(value >> 8);
        _buffer[_count++] = (byte)(value >> 16);
        _buffer[_count++] = (byte)(value >> 24);
    }

    /// <summary>按小端写入有符号 64 位整数。</summary>
    public void WriteInt64(long value) => WriteUInt64(unchecked((ulong)value));

    /// <summary>按小端写入无符号 64 位整数。</summary>
    public void WriteUInt64(ulong value)
    {
        EnsureCapacity(8);
        for (int i = 0; i < 8; i++)
            _buffer[_count++] = (byte)(value >> (i * 8));
    }

    /// <summary>写入指定范围的原始字节，不附加长度。</summary>
    public void WriteBytes(byte[] value, int offset, int length)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        if (offset < 0 || length < 0 || offset > value.Length - length)
            throw new ArgumentOutOfRangeException(nameof(offset), "字节范围越界。");

        EnsureCapacity(length);
        if (length > 0)
            Buffer.BlockCopy(value, offset, _buffer, _count, length);
        _count += length;
    }

    /// <summary>写入 int32 长度前缀和受上限约束的原始字节。</summary>
    public void WriteLengthPrefixedBytes(byte[] value, int maxLength)
    {
        value ??= Array.Empty<byte>();
        ValidateLength(value.Length, maxLength, nameof(value));
        WriteInt32(value.Length);
        WriteBytes(value, 0, value.Length);
    }

    /// <summary>写入 int32 UTF-8 字节长度和严格编码字符串。</summary>
    public void WriteString(string value, int maxByteLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            WriteInt32(0);
            return;
        }

        byte[] utf8;
        try
        {
            utf8 = StrictUtf8.GetBytes(value);
        }
        catch (EncoderFallbackException ex)
        {
            throw new NetBufferException("字符串包含无效 UTF-16 序列。", ex);
        }

        ValidateLength(utf8.Length, maxByteLength, nameof(value));
        WriteInt32(utf8.Length);
        WriteBytes(utf8, 0, utf8.Length);
    }

    /// <summary>复制当前有效载荷；调用方可安全持有返回数组。</summary>
    public byte[] ToArray()
    {
        var result = new byte[_count];
        if (_count > 0)
            Buffer.BlockCopy(_buffer, 0, result, 0, _count);
        return result;
    }

    /// <summary>验证业务字段长度，禁止负上限和超限写入。</summary>
    static void ValidateLength(int length, int maxLength, string parameterName)
    {
        if (maxLength < 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        if (length > maxLength)
            throw new NetBufferException($"{parameterName} 长度 {length} 超过上限 {maxLength}。");
    }

    /// <summary>按需增长但永不超过构造时的最大载荷。</summary>
    void EnsureCapacity(int additional)
    {
        if (additional < 0 || _count > _maxPayloadBytes - additional)
            throw new NetBufferException($"网络载荷超过上限 {_maxPayloadBytes} 字节。");

        int needed = _count + additional;
        if (needed <= _buffer.Length)
            return;

        int doubled = _buffer.Length <= _maxPayloadBytes / 2
            ? _buffer.Length * 2
            : _maxPayloadBytes;
        int next = Math.Max(needed, doubled);
        var grown = new byte[next];
        Buffer.BlockCopy(_buffer, 0, grown, 0, _count);
        _buffer = grown;
    }
}
