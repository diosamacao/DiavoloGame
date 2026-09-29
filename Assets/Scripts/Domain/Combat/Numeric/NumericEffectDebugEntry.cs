using System;

/// <summary>单条 ActiveEffect 的调试条目。</summary>
public readonly struct NumericEffectDebugEntry
{
    /// <summary>创建调试条目。</summary>
    public NumericEffectDebugEntry(
        string id,
        EffectDurationPolicy policy,
        int remainingFrames,
        int stackCount,
        int framesUntilNextPeriod)
    {
        Id = id ?? string.Empty;
        Policy = policy;
        RemainingFrames = remainingFrames;
        StackCount = stackCount;
        FramesUntilNextPeriod = framesUntilNextPeriod;
    }

    public string Id { get; }
    public EffectDurationPolicy Policy { get; }
    public int RemainingFrames { get; }
    public int StackCount { get; }
    public int FramesUntilNextPeriod { get; }
}
