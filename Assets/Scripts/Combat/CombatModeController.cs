using System;
using UnityEngine;

/// <summary>战斗模式运行时：维护 mode、出招表与 Locomotion Profile；不引用 ActionRuntimeController。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterAnimationController))]
public class CombatModeController : MonoBehaviour, ICombatModeController
{
    [SerializeField] CombatModeProfile profile = null!;

    CharacterAnimationController _animation = null!;
    CombatModeType _currentMode;
    bool _hasPendingMode;
    CombatModeType _pendingMode;
    bool _initialized;

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

    void Awake()
    {
        _animation = GetComponent<CharacterAnimationController>();

        if (profile != null)
            InitializeProfile(logErrors: true);
    }

    void Start()
    {
        if (!_initialized)
            InitializeProfile(logErrors: true);
    }

    /// <summary>绑定战斗模式配置，供 CharacterConfig 运行时装配。</summary>
    public void BindProfile(CombatModeProfile combatProfile)
    {
        profile = combatProfile;
        InitializeProfile(logErrors: true);
    }

    /// <summary>初始化当前模式与 Locomotion Profile；失败后禁用运行时逻辑。</summary>
    bool InitializeProfile(bool logErrors)
    {
        if (profile == null)
        {
            if (logErrors)
                Debug.LogError("CombatModeController: profile 未绑定，无法解析出招表。", this);

            enabled = false;
            return false;
        }

        _currentMode = profile.DefaultMode;

        if (ActiveActionSet == null)
        {
            if (logErrors)
            {
                Debug.LogError(
                    $"CombatModeController: defaultMode={profile.DefaultMode} 未在 profile 中配置出招表。",
                    this);
            }

            enabled = false;
            return false;
        }

        _initialized = true;
        enabled = true;
        ApplyLocomotionForMode(_currentMode);
        return true;
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
        if (_animation == null || profile == null)
            return;

        if (!profile.TryGetLocomotionProfile(mode, out CharacterAnimationProfile locomotionProfile))
            return;

        _animation.SetProfile(locomotionProfile);
        _animation.ResetPlaybackState();
    }
}
