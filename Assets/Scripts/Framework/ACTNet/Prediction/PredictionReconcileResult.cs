/// <summary>通用 Coordinator 一次权威对照的只读结果。</summary>
public readonly struct PredictionReconcileResult
{
    /// <summary>创建对照结果。</summary>
    public PredictionReconcileResult(bool snapped, int error, int replayedCommands)
    {
        Snapped = snapped;
        Error = error < 0 ? 0 : error;
        ReplayedCommands = replayedCommands < 0 ? 0 : replayedCommands;
    }

    /// <summary>已 Restore 权威状态。</summary>
    public bool Snapped { get; }

    /// <summary>权威与 Ack 帧预测的误差。</summary>
    public int Error { get; }

    /// <summary>Restore 后实际重放的命令条数。</summary>
    public int ReplayedCommands { get; }
}
