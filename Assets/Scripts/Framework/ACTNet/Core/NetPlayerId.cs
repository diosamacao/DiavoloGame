using System;

/// <summary>一次 Session 内稳定且与连接生命周期分离的玩家标识。</summary>
public readonly struct NetPlayerId : IComparable<NetPlayerId>, IEquatable<NetPlayerId>
{
    /// <summary>无效玩家标识；有效值从 1 开始。</summary>
    public static NetPlayerId Invalid => default;

    /// <summary>底层稳定整数值。</summary>
    public int Value { get; }

    /// <summary>是否可用于玩家注册与所有权映射。</summary>
    public bool IsValid => Value > 0;

    /// <summary>由正整数创建玩家标识。</summary>
    public NetPlayerId(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "NetPlayerId 必须大于 0。");
        Value = value;
    }

    /// <inheritdoc />
    public int CompareTo(NetPlayerId other) => Value.CompareTo(other.Value);

    /// <inheritdoc />
    public bool Equals(NetPlayerId other) => Value == other.Value;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is NetPlayerId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value;

    /// <inheritdoc />
    public override string ToString() => IsValid ? Value.ToString() : "Invalid";

    /// <summary>判断两个玩家标识是否相同。</summary>
    public static bool operator ==(NetPlayerId left, NetPlayerId right) => left.Equals(right);

    /// <summary>判断两个玩家标识是否不同。</summary>
    public static bool operator !=(NetPlayerId left, NetPlayerId right) => !left.Equals(right);
}
