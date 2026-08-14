using System;

/// <summary>权威命中边沿；须带 frame 与 SimHitKey，避免重传双算。</summary>
public readonly struct ReplicatedHitEvent : IEquatable<ReplicatedHitEvent>
{
    /// <summary>创建一条可复制命中事件。</summary>
    public ReplicatedHitEvent(long frame, SimHitKey key)
    {
        Frame = frame;
        Key = key;
    }

    /// <summary>命中所属权威逻辑帧。</summary>
    public long Frame { get; }

    /// <summary>稳定命中键。</summary>
    public SimHitKey Key { get; }

    /// <summary>比较帧与命中键。</summary>
    public bool Equals(ReplicatedHitEvent other) =>
        Frame == other.Frame && Key.Equals(other.Key);

    /// <inheritdoc />
    public override bool Equals(object obj) =>
        obj is ReplicatedHitEvent other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (Frame.GetHashCode() * 397) ^ Key.GetHashCode();
        }
    }
}
