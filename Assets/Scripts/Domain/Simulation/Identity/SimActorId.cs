using System;

/// <summary>单次 SimulationWorld 会话内稳定、单调且不复用的 Actor 标识。</summary>
public readonly struct SimActorId : IComparable<SimActorId>, IEquatable<SimActorId>
{
    /// <summary>无效标识；有效 Actor 从 1 开始。</summary>
    public static SimActorId Invalid => default;

    /// <summary>底层稳定整数值。</summary>
    public int Value { get; }

    /// <summary>标识是否可用于 World 注册与排序。</summary>
    public bool IsValid => Value > 0;

    /// <summary>由正整数构造稳定标识。</summary>
    public SimActorId(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "SimActorId 必须大于 0。");

        Value = value;
    }

    /// <summary>按整数值提供跨容器一致的 Actor 顺序。</summary>
    public int CompareTo(SimActorId other) => Value.CompareTo(other.Value);

    /// <summary>比较两个 Actor 标识是否相同。</summary>
    public bool Equals(SimActorId other) => Value == other.Value;

    /// <summary>比较对象是否为相同 Actor 标识。</summary>
    public override bool Equals(object obj) => obj is SimActorId other && Equals(other);

    /// <summary>返回稳定整数哈希。</summary>
    public override int GetHashCode() => Value;

    /// <summary>返回便于日志定位的稳定文本。</summary>
    public override string ToString() => IsValid ? Value.ToString() : "Invalid";

    /// <summary>判断两个 Actor 标识是否相同。</summary>
    public static bool operator ==(SimActorId left, SimActorId right) => left.Equals(right);

    /// <summary>判断两个 Actor 标识是否不同。</summary>
    public static bool operator !=(SimActorId left, SimActorId right) => !left.Equals(right);
}
