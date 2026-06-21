using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterAnimationController))]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterRootMotionDriver))]
/// <summary>通用招式播放器：UpdateFrame 统一 Logic Tick；经 ICombatModeController 单向访问战斗模式。</summary>
public class ActionRuntimeController : MonoBehaviour, IActionRuntime, IActionHitReceiver
{
    [SerializeField] CharacterAnimationController animationController = null!;

    CharacterController _motor = null!;
    CharacterRootMotionDriver _rootMotion = null!;
    /// <summary>同物体 CombatModeController；单向依赖，不通过接口隐藏 Profile / ActiveActionSet。</summary>
    CombatModeController _combatMode = null!;
    readonly List<ICombatFrameConsumer> _frameConsumers = new();
    IActionComboInput _comboInput;
    IActionStartContext _startContext;
    ActionDefinition _current;
    bool _isPlaying;
    bool _hitStopPaused;
    bool _hitStopTriggeredThisAction;
    bool _hasConfirmedHitThisAction;
    float _elapsed;
    int _lastProcessedFrame = -1;

    public bool IsPlaying => _isPlaying;
    public bool IsHitStopPaused => _hitStopPaused;
    public bool HasConfirmedHitThisAction => _hasConfirmedHitThisAction;
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

    /// <summary>Logic Tick 帧推进；编辑器 Scrub 与 Play Mode 共用。</summary>
    public event Action<CombatFrameContext> FrameAdvanced;

    /// <summary>当前战斗模式绑定的出招表 Entries。</summary>
    public IReadOnlyList<ActionEntry> InputEntries =>
        ActiveActionSet != null ? ActiveActionSet.Entries : Array.Empty<ActionEntry>();

    PlayerActionSet ActiveActionSet =>
        ResolveCombatMode()?.ActiveActionSet;

    /// <summary>懒解析同物体 CombatModeController，避免 Awake 顺序导致 GetEntryInputReferences 返回空。</summary>
    CombatModeController ResolveCombatMode()
    {
        if (_combatMode == null)
            _combatMode = GetComponent<CombatModeController>();
        return _combatMode;
    }

    /// <summary>全部模式出招表的离散输入并集，供 InputReader 轮询。</summary>
    public InputActionReference[] GetEntryInputReferences()
    {
        CombatModeController mode = ResolveCombatMode();
        if (mode != null && mode.Profile != null)
            return mode.Profile.CollectAllInputReferences();

        return Array.Empty<InputActionReference>();
    }

    void Awake()
    {
        if (animationController == null)
            animationController = GetComponent<CharacterAnimationController>();

        _combatMode = GetComponent<CombatModeController>();

        _motor = GetComponent<CharacterController>();
        _rootMotion = GetComponent<CharacterRootMotionDriver>();
        DiscoverFrameConsumers();
    }

    /// <summary>注册 Logic Tick 消费者（Hitbox、VFX 等）；Awake 时会自动发现同物体实现。</summary>
    public void RegisterFrameConsumer(ICombatFrameConsumer consumer)
    {
        if (consumer != null && !_frameConsumers.Contains(consumer))
            _frameConsumers.Add(consumer);
    }

    public void BindComboInput(IActionComboInput comboInput) => _comboInput = comboInput;

    public void BindActionStartContext(IActionStartContext startContext) => _startContext = startContext;

    /// <summary>注入战斗模式（Awake 默认 GetComponent；也可由 Bootstrap 显式绑定）。</summary>
    public void BindCombatMode(CombatModeController combatMode) => _combatMode = combatMode;

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
        SyncLogicFrameFromElapsed();

        if (TryResolveCancelWindows())
            return;

        if (TryResolveTransitions())
            return;

        if (_elapsed >= _current.DurationSeconds)
            Stop();
    }

    /// <summary>编辑器 Scrub 与 Play Mode 共用的 Logic Tick 入口；要求招式已在播放中。</summary>
    public void UpdateFrame(int frameIndex)
    {
        if (!_isPlaying || _current == null || _hitStopPaused)
            return;

        frameIndex = Mathf.Clamp(frameIndex, 0, Mathf.Max(0, _current.TotalFrames - 1));
        _elapsed = frameIndex / _current.SampleRate;
        AdvanceLogicFramesThrough(frameIndex);
    }

    public void Stop()
    {
        if (_isPlaying)
            NotifyActionEnded();

        _isPlaying = false;
        _current = null;
        _elapsed = 0f;
        _lastProcessedFrame = -1;
        _hitStopPaused = false;
        _hitStopTriggeredThisAction = false;
        _hasConfirmedHitThisAction = false;
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

    /// <summary>HitBoxSystem 命中回流；支撑 OnHitConfirm Transition。</summary>
    public void NotifyHit(in ActionHitContext context)
    {
        if (!_isPlaying || _current == null || context.Action != _current)
            return;

        _hasConfirmedHitThisAction = true;
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
            if (!_current.IsTransitionEligible(transition, _elapsed, _hasConfirmedHitThisAction))
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
        NotifyActionEnded();
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
                TrySwitchCombatMode(action.SwitchCombatModeTarget, action.SwitchCombatModePolicy);
                break;
        }
    }

    /// <summary>切换战斗模式；StopCurrentAction 时本类先 Stop 再以 isActionPlaying=false 重试。</summary>
    void TrySwitchCombatMode(CombatModeType mode, CombatModeSwitchPolicy policy)
    {
        if (_combatMode == null)
            return;

        CombatModeSwitchResult result = _combatMode.TrySetMode(mode, policy, _isPlaying);
        if (result != CombatModeSwitchResult.RequiresStopCurrentAction)
            return;

        Stop();
        _combatMode.TrySetMode(mode, policy, false);
    }

    void BeginAction(ActionDefinition action)
    {
        _current = action;
        _isPlaying = true;
        _elapsed = 0f;
        _lastProcessedFrame = -1;
        _hitStopTriggeredThisAction = false;
        _hasConfirmedHitThisAction = false;
        _rootMotion?.SetActive(action.UseRootMotion);
        animationController.PlayClip(action.AnimationClip, action.CrossFadeDuration);

        NotifyActionBegan(action);
        DispatchCombatFrame(0, -1);
        _lastProcessedFrame = 0;
    }

    void SyncLogicFrameFromElapsed()
    {
        int frame = _current.FrameAt(_elapsed);
        AdvanceLogicFramesThrough(frame);
    }

    /// <summary>从 _lastProcessedFrame+1 推进到 targetFrame，避免单帧大 delta 漏掉中间 Hitbox/VFX。</summary>
    void AdvanceLogicFramesThrough(int targetFrame)
    {
        if (targetFrame <= _lastProcessedFrame)
            return;

        for (int frame = _lastProcessedFrame + 1; frame <= targetFrame; frame++)
            DispatchCombatFrame(frame, frame - 1);

        _lastProcessedFrame = targetFrame;
    }

    void DispatchCombatFrame(int frameIndex, int previousFrameIndex)
    {
        var context = new CombatFrameContext(_current, frameIndex, previousFrameIndex, _elapsed, transform);
        FrameAdvanced?.Invoke(context);

        foreach (ICombatFrameConsumer consumer in _frameConsumers)
            consumer.OnCombatFrameAdvanced(in context);
    }

    void NotifyActionBegan(ActionDefinition action)
    {
        foreach (ICombatFrameConsumer consumer in _frameConsumers)
            consumer.OnActionBegan(action);
    }

    void NotifyActionEnded()
    {
        foreach (ICombatFrameConsumer consumer in _frameConsumers)
            consumer.OnActionEnded();
    }

    void DiscoverFrameConsumers()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is ICombatFrameConsumer consumer)
                RegisterFrameConsumer(consumer);
        }
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
