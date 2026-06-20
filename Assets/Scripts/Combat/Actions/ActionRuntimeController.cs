using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterAnimationController))]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterRootMotionDriver))]
[RequireComponent(typeof(CombatModeController))]
/// <summary>通用招式播放器：CancelWindow 消费输入衔接，ActionTransition 处理收招；出招表由 CombatModeController 提供。</summary>
public class ActionRuntimeController : MonoBehaviour, IActionRuntime
{
    [SerializeField] CharacterAnimationController animationController = null!;
    [SerializeField] CombatModeController combatMode = null!;

    CharacterController _motor = null!;
    CharacterRootMotionDriver _rootMotion = null!;
    IActionComboInput _comboInput;
    IActionStartContext _startContext;
    ActionDefinition _current;
    bool _isPlaying;
    bool _hitStopPaused;
    bool _hitStopTriggeredThisAction;
    float _elapsed;

    public bool IsPlaying => _isPlaying;
    public bool IsHitStopPaused => _hitStopPaused;
    public bool CanCancelByMovement =>
        _isPlaying && _current != null && _current.IsInMovementCancelWindow(_elapsed);
    public bool CanRotateByInput =>
        _isPlaying && _current != null && _current.IsInRotationWindow(_elapsed);
    public ActionDefinition CurrentAction => _current;

    /// <summary>当前招式已播放秒数。</summary>
    public float ElapsedSeconds => _elapsed;

    /// <summary>当前招式逻辑帧（与 ActionDefinition.sampleRate 对齐）。</summary>
    public int CurrentFrame =>
        _isPlaying && _current != null ? _current.FrameAt(_elapsed) : 0;

    float IActionRuntime.ElapsedSeconds => _elapsed;

    int IActionRuntime.CurrentFrame =>
        _isPlaying && _current != null ? _current.FrameAt(_elapsed) : 0;

    /// <summary>当前战斗模式绑定的出招表 Entries。</summary>
    public IReadOnlyList<ActionEntry> InputEntries =>
        ActiveActionSet != null ? ActiveActionSet.Entries : Array.Empty<ActionEntry>();

    PlayerActionSet ActiveActionSet =>
        combatMode != null ? combatMode.ActiveActionSet : null;

    /// <summary>全部模式出招表的离散输入并集，供 InputReader 轮询。</summary>
    public InputActionReference[] GetEntryInputReferences()
    {
        if (combatMode != null && combatMode.Profile != null)
            return combatMode.Profile.CollectAllInputReferences();

        return Array.Empty<InputActionReference>();
    }

    void Awake()
    {
        if (animationController == null)
            animationController = GetComponent<CharacterAnimationController>();

        if (combatMode == null)
            combatMode = GetComponent<CombatModeController>();

        _motor = GetComponent<CharacterController>();
        _rootMotion = GetComponent<CharacterRootMotionDriver>();
    }

    public void BindComboInput(IActionComboInput comboInput) => _comboInput = comboInput;

    public void BindActionStartContext(IActionStartContext startContext) => _startContext = startContext;

    public bool TryStartByInput(string inputId)
    {
        PlayerActionSet actionSet = ActiveActionSet;
        if (_isPlaying || actionSet == null || !actionSet.TryGetStartAction(inputId, out ActionDefinition startAction))
            return false;

        return TryStart(startAction);
    }

    public bool TryStart(ActionDefinition action)
    {
        if (_isPlaying || action == null || action.AnimationClip == null || animationController == null)
            return false;

        ExecuteStartBehaviors(action);
        BeginAction(action);
        return true;
    }

    public void Tick(float deltaTime)
    {
        if (!_isPlaying || _current == null || _hitStopPaused)
            return;

        _elapsed += deltaTime;
        ApplyScriptedDisplacement(deltaTime);

        if (TryResolveCancelWindows())
            return;

        if (TryResolveTransitions())
            return;

        // 无匹配 Transition 时自然收招
        if (_elapsed >= _current.DurationSeconds)
            Stop();
    }

    public void Stop()
    {
        _isPlaying = false;
        _current = null;
        _elapsed = 0f;
        _hitStopPaused = false;
        _hitStopTriggeredThisAction = false;
        _rootMotion?.SetActive(false);
    }

    /// <summary>卡肉期间暂停招式逻辑时间推进（由 HitStopController 驱动）。</summary>
    public void SetHitStopPaused(bool paused) => _hitStopPaused = paused;

    /// <summary>每招仅触发一次卡肉；返回 false 表示本招已触发过。</summary>
    public bool TryConsumeHitStopTrigger()
    {
        if (_hitStopTriggeredThisAction)
            return false;

        _hitStopTriggeredThisAction = true;
        return true;
    }

    /// <summary>按 priority 扫描 CancelWindow，首个匹配的 Action 取消生效。</summary>
    bool TryResolveCancelWindows()
    {
        PlayerActionSet actionSet = ActiveActionSet;
        if (_comboInput == null || _current == null || actionSet == null)
            return false;

        foreach (ResolvedCancelWindow window in _current.GetCancelWindowsSorted())
        {
            if (!_current.IsInCancelWindow(window, _elapsed))
                continue;

            if (window.CancelType != CancelType.Action)
                continue;

            if (!TryConsumeMatchingInput(window, out string matchedInputId))
                continue;

            if (!actionSet.TryResolveNext(matchedInputId, _current, out ActionDefinition nextAction))
                continue;

            if (nextAction == null || nextAction.AnimationClip == null)
                continue;

            ClearComboBuffersExcept(matchedInputId, actionSet);
            TransitionTo(nextAction);
            return true;
        }

        return false;
    }

    bool TryConsumeMatchingInput(ResolvedCancelWindow window, out string matchedInputId)
    {
        matchedInputId = null;

        foreach (string inputId in window.AllowedInputs)
        {
            if (_comboInput.HasBuffer(inputId))
            {
                _comboInput.TryConsumeBuffer(inputId);
                matchedInputId = inputId;
                return true;
            }
        }

        return false;
    }

    /// <summary>清除当前出招表中除 keepInputId 外的离散缓冲。</summary>
    void ClearComboBuffersExcept(string keepInputId, PlayerActionSet actionSet)
    {
        if (_comboInput == null || actionSet == null)
            return;

        foreach (ActionEntry entry in actionSet.Entries)
        {
            if (!entry.IsValid || entry.InputId == keepInputId)
                continue;

            _comboInput.TryConsumeBuffer(entry.InputId);
        }
    }

    /// <summary>按 priority 扫描 Transition，首个满足条件的自动衔接或 Stop。</summary>
    bool TryResolveTransitions()
    {
        if (_current == null)
            return false;

        foreach (ActionTransition transition in _current.GetTransitionsSorted())
        {
            if (!_current.IsTransitionEligible(transition, _elapsed))
                continue;

            if (transition.TargetAction != null && transition.TargetAction.AnimationClip != null)
            {
                TransitionTo(transition.TargetAction);
                return true;
            }

            Stop();
            return true;
        }

        return false;
    }

    void TransitionTo(ActionDefinition action)
    {
        ExecuteStartBehaviors(action);
        BeginAction(action);
    }

    void ExecuteStartBehaviors(ActionDefinition action)
    {
        if (action == null || _startContext == null)
            return;

        ActionStartBehaviorType[] behaviors = action.StartBehaviors;
        foreach (ActionStartBehaviorType behavior in behaviors)
            ExecuteStartBehavior(action, behavior);
    }

    void ExecuteStartBehavior(ActionDefinition action, ActionStartBehaviorType behavior)
    {
        switch (behavior)
        {
            case ActionStartBehaviorType.FaceBufferedMoveIntent:
                _startContext.FaceBufferedMoveIntent();
                break;
            case ActionStartBehaviorType.SwitchCombatMode:
                if (combatMode != null)
                {
                    combatMode.TrySetMode(action.SwitchCombatModeTarget, action.SwitchCombatModePolicy);
                }
                break;
        }
    }

    void BeginAction(ActionDefinition action)
    {
        _current = action;
        _isPlaying = true;
        _elapsed = 0f;
        _hitStopTriggeredThisAction = false;
        _rootMotion?.SetActive(action.UseRootMotion);
        animationController.PlayClip(action.AnimationClip, action.CrossFadeDuration);
    }

    void ApplyScriptedDisplacement(float deltaTime)
    {
        if (_motor == null || !_current.HasScriptedDisplacement || !_current.IsInDisplacementWindow(_elapsed))
            return;

        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return;

        forward.Normalize();
        float signedSpeed = _current.DisplacementSpeed;
        _motor.Move(forward * (signedSpeed * deltaTime));
    }
}
