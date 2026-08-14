/// <summary>出招预测和解结果：是否因权威硬直/未起手取消了本地预测招。</summary>
public readonly struct PredictedActionReconcileResult
{
    /// <summary>创建一条和解结果。</summary>
    public PredictedActionReconcileResult(bool cancelled, int actionId, int actionFrame)
    {
        Cancelled = cancelled;
        ActionId = actionId;
        ActionFrame = actionFrame < 0 ? 0 : actionFrame;
    }

    /// <summary>预测招被权威否决或改为受击/死亡。</summary>
    public bool Cancelled { get; }

    /// <summary>和解后的动作 Id；0 表示空闲。</summary>
    public int ActionId { get; }

    /// <summary>和解后的动作帧。</summary>
    public int ActionFrame { get; }
}
