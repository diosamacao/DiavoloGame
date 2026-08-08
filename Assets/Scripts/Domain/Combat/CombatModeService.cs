using System;
using UnityEngine;

/// <summary>战斗模式服务：维护 mode、ActiveGraph 与 Locomotion Profile。</summary>
public sealed class CombatModeService : ICombatModeService
{
    readonly CombatModeProfile profile;
    readonly CharacterAnimationService _animation;
    CombatModeType _currentMode;
    bool _hasPendingMode;
    CombatModeType _pendingMode;

    /// <summary>当前模式。</summary>
    public CombatModeType CurrentMode => _currentMode;

    /// <summary>模式配置。</summary>
    public CombatModeProfile Profile => profile;

    /// <summary>模式切换事件。</summary>
    public event Action<CombatModeType, CombatModeType> ModeChanged;

    /// <summary>当前模式绑定的 ActionGraph；未配置时为 null。</summary>
    public ActionGraph ActiveGraph
    {
        get
        {
            if (profile == null)
                return null;

            profile.TryGetActionGraph(_currentMode, out ActionGraph graph);
            return graph;
        }
    }

    /// <summary>创建战斗模式运行时，并立即应用默认模式的 Locomotion Profile。</summary>
    public CombatModeService(CombatModeProfile combatProfile, CharacterAnimationService animation)
    {
        profile = combatProfile;
        _animation = animation;
        if (profile == null)
            throw new ArgumentNullException(nameof(combatProfile), "CombatModeService: profile 未绑定，无法解析出招图。");

        _currentMode = profile.DefaultMode;

        if (ActiveGraph == null)
            throw new InvalidOperationException(
                $"CombatModeService: defaultMode={profile.DefaultMode} 未在 profile 中配置 ActionGraph。" +
                "若刚从 ActionSet 迁移，请打开 Unity 让 Migrator 跑完或执行 ACTGame/Combat/Migrate ActionSet To Mode Graph。");

        ApplyLocomotionForMode(_currentMode);
    }

    /// <summary>请求切换模式；招式中 OnNextLocomotion 挂起，StopCurrentAction 由调用方 Stop 后重试。</summary>
    public CombatModeSwitchResult TrySetMode(
        CombatModeType mode,
        CombatModeSwitchPolicy policy = CombatModeSwitchPolicy.Immediate,
        bool isActionPlaying = false)
    {
        if (profile == null || !profile.TryGetActionGraph(mode, out _))
            return CombatModeSwitchResult.Failed;

        if (mode == _currentMode)
            return CombatModeSwitchResult.Applied;

        if (policy == CombatModeSwitchPolicy.OnNextLocomotion && isActionPlaying)
        {
            _pendingMode = mode;
            _hasPendingMode = true;
            return CombatModeSwitchResult.PendingUntilLocomotion;
        }

        if (policy == CombatModeSwitchPolicy.StopCurrentAction && isActionPlaying)
            return CombatModeSwitchResult.RequiresStopCurrentAction;

        ApplyMode(mode);
        return CombatModeSwitchResult.Applied;
    }

    /// <summary>应用挂起的 OnNextLocomotion 切换；调用方应保证已回到 Locomotion。</summary>
    public void ApplyPendingModeIfReady()
    {
        if (!_hasPendingMode)
            return;

        ApplyMode(_pendingMode);
        _hasPendingMode = false;
    }

    void ApplyMode(CombatModeType mode)
    {
        if (mode == _currentMode)
            return;

        CombatModeType previous = _currentMode;
        _currentMode = mode;
        ApplyLocomotionForMode(mode);
        ModeChanged?.Invoke(previous, mode);
    }

    /// <summary>按 mode 切换 Clip 映射（取自该模式 LocomotionProfile）；相位参数仍由初始 Loco 驱动。</summary>
    void ApplyLocomotionForMode(CombatModeType mode)
    {
        if (!profile.TryGetLocomotionProfile(mode, out CharacterLocomotionProfile locomotion)
            || locomotion.AnimationProfile == null)
        {
            Debug.LogError($"CombatModeService: mode={mode} 缺少 LocomotionProfile.AnimationProfile。", profile);
            return;
        }

        _animation.SetProfile(locomotion.AnimationProfile);
        _animation.ResetPlaybackState();
    }
}
