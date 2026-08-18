/// <summary>一次时钟推进的追帧与 overrun 观测；不参与玩法决策。</summary>
public readonly struct SimulationTickMetrics
{
    /// <summary>创建本拍指标。</summary>
    public SimulationTickMetrics(
        int stepsTaken,
        bool catchUpClamped,
        double lastAdvanceDurationMs,
        bool overrun)
    {
        StepsTaken = stepsTaken;
        CatchUpClamped = catchUpClamped;
        LastAdvanceDurationMs = lastAdvanceDurationMs;
        Overrun = overrun;
    }

    /// <summary>本次实际执行的固定逻辑步数。</summary>
    public int StepsTaken { get; }

    /// <summary>欠账仍不少于一步，说明触及了追帧上限。</summary>
    public bool CatchUpClamped { get; }

    /// <summary>本次 Advance 墙钟毫秒。</summary>
    public double LastAdvanceDurationMs { get; }

    /// <summary>墙钟超过本拍逻辑预算（步数 × 固定步长）。</summary>
    public bool Overrun { get; }
}
