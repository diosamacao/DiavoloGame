using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterAnimationController))]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterRootMotionDriver))]
/// <summary>通用招式播放器：CancelWindow 消费输入衔接，ActionTransition 处理收招。</summary>
public class ActionRuntimeController : MonoBehaviour, IActionRuntime
{
    [SerializeField] CharacterAnimationController animationController = null!;
    [SerializeField] PlayerActionSet actionSet = null!;

    CharacterController _motor = null!;
    CharacterRootMotionDriver _rootMotion = null!;
    IActionComboInput _comboInput;
    IActionStartContext _startContext;
    ActionDefinition _current;
    bool _isPlaying;
    float _elapsed;

    public bool IsPlaying => _isPlaying;
    public bool CanCancelByMovement =>
        _isPlaying && _current != null && _current.IsInMovementCancelWindow(_elapsed);
    public ActionDefinition CurrentAction => _current;

    /// <summary>出招表入口，供 PlayerController 注册输入。</summary>
    public IReadOnlyList<ActionEntry> InputEntries =>
        actionSet != null ? actionSet.Entries : Array.Empty<ActionEntry>();

    void Awake()
    {
        if (animationController == null)
            animationController = GetComponent<CharacterAnimationController>();

        _motor = GetComponent<CharacterController>();
        _rootMotion = GetComponent<CharacterRootMotionDriver>();
    }

    public void BindComboInput(IActionComboInput comboInput) => _comboInput = comboInput;

    public void BindActionStartContext(IActionStartContext startContext) => _startContext = startContext;

    public bool TryStartByInput(string inputId)
    {
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
        if (!_isPlaying || _current == null)
            return;

        _elapsed += deltaTime;
        ApplyScriptedDisplacement(deltaTime);

        if (TryResolveCancelWindows())
            return;

        if (_elapsed >= _current.DurationSeconds)
            ResolveEndTransitions();
    }

    public void Stop()
    {
        _isPlaying = false;
        _current = null;
        _elapsed = 0f;
        _rootMotion?.SetActive(false);
    }

    /// <summary>按 priority 扫描 CancelWindow，首个匹配的 Action 取消生效。</summary>
    bool TryResolveCancelWindows()
    {
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

            if (window.TargetAction == null || window.TargetAction.AnimationClip == null)
                return false;

            ClearComboBuffersExcept(matchedInputId);
            TransitionTo(window.TargetAction);
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

    void ClearComboBuffersExcept(string keepInputId)
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

    /// <summary>AnimationEnd：按 Transition 衔接或 Stop。</summary>
    void ResolveEndTransitions()
    {
        foreach (ActionTransition transition in _current.GetTransitionsSorted())
        {
            if (transition.Condition != ActionTransitionCondition.AnimationEnd)
                continue;

            if (transition.TargetAction != null && transition.TargetAction.AnimationClip != null)
            {
                TransitionTo(transition.TargetAction);
                return;
            }

            Stop();
            return;
        }

        Stop();
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
            ExecuteStartBehavior(behavior);
    }

    void ExecuteStartBehavior(ActionStartBehaviorType behavior)
    {
        switch (behavior)
        {
            case ActionStartBehaviorType.FaceBufferedMoveIntent:
                _startContext.FaceBufferedMoveIntent();
                break;
        }
    }

    void BeginAction(ActionDefinition action)
    {
        _current = action;
        _isPlaying = true;
        _elapsed = 0f;
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
