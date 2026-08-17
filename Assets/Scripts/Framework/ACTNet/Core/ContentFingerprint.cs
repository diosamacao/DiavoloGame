using System;

/// <summary>Gameplay Content Manifest 的稳定 128 位指纹，用于握手拒绝内容不一致。</summary>
public readonly struct ContentFingerprint : IEquatable<ContentFingerprint>
{
    /// <summary>未计算或无效的内容指纹。</summary>
    public static ContentFingerprint Invalid => default;

    /// <summary>指纹高 64 位。</summary>
    public ulong High { get; }

    /// <summary>指纹低 64 位。</summary>
    public ulong Low { get; }

    /// <summary>是否包含非零内容指纹。</summary>
    public bool IsValid => High != 0ul || Low != 0ul;

    /// <summary>由两个 64 位分量创建内容指纹；全零保留给 Invalid。</summary>
    public ContentFingerprint(ulong high, ulong low)
    {
        if (high == 0ul && low == 0ul)
            throw new ArgumentException("ContentFingerprint 不能全为 0。");
        High = high;
        Low = low;
    }

    /// <inheritdoc />
    public bool Equals(ContentFingerprint other) => High == other.High && Low == other.Low;

    /// <inheritdoc />
    public override bool Equals(object obj) =>
        obj is ContentFingerprint other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (High.GetHashCode() * 397) ^ Low.GetHashCode();
        }
    }

    /// <inheritdoc />
    public override string ToString() =>
        IsValid ? $"{High:X16}{Low:X16}" : "Invalid";

    /// <summary>判断两个内容指纹是否相同。</summary>
    public static bool operator ==(ContentFingerprint left, ContentFingerprint right) =>
        left.Equals(right);

    /// <summary>判断两个内容指纹是否不同。</summary>
    public static bool operator !=(ContentFingerprint left, ContentFingerprint right) =>
        !left.Equals(right);
}
