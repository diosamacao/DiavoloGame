using System;

/// <summary>传输层一次连接生命周期内稳定的连接标识。</summary>
public readonly struct NetConnectionId : IComparable<NetConnectionId>, IEquatable<NetConnectionId>
{
    /// <summary>无效连接标识；有效值从 1 开始。</summary>
    public static NetConnectionId Invalid => default;

    /// <summary>底层稳定整数值。</summary>
    public int Value { get; }

    /// <summary>是否可用于连接注册与定向发送。</summary>
    public bool IsValid => Value > 0;

    /// <summary>由正整数创建连接标识。</summary>
    public NetConnectionId(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "NetConnectionId 必须大于 0。");
        Value = value;
    }

    /// <inheritdoc />
    public int CompareTo(NetConnectionId other) => Value.CompareTo(other.Value);

    /// <inheritdoc />
    public bool Equals(NetConnectionId other) => Value == other.Value;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is NetConnectionId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value;

    /// <inheritdoc />
    public override string ToString() => IsValid ? Value.ToString() : "Invalid";

    /// <summary>判断两个连接标识是否相同。</summary>
    public static bool operator ==(NetConnectionId left, NetConnectionId right) => left.Equals(right);

    /// <summary>判断两个连接标识是否不同。</summary>
    public static bool operator !=(NetConnectionId left, NetConnectionId right) => !left.Equals(right);
}
