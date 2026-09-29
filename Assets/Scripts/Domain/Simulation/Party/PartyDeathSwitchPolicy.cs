using System;

/// <summary>以纯整数帧裁定死亡序列何时允许提交自动换人或队灭。</summary>
public static class PartyDeathSwitchPolicy
{
    /// <summary>死亡发生后至少保留的逻辑帧数，避免同帧死亡与换人。</summary>
    public const int MinimumCloseoutFrames = 15;

    /// <summary>计算死亡序列的确定性最晚提交帧：死亡 Action 总帧数再加最短窗口。</summary>
    public static int ResolveDeterministicCap(int deathActionTotalFrames) =>
        Math.Max(0, deathActionTotalFrames) + MinimumCloseoutFrames;

    /// <summary>达到最短窗口后以完成信号提交；信号缺失时到确定性上限强制提交。</summary>
    public static bool ShouldCloseout(
        int elapsedFrames,
        bool deathSequenceComplete,
        int deathActionTotalFrames)
    {
        if (elapsedFrames < MinimumCloseoutFrames)
            return false;
        return deathSequenceComplete
            || elapsedFrames >= ResolveDeterministicCap(deathActionTotalFrames);
    }
}
