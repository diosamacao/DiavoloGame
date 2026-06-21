using System;

/// <summary>战斗模式运行时接口：供装备、Buff、UI 等系统切换出招表。</summary>
public interface ICombatModeController
{
    CombatModeType CurrentMode { get; }

    /// <summary>当前模式绑定的出招表。</summary>
    PlayerActionSet ActiveActionSet { get; }

    CombatModeProfile Profile { get; }

    /// <summary>模式切换时触发：(previous, current)。</summary>
    event Action<CombatModeType, CombatModeType> ModeChanged;

    /// <summary>请求切换战斗模式；isActionPlaying 由调用方传入，本类不引用 ActionRuntime。</summary>
    CombatModeSwitchResult TrySetMode(
        CombatModeType mode,
        CombatModeSwitchPolicy policy = CombatModeSwitchPolicy.Immediate,
        bool isActionPlaying = false);

    /// <summary>应用 OnNextLocomotion 挂起的模式；由 CharacterActionDriver 在回到 Locomotion 后调用。</summary>
    void ApplyPendingModeIfReady();
}
