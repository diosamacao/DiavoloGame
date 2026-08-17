using System;

/// <summary>连接内非负且单调的消息序列号，用于丢弃旧包与关联 ACK。</summary>
public readonly struct NetSequence : IComparable<NetSequence>, IEquatable<NetSequence>
{
    readonly ulong _encodedValue;

    /// <summary>尚无有效消息序列。</summary>
    public static NetSequence Invalid => default;

    /// <summary>底层 64 位序列号；Invalid 读取为 -1。</summary>
    public long Value => IsValid ? checked((long)(_encodedValue - 1ul)) : -1;

    /// <summary>是否代表有效消息序列。</summary>
    public bool IsValid => _encodedValue != 0ul;

    /// <summary>由非负长整数创建消息序列。</summary>
    public NetSequence(long value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "NetSequence 不能为负。");
        _encodedValue = checked((ulong)value + 1ul);
    }

    /// <summary>返回下一序列号；溢出时抛出异常。</summary>
    public NetSequence Next()
    {
        if (!IsValid)
            throw new InvalidOperationException("Invalid NetSequence 没有下一值。");
        return new NetSequence(checked(Value + 1));
    }

    /// <inheritdoc />
    public int CompareTo(NetSequence other) => _encodedValue.CompareTo(other._encodedValue);

    /// <inheritdoc />
    public bool Equals(NetSequence other) => _encodedValue == other._encodedValue;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is NetSequence other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _encodedValue.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => IsValid ? Value.ToString() : "Invalid";

    /// <summary>判断两个消息序列是否相同。</summary>
    public static bool operator ==(NetSequence left, NetSequence right) => left.Equals(right);

    /// <summary>判断两个消息序列是否不同。</summary>
    public static bool operator !=(NetSequence left, NetSequence right) => !left.Equals(right);
}
