using System;

/// <summary>Numeric 只读调试快照：Attribute 摘要 + ActiveEffects + Flags（G5 完成态字段）。</summary>
public readonly struct NumericDebugSnapshot
{
    /// <summary>构造一帧调试快照。</summary>
    public NumericDebugSnapshot(
        int healthMilli,
        int energyMilli,
        int attackMilli,
        int defenseMilli,
        int outgoingDamageMultMilli,
        int incomingDamageMultMilli,
        int inCombatHoldFrames,
        int perfectDodgeCounterFrames,
        int dodgeRechargeFramesLeft,
        NumericEffectDebugEntry[] effects)
    {
        HealthMilli = healthMilli;
        EnergyMilli = energyMilli;
        AttackMilli = attackMilli;
        DefenseMilli = defenseMilli;
        OutgoingDamageMultMilli = outgoingDamageMultMilli;
        IncomingDamageMultMilli = incomingDamageMultMilli;
        InCombatHoldFrames = inCombatHoldFrames;
        PerfectDodgeCounterFrames = perfectDodgeCounterFrames;
        DodgeRechargeFramesLeft = dodgeRechargeFramesLeft;
        Effects = effects ?? Array.Empty<NumericEffectDebugEntry>();
    }

    public int HealthMilli { get; }
    public int EnergyMilli { get; }
    public int AttackMilli { get; }
    public int DefenseMilli { get; }
    public int OutgoingDamageMultMilli { get; }
    public int IncomingDamageMultMilli { get; }
    public int InCombatHoldFrames { get; }
    public int PerfectDodgeCounterFrames { get; }
    public int DodgeRechargeFramesLeft { get; }
    public NumericEffectDebugEntry[] Effects { get; }
}

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
