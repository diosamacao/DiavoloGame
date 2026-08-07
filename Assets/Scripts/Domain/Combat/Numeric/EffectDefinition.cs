using System;

/// <summary>
/// Effect 静态定义（纯 C#）；运行时由 <see cref="EffectContainer"/> 施加。
/// 首版不做 ScriptableObject 包装——G3/Editor 需要时再挂资产壳。
/// </summary>
public sealed class EffectDefinition
{
    static readonly EffectModifierSpec[] EmptyModifiers = Array.Empty<EffectModifierSpec>();
    static readonly EffectAttributeDelta[] EmptyDeltas = Array.Empty<EffectAttributeDelta>();

    EffectDefinition(
        string id,
        EffectDurationPolicy durationPolicy,
        int durationFrames,
        int intervalFrames,
        EffectStackPolicy stackPolicy,
        int maxStacks,
        EffectModifierSpec[] modifiers,
        EffectAttributeDelta[] instantDeltas,
        EffectAttributeDelta[] periodicDeltas)
    {
        Id = string.IsNullOrEmpty(id) ? "unnamed" : id;
        DurationPolicy = durationPolicy;
        DurationFrames = Math.Max(0, durationFrames);
        IntervalFrames = Math.Max(1, intervalFrames);
        StackPolicy = stackPolicy;
        MaxStacks = Math.Max(1, maxStacks);
        Modifiers = modifiers ?? EmptyModifiers;
        InstantDeltas = instantDeltas ?? EmptyDeltas;
        PeriodicDeltas = periodicDeltas ?? EmptyDeltas;
    }

    /// <summary>叠层/替换键。</summary>
    public string Id { get; }

    /// <summary>Instant / Duration / Periodic。</summary>
    public EffectDurationPolicy DurationPolicy { get; }

    /// <summary>Duration/Periodic 总逻辑帧；Instant 忽略。</summary>
    public int DurationFrames { get; }

    /// <summary>Periodic 跳伤间隔（逻辑帧）。</summary>
    public int IntervalFrames { get; }

    /// <summary>再施加时的叠层策略。</summary>
    public EffectStackPolicy StackPolicy { get; }

    /// <summary>StackCount 策略下的层数上限。</summary>
    public int MaxStacks { get; }

    /// <summary>Duration 期间挂到 AttributeSet 的修饰。</summary>
    public EffectModifierSpec[] Modifiers { get; }

    /// <summary>Instant 立即写入 Base 的增量。</summary>
    public EffectAttributeDelta[] InstantDeltas { get; }

    /// <summary>Periodic 每次跳变写入 Base 的增量（× StackCount）。</summary>
    public EffectAttributeDelta[] PeriodicDeltas { get; }

    /// <summary>构造立即效果（Cost/Grant/单次伤害）。</summary>
    public static EffectDefinition CreateInstant(
        string id,
        params EffectAttributeDelta[] deltas) =>
        new(
            id,
            EffectDurationPolicy.Instant,
            durationFrames: 0,
            intervalFrames: 1,
            EffectStackPolicy.Replace,
            maxStacks: 1,
            EmptyModifiers,
            deltas ?? EmptyDeltas,
            EmptyDeltas);

    /// <summary>构造持续修饰效果（加攻/减伤等）。</summary>
    public static EffectDefinition CreateDuration(
        string id,
        int durationFrames,
        EffectStackPolicy stackPolicy,
        int maxStacks,
        params EffectModifierSpec[] modifiers) =>
        new(
            id,
            EffectDurationPolicy.Duration,
            durationFrames,
            intervalFrames: 1,
            stackPolicy,
            maxStacks,
            modifiers ?? EmptyModifiers,
            EmptyDeltas,
            EmptyDeltas);

    /// <summary>构造周期跳变效果（DOT/HOT）；总跳数 = durationFrames / intervalFrames。</summary>
    public static EffectDefinition CreatePeriodic(
        string id,
        int durationFrames,
        int intervalFrames,
        EffectStackPolicy stackPolicy,
        int maxStacks,
        params EffectAttributeDelta[] periodicDeltas) =>
        new(
            id,
            EffectDurationPolicy.Periodic,
            durationFrames,
            intervalFrames,
            stackPolicy,
            maxStacks,
            EmptyModifiers,
            EmptyDeltas,
            periodicDeltas ?? EmptyDeltas);
}
