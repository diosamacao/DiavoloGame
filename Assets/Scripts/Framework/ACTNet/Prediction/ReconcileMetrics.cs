/// <summary>通用预测纠偏计数；HUD 只显示，不解释业务误差单位。</summary>
public sealed class ReconcileMetrics
{
    /// <summary>累计 Restore 次数。</summary>
    public int SnapCount { get; private set; }

    /// <summary>累计 Replay 的命令条数。</summary>
    public int ReplayCount { get; private set; }

    /// <summary>最近一次对照误差；单位由业务命名。</summary>
    public int LastError { get; private set; }

    /// <summary>当前尚未 Ack 的命令数。</summary>
    public int PendingCommands { get; private set; }

    /// <summary>只 Ack 时更新误差与 pending。</summary>
    public void RecordAcknowledge(int error, int pendingCommands)
    {
        LastError = error < 0 ? 0 : error;
        PendingCommands = pendingCommands < 0 ? 0 : pendingCommands;
    }

    /// <summary>发生 Restore 时累加 snap / replay。</summary>
    public void RecordCorrection(int error, int replayed, int pendingCommands)
    {
        SnapCount++;
        ReplayCount += replayed < 0 ? 0 : replayed;
        RecordAcknowledge(error, pendingCommands);
    }
}
