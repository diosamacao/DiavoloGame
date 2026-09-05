/// <summary>弹刀卡肉帧数：盒勾了 UseHitStop 用盒值，否则默认 8。真伤不走此缺省。</summary>
public static class AssistParryHitStop
{
    /// <summary>敌人盒未勾卡肉时的弹刀默认逻辑帧（60Hz）。</summary>
    public const int DefaultFrames = 22;

    /// <summary>解析弹刀双方停顿帧数。空 Feedback 或未勾 UseHitStop 返回 <see cref="DefaultFrames"/>。</summary>
    public static int ResolveFrames(HitFeedbackSettings feedback)
    {
        if (feedback != null && feedback.UseHitStop)
            return feedback.HitStopFrames;

        return DefaultFrames;
    }
}
