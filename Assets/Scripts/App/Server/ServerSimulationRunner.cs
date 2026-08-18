using System;
using System.Diagnostics;

/// <summary>Dedicated 单调时钟：用墙钟 dt 驱动固定步，并记录 overrun。</summary>
public sealed class ServerSimulationRunner
{
    readonly SimulationStepKernel _kernel;
    readonly Action _stepOnce;
    long _lastNowMs;
    bool _hasClock;

    /// <summary>用已有追帧核与单步回调创建 Runner；禁止 PredictedLocomotionDriver。</summary>
    public ServerSimulationRunner(SimulationStepKernel kernel, Action stepOnce)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _stepOnce = stepOnce ?? throw new ArgumentNullException(nameof(stepOnce));
    }

    /// <summary>最近一次 Advance 的指标。</summary>
    public SimulationTickMetrics Metrics { get; private set; }

    /// <summary>按单调 nowMs 追帧；首拍只对齐时钟不步进。</summary>
    public void Advance(long nowMs)
    {
        if (!_hasClock)
        {
            _lastNowMs = nowMs;
            _hasClock = true;
            Metrics = default;
            return;
        }

        double deltaSeconds = Math.Max(0d, (nowMs - _lastNowMs) / 1000.0);
        _lastNowMs = nowMs;
        int steps = _kernel.ConsumeSteps(deltaSeconds, out bool clamped);
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < steps; i++)
            _stepOnce();
        watch.Stop();

        double durationMs = watch.Elapsed.TotalMilliseconds;
        double budgetMs = steps * _kernel.FixedDeltaSeconds * 1000.0;
        bool overrun = steps > 0 && durationMs > budgetMs + 0.5d;
        Metrics = new SimulationTickMetrics(steps, clamped, durationMs, overrun);
    }
}
