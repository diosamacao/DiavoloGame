using System;

/// <summary>网络协议使用的非负固定逻辑帧编号；与渲染帧无关。</summary>
public readonly struct NetTick : IComparable<NetTick>, IEquatable<NetTick>
{
    /// <summary>尚无有效网络帧。</summary>
    public static NetTick Invalid => default;

    readonly ulong _encodedValue;

    /// <summary>底层 64 位帧号；Invalid 读取为 -1。</summary>
    public long Value => IsValid ? checked((long)(_encodedValue - 1ul)) : -1;

    /// <summary>是否代表已开始的网络逻辑帧。</summary>
    public bool IsValid => _encodedValue != 0ul;

    /// <summary>由非负长整数创建网络帧。</summary>
    public NetTick(long value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "NetTick 不能为负。");
        _encodedValue = checked((ulong)value + 1ul);
    }

    /// <summary>返回下一逻辑帧；溢出时抛出异常。</summary>
    public NetTick Next()
    {
        if (!IsValid)
            throw new InvalidOperationException("Invalid NetTick 没有下一帧。");
        return new NetTick(checked(Value + 1));
    }

    /// <inheritdoc />
    public int CompareTo(NetTick other) => Value.CompareTo(other.Value);

    /// <inheritdoc />
    public bool Equals(NetTick other) => _encodedValue == other._encodedValue;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is NetTick other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => IsValid ? Value.ToString() : "Invalid";

    /// <summary>判断两个网络帧是否相同。</summary>
    public static bool operator ==(NetTick left, NetTick right) => left.Equals(right);

    /// <summary>判断两个网络帧是否不同。</summary>
    public static bool operator !=(NetTick left, NetTick right) => !left.Equals(right);
}
