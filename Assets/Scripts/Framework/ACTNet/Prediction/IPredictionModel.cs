/// <summary>业务预测模型：Coordinator 只调用这些方法，不解读 ActionId / Hit / Death。</summary>
public interface IPredictionModel<TCommand, TState>
{
    /// <summary>采集当前预测状态，供历史对照。</summary>
    TState Capture();

    /// <summary>把权威状态写回预测副本；随后由 Coordinator 决定是否 Replay。</summary>
    void Restore(in TState authorityState);

    /// <summary>按业务策略模拟一条未确认命令；跳过时返回 false，不计入 replayed。</summary>
    bool TrySimulate(in TCommand command, in PredictionCorrectionPolicy policy);

    /// <summary>测量权威与该 Ack 帧预测状态的误差；单位由业务命名。</summary>
    int MeasureError(in TState authority, in TState predicted);
}
