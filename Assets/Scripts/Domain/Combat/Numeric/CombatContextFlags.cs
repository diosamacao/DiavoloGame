using System;

/// <summary>
/// 短时战斗上下文旗标（非 Attribute）：接战门闩、完美反击缓冲、闪避充能倒计时。
/// </summary>
public sealed class CombatContextFlags
{
    /// <summary>接战被动回能门闩剩余逻辑帧。</summary>
    public int InCombatHoldFrames { get; private set; }

    /// <summary>完美闪避反击缓冲剩余逻辑帧。</summary>
    public int PerfectDodgeCounterFrames { get; private set; }

    /// <summary>下一次闪避充能剩余逻辑帧；0 表示未在充能。</summary>
    public int DodgeRechargeFramesLeft { get; private set; }

    /// <summary>是否处于接战门闩内。</summary>
    public bool IsInCombat => InCombatHoldFrames > 0;

    /// <summary>是否可派生 PerfectDodgeAttack。</summary>
    public bool HasPerfectDodgeCounter => PerfectDodgeCounterFrames > 0;

    /// <summary>刷新接战门闩（通常取 Config.CombatHoldFrames）。</summary>
    public void SetInCombatHold(int frames) =>
        InCombatHoldFrames = Math.Max(0, frames);

    /// <summary>武装完美反击缓冲。</summary>
    public void ArmPerfectDodgeCounter(int frames) =>
        PerfectDodgeCounterFrames = Math.Max(0, frames);

    /// <summary>Counter 起手或超时清空。</summary>
    public void ClearPerfectDodgeCounter() => PerfectDodgeCounterFrames = 0;

    /// <summary>开始或重置闪避充能倒计时。</summary>
    public void SetDodgeRechargeFramesLeft(int frames) =>
        DodgeRechargeFramesLeft = Math.Max(0, frames);

    /// <summary>每逻辑帧递减门闩与反击缓冲（不含闪避充能，由 NumericSystem 在充能逻辑里处理）。</summary>
    public void StepHoldAndCounter()
    {
        if (InCombatHoldFrames > 0)
            InCombatHoldFrames--;
        if (PerfectDodgeCounterFrames > 0)
            PerfectDodgeCounterFrames--;
    }

    /// <summary>闪避充能倒计时减 1；归零时返回 true 表示应 +1 次。</summary>
    public bool TickDodgeRecharge()
    {
        if (DodgeRechargeFramesLeft <= 0)
            return false;

        DodgeRechargeFramesLeft--;
        return DodgeRechargeFramesLeft <= 0;
    }
}
