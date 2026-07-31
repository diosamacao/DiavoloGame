using System;

/// <summary>把可变渲染帧时间转换为有上限但不丢欠账的固定逻辑步数。</summary>
public sealed class FixedStepAccumulator
{
    const double StepEpsilon = 1e-9;

    readonly double _fixedDeltaSeconds;
    readonly int _maxFrameCatchUp;
    double _accumulatedSeconds;

    /// <summary>当前尚未消费的时间欠账。</summary>
    public double AccumulatedSeconds => _accumulatedSeconds;

    /// <summary>上一逻辑 Pose 到当前逻辑 Pose 的渲染插值比例；追帧欠账过大时钳制为 1。</summary>
    public float InterpolationAlpha =>
        (float)Math.Max(0d, Math.Min(1d, _accumulatedSeconds / _fixedDeltaSeconds));

    /// <summary>使用固定步长和每次调用的追帧上限创建 accumulator。</summary>
    public FixedStepAccumulator(double fixedDeltaSeconds, int maxFrameCatchUp)
    {
        if (fixedDeltaSeconds <= 0d)
            throw new ArgumentOutOfRangeException(nameof(fixedDeltaSeconds));
        if (maxFrameCatchUp <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxFrameCatchUp));

        _fixedDeltaSeconds = fixedDeltaSeconds;
        _maxFrameCatchUp = maxFrameCatchUp;
    }

    /// <summary>累积渲染时间并返回本次允许推进的固定逻辑帧数。</summary>
    public int ConsumeSteps(double renderDeltaSeconds)
    {
        if (double.IsNaN(renderDeltaSeconds) || double.IsInfinity(renderDeltaSeconds))
            throw new ArgumentOutOfRangeException(nameof(renderDeltaSeconds));

        _accumulatedSeconds += Math.Max(0d, renderDeltaSeconds);
        int availableSteps = (int)Math.Floor(
            (_accumulatedSeconds + StepEpsilon) / _fixedDeltaSeconds);
        int consumedSteps = Math.Min(availableSteps, _maxFrameCatchUp);
        _accumulatedSeconds -= consumedSteps * _fixedDeltaSeconds;

        if (_accumulatedSeconds < 0d && _accumulatedSeconds > -StepEpsilon)
            _accumulatedSeconds = 0d;

        return consumedSteps;
    }

    /// <summary>清空所有未消费时间；用于场景切换或显式重置。</summary>
    public void Reset() => _accumulatedSeconds = 0d;
}
