/// <summary>ACT 走跑重放种类；只由 ActCharacterPredictionModel 解释，不进 Coordinator。</summary>
public static class ActPredictionReplayKind
{
    /// <summary>旧 Predict 路径：ApplyInput。</summary>
    public const byte Wish = 0;

    /// <summary>房间路径：IPredictedLocomotionReplay.ReplayTick。</summary>
    public const byte Runner = 1;
}
