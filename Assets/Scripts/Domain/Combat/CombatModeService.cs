using System;
using UnityEngine;

/// <summary>战斗模式服务：维护 mode、出招表与 Locomotion Profile；不引用 ActionExecutor。</summary>
public sealed class CombatModeService : ICombatModeService
{
    readonly CombatModeProfile profile;
    readonly CharacterAnimationService _animation;
    CombatModeType _currentMode;
    bool _hasPendingMode;
    CombatModeType _pendingMode;

    public CombatModeType CurrentMode => _currentMode;
    public CombatModeProfile Profile => profile;

    public event Action<CombatModeType, CombatModeType> ModeChanged;

    /// <summary>当前模式绑定的出招表；未配置 profile 或条目缺失时为 null。</summary>
    public PlayerActionSet ActiveActionSet
    {
        get
        {
            if (profile == null)
                return null;

            profile.TryGetActionSet(_currentMode, out PlayerActionSet actionSet);
            return actionSet;
        }
    }

    /// <summary>创建战斗模式运行时，并立即应用默认模式的 Locomotion Profile。</summary>
    public CombatModeService(CombatModeProfile combatProfile, CharacterAnimationService animation)
    {
        profile = combatProfile;
        _animation = animation;
        if (profile == null)
            throw new ArgumentNullException(nameof(combatProfile), "CombatModeService: profile 未绑定，无法解析出招表。");

        _currentMode = profile.DefaultMode;

        if (ActiveActionSet == null)
            throw new InvalidOperationException(
                $"CombatModeService: defaultMode={profile.DefaultMode} 未在 profile 中配置出招表。");

        ApplyLocomotionForMode(_currentMode);
    }

    /// <summary>请求切换模式；招式中 OnNextLocomotion 挂起，StopCurrentAction 由调用方 Stop 后重试。</summary>
    public CombatModeSwitchResult TrySetMode(
        CombatModeType mode,
        CombatModeSwitchPolicy policy = CombatModeSwitchPolicy.Immediate,
        bool isActionPlaying = false)
    {
        if (profile == null || !profile.TryGetActionSet(mode, out _))
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

    /// <summary>按 mode 切换 Locomotion Profile；未配置 profile 时保持当前动画映射不变。</summary>
    void ApplyLocomotionForMode(CombatModeType mode)
    {
        if (!profile.TryGetLocomotionProfile(mode, out CharacterAnimationProfile locomotionProfile))
            return;

        _animation.SetProfile(locomotionProfile);
        _animation.ResetPlaybackState();
    }
}
