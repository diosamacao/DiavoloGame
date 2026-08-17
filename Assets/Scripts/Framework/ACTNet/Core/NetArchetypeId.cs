using System;

/// <summary>Content Manifest 中稳定的复制实体原型标识。</summary>
public readonly struct NetArchetypeId : IComparable<NetArchetypeId>, IEquatable<NetArchetypeId>
{
    /// <summary>无效原型标识；有效值从 1 开始。</summary>
    public static NetArchetypeId Invalid => default;

    /// <summary>底层稳定整数值。</summary>
    public int Value { get; }

    /// <summary>是否可用于 Spawn 与客户端 Factory 查找。</summary>
    public bool IsValid => Value > 0;

    /// <summary>由正整数创建原型标识。</summary>
    public NetArchetypeId(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "NetArchetypeId 必须大于 0。");
        Value = value;
    }

    /// <inheritdoc />
    public int CompareTo(NetArchetypeId other) => Value.CompareTo(other.Value);

    /// <inheritdoc />
    public bool Equals(NetArchetypeId other) => Value == other.Value;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is NetArchetypeId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value;

    /// <inheritdoc />
    public override string ToString() => IsValid ? Value.ToString() : "Invalid";

    /// <summary>判断两个原型标识是否相同。</summary>
    public static bool operator ==(NetArchetypeId left, NetArchetypeId right) => left.Equals(right);

    /// <summary>判断两个原型标识是否不同。</summary>
    public static bool operator !=(NetArchetypeId left, NetArchetypeId right) => !left.Equals(right);
}
