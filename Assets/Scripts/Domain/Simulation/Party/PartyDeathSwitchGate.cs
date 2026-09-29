using System;

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
