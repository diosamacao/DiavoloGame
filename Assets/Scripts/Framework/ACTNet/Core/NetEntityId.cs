using System;

/// <summary>一次网络世界生命周期内稳定且不复用的复制实体标识。</summary>
public readonly struct NetEntityId : IComparable<NetEntityId>, IEquatable<NetEntityId>
{
    /// <summary>无效实体标识；有效值从 1 开始。</summary>
    public static NetEntityId Invalid => default;

    /// <summary>底层稳定整数值。</summary>
    public int Value { get; }

    /// <summary>是否可用于复制注册与引用。</summary>
    public bool IsValid => Value > 0;

    /// <summary>由正整数创建实体标识。</summary>
    public NetEntityId(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "NetEntityId 必须大于 0。");
        Value = value;
    }

    /// <inheritdoc />
    public int CompareTo(NetEntityId other) => Value.CompareTo(other.Value);

    /// <inheritdoc />
    public bool Equals(NetEntityId other) => Value == other.Value;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is NetEntityId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value;

    /// <inheritdoc />
    public override string ToString() => IsValid ? Value.ToString() : "Invalid";

    /// <summary>判断两个实体标识是否相同。</summary>
    public static bool operator ==(NetEntityId left, NetEntityId right) => left.Equals(right);

    /// <summary>判断两个实体标识是否不同。</summary>
    public static bool operator !=(NetEntityId left, NetEntityId right) => !left.Equals(right);
}
