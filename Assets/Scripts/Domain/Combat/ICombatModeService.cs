using System;

/// <summary>战斗模式运行时接口：供装备、Buff、UI 等系统切换出招图。</summary>
public interface ICombatModeService
{
    /// <summary>当前战斗模式。</summary>
    CombatModeType CurrentMode { get; }

    /// <summary>当前模式绑定的 ActionGraph。</summary>
    ActionGraph ActiveGraph { get; }

    /// <summary>模式配置资产。</summary>
    CombatModeProfile Profile { get; }

    /// <summary>模式切换时触发：(previous, current)。</summary>
    event Action<CombatModeType, CombatModeType> ModeChanged;

    /// <summary>请求切换战斗模式；调用方显式传入当前是否有活动动作。</summary>
    CombatModeSwitchResult TrySetMode(
        CombatModeType mode,
        CombatModeSwitchPolicy policy = CombatModeSwitchPolicy.Immediate,
        bool isActionPlaying = false);

    /// <summary>应用 OnNextLocomotion 挂起的模式；由 CharacterActionDriver 在回到 Locomotion 后调用。</summary>
    void ApplyPendingModeIfReady();
}
