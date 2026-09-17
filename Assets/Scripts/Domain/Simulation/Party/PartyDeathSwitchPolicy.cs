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

/// <summary>跟踪当前 Active 槽的一次死亡窗口，并把帧计数交给纯策略裁定。</summary>
public sealed class PartyDeathSwitchGate
{
    int _trackedSlot = -1;
    int _elapsedFrames;

    /// <summary>当前死亡窗口已经观察到的逻辑帧数；未跟踪时为 0。</summary>
    public int ElapsedFrames => _elapsedFrames;

    /// <summary>
    /// 在每次 AfterLogicStep 调用一次；首次观察死亡计为第 1 帧，打开后立即重置。
    /// </summary>
    public bool Advance(
        int activeSlot,
        bool isDead,
        bool deathSequenceComplete,
        int deathActionTotalFrames)
    {
        if (!isDead)
        {
            Reset();
            return false;
        }

        if (_trackedSlot != activeSlot)
        {
            _trackedSlot = activeSlot;
            _elapsedFrames = 0;
        }

        _elapsedFrames++;
        if (!PartyDeathSwitchPolicy.ShouldCloseout(
                _elapsedFrames,
                deathSequenceComplete,
                deathActionTotalFrames))
        {
            return false;
        }

        Reset();
        return true;
    }

    /// <summary>清除已跟踪死亡窗口，供权威纠正或一次收尾提交后复用。</summary>
    public void Reset()
    {
        _trackedSlot = -1;
        _elapsedFrames = 0;
    }
}

/// <summary>协调器原子提交死亡后产生的换槽或队灭结果。</summary>
public readonly struct PartyDeathCloseout
{
    /// <summary>创建一次死亡收尾结果；toSlot 小于 0 表示全队已灭。</summary>
    public PartyDeathCloseout(int fromSlot, int toSlot)
    {
        FromSlot = fromSlot;
        ToSlot = toSlot;
    }

    /// <summary>死亡槽位。</summary>
    public int FromSlot { get; }

    /// <summary>自动上场槽位；队灭时为 -1。</summary>
    public int ToSlot { get; }

    /// <summary>本次收尾是否没有可用成员。</summary>
    public bool PartyWiped => ToSlot < 0;
}
