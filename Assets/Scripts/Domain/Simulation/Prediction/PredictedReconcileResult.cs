/// <summary>一次权威 pose 和解的只读结果。</summary>
public readonly struct PredictedReconcileResult
{
    /// <summary>创建和解结果。</summary>
    public PredictedReconcileResult(bool snapped, int planarErrorMm, int replayedInputs)
    {
        Snapped = snapped;
        PlanarErrorMm = planarErrorMm < 0 ? 0 : planarErrorMm;
        ReplayedInputs = replayedInputs < 0 ? 0 : replayedInputs;
    }

    /// <summary>超阈并已吸附权威、重放后续输入。</summary>
    public bool Snapped { get; }

    /// <summary>权威与该帧预测位姿的水平误差（毫米）。</summary>
    public int PlanarErrorMm { get; }

    /// <summary>吸附后重放的未确认输入条数；未吸附为 0。</summary>
    public int ReplayedInputs { get; }
}
