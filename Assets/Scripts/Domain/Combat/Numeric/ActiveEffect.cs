using System.Collections.Generic;

/// <summary>已激活的 Duration/Periodic Effect 运行时实例。</summary>
public sealed class ActiveEffect
{
    readonly List<int> _modifierHandles = new(4);

    /// <summary>由 <see cref="EffectContainer"/> 创建。</summary>
    internal ActiveEffect(EffectDefinition definition)
    {
        Definition = definition;
        RemainingFrames = definition.DurationFrames;
        FramesUntilNextPeriod = definition.IntervalFrames;
        StackCount = 1;
    }

    /// <summary>静态定义。</summary>
    public EffectDefinition Definition { get; }

    /// <summary>剩余逻辑帧；0 表示本 Step 末应移除。</summary>
    public int RemainingFrames { get; private set; }

    /// <summary>距下次 Periodic 跳变的剩余帧。</summary>
    public int FramesUntilNextPeriod { get; private set; }

    /// <summary>当前叠层（至少 1）。</summary>
    public int StackCount { get; private set; }

    /// <summary>本实例挂在 AttributeSet 上的修饰句柄。</summary>
    public IReadOnlyList<int> ModifierHandles => _modifierHandles;

    /// <summary>刷新持续时间与周期计时（Refresh / 达上限再施加）。</summary>
    internal void RefreshTimers()
    {
        RemainingFrames = Definition.DurationFrames;
        FramesUntilNextPeriod = Definition.IntervalFrames;
    }

    /// <summary>叠层 +1。</summary>
    internal void AddStack() => StackCount++;

    /// <summary>推进一帧持续时间；归零返回 true。</summary>
    internal bool TickDuration()
    {
        if (RemainingFrames > 0)
            RemainingFrames--;
        return RemainingFrames <= 0;
    }

    /// <summary>推进周期计时；到点返回 true 并重置间隔。</summary>
    internal bool TickPeriod()
    {
        if (Definition.DurationPolicy != EffectDurationPolicy.Periodic)
            return false;

        if (FramesUntilNextPeriod > 0)
            FramesUntilNextPeriod--;

        if (FramesUntilNextPeriod > 0)
            return false;

        FramesUntilNextPeriod = Definition.IntervalFrames;
        return true;
    }

    /// <summary>记录 Modifier 句柄以便到期移除。</summary>
    internal void TrackModifierHandle(int handle) => _modifierHandles.Add(handle);
}
