/// <summary>一次权威对照后的纠偏策略；ReplayKind 仅由业务模型解释。</summary>
public readonly struct PredictionCorrectionPolicy
{
    /// <summary>只 Ack、不 Restore。</summary>
    public static PredictionCorrectionPolicy AcknowledgeOnly { get; } =
        new PredictionCorrectionPolicy(correctionRequired: false, allowReplay: false, replayKind: 0);

    /// <summary>创建纠偏策略。</summary>
    public PredictionCorrectionPolicy(bool correctionRequired, bool allowReplay, byte replayKind)
    {
        CorrectionRequired = correctionRequired;
        AllowReplay = allowReplay && correctionRequired;
        ReplayKind = replayKind;
    }

    /// <summary>误差超阈，需要 Restore。</summary>
    public bool CorrectionRequired { get; }

    /// <summary>Restore 后是否重放未确认命令。</summary>
    public bool AllowReplay { get; }

    /// <summary>业务层重放种类；Coordinator 不得解读。</summary>
    public byte ReplayKind { get; }
}
