using System;

/// <summary>固定 60Hz 追帧核：只负责时间欠账，不持有 World 或表现。</summary>
public sealed class SimulationStepKernel
{
    readonly FixedStepAccumulator _accumulator;
    readonly double _fixedDeltaSeconds;

    /// <summary>按模拟配置创建追帧核。</summary>
    public SimulationStepKernel(SimulationConfig config = null)
    {
        SimulationConfig resolved = config ?? new SimulationConfig();
        _fixedDeltaSeconds = resolved.FixedDeltaSeconds;
        _accumulator = new FixedStepAccumulator(
            resolved.FixedDeltaSeconds,
            resolved.MaxFrameCatchUp);
        FixedDeltaSeconds = resolved.FixedDeltaSeconds;
        MaxFrameCatchUp = resolved.MaxFrameCatchUp;
    }

    /// <summary>单步逻辑秒数。</summary>
    public double FixedDeltaSeconds { get; }

    /// <summary>单次 Advance 最多追的逻辑帧数。</summary>
    public int MaxFrameCatchUp { get; }

    /// <summary>渲染插值比例；Dedicated 不读此值。</summary>
    public float InterpolationAlpha => _accumulator.InterpolationAlpha;

    /// <summary>只读预览本次 Consume 会推进几步，不改欠账。</summary>
    public int PeekSteps(double deltaSeconds) => _accumulator.PeekSteps(deltaSeconds);

    /// <summary>累积时间并返回本次允许的固定步数；同时给出是否触及追帧上限。</summary>
    public int ConsumeSteps(double deltaSeconds, out bool catchUpClamped)
    {
        int consumed = _accumulator.ConsumeSteps(deltaSeconds);
        catchUpClamped = _accumulator.AccumulatedSeconds + 1e-9 >= _fixedDeltaSeconds;
        return consumed;
    }

    /// <summary>清空欠账。</summary>
    public void Reset() => _accumulator.Reset();
}
