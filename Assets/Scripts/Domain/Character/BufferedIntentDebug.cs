/// <summary>供 Debug HUD 读取的单条缓冲意图剩余帧。</summary>
public readonly struct BufferedIntentDebug
{
    /// <summary>创建一条缓冲调试项。</summary>
    public BufferedIntentDebug(GameplayIntentType intent, int remainingFrames)
    {
        Intent = intent;
        RemainingFrames = remainingFrames;
    }

    /// <summary>意图类型。</summary>
    public GameplayIntentType Intent { get; }

    /// <summary>剩余有效逻辑帧。</summary>
    public int RemainingFrames { get; }
}
