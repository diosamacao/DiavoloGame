using System;
using UnityEngine;

/// <summary>战斗模式运行时接口：供装备、Buff、UI 等系统切换出招表。</summary>
public interface ICombatModeController
{
    CombatModeType CurrentMode { get; }

    /// <summary>模式切换时触发：(previous, current)。</summary>
    event Action<CombatModeType, CombatModeType> ModeChanged;

    /// <summary>请求切换战斗模式；若 profile 未配置目标模式则返回 false。</summary>
    bool TrySetMode(CombatModeType mode, CombatModeSwitchPolicy policy = CombatModeSwitchPolicy.Immediate);

    /// <summary>招式已结束且存在挂起模式时立刻应用（OnNextLocomotion）。</summary>
    void ApplyPendingModeIfReady();
}

/// <summary>战斗模式运行时：维护当前 mode、出招表与 Locomotion 动画 Profile。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ActionRuntimeController))]
[RequireComponent(typeof(CharacterAnimationController))]
public class CombatModeController : MonoBehaviour, ICombatModeController
{
    [SerializeField] CombatModeProfile profile = null!;

    ActionRuntimeController _actionRuntime = null!;
    CharacterAnimationController _animation = null!;
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

    void Awake()
    {
        _actionRuntime = GetComponent<ActionRuntimeController>();
        _animation = GetComponent<CharacterAnimationController>();

        if (profile == null)
        {
            Debug.LogError("CombatModeController: profile 未绑定，无法解析出招表。", this);
            enabled = false;
            return;
        }

        _currentMode = profile.DefaultMode;

        if (ActiveActionSet == null)
        {
            Debug.LogError(
                $"CombatModeController: defaultMode={profile.DefaultMode} 未在 profile 中配置出招表。",
                this);
            enabled = false;
            return;
        }

        ApplyLocomotionForMode(_currentMode);
    }

    void Update()
    {
        TryApplyPendingMode();
    }

    /// <summary>请求切换模式；OnNextLocomotion 在招式中会挂起至回到 Locomotion。</summary>
    public bool TrySetMode(CombatModeType mode, CombatModeSwitchPolicy policy = CombatModeSwitchPolicy.Immediate)
    {
        if (profile == null || !profile.TryGetActionSet(mode, out _))
            return false;

        if (mode == _currentMode)
            return true;

        if (policy == CombatModeSwitchPolicy.OnNextLocomotion && _actionRuntime != null && _actionRuntime.IsPlaying)
        {
            _pendingMode = mode;
            _hasPendingMode = true;
            return true;
        }

        if (policy == CombatModeSwitchPolicy.StopCurrentAction && _actionRuntime != null && _actionRuntime.IsPlaying)
            _actionRuntime.Stop();

        ApplyMode(mode);
        return true;
    }

    /// <summary>招式已结束且存在挂起模式时立刻应用（供 PlayerController 在消费预输入前调用）。</summary>
    public void ApplyPendingModeIfReady()
    {
        TryApplyPendingMode();
    }

    /// <summary>招式结束后应用挂起的 OnNextLocomotion 切换。</summary>
    void TryApplyPendingMode()
    {
        if (!_hasPendingMode || _actionRuntime == null || _actionRuntime.IsPlaying)
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
        // 清除 Key 缓存，否则同 Idle/Walk/Run 不会 CrossFade 到新 StateName
        _animation.ResetPlaybackState();
    }
}

