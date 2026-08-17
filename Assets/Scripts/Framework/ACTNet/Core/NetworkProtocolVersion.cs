using System;

/// <summary>显式的网络线协议版本；不表达 Content 或客户端产品版本。</summary>
public readonly struct NetworkProtocolVersion :
    IComparable<NetworkProtocolVersion>,
    IEquatable<NetworkProtocolVersion>
{
    /// <summary>无效协议版本。</summary>
    public static NetworkProtocolVersion Invalid => default;

    /// <summary>线上编码使用的正整数版本值。</summary>
    public int Value { get; }

    /// <summary>是否可参与握手比较。</summary>
    public bool IsValid => Value > 0;

    /// <summary>由正整数创建协议版本。</summary>
    public NetworkProtocolVersion(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "NetworkProtocolVersion 必须大于 0。");
        Value = value;
    }

    /// <inheritdoc />
    public int CompareTo(NetworkProtocolVersion other) => Value.CompareTo(other.Value);

    /// <inheritdoc />
    public bool Equals(NetworkProtocolVersion other) => Value == other.Value;

    /// <inheritdoc />
    public override bool Equals(object obj) =>
        obj is NetworkProtocolVersion other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value;

    /// <inheritdoc />
    public override string ToString() => IsValid ? Value.ToString() : "Invalid";

    /// <summary>判断两个协议版本是否相同。</summary>
    public static bool operator ==(
        NetworkProtocolVersion left,
        NetworkProtocolVersion right) =>
        left.Equals(right);

    /// <summary>判断两个协议版本是否不同。</summary>
    public static bool operator !=(
        NetworkProtocolVersion left,
        NetworkProtocolVersion right) =>
        !left.Equals(right);
}
