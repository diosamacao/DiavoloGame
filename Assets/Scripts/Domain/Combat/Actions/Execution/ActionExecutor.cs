using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>单角色动作执行器：只播放已解析好的招式，负责帧推进、CancelWindow、Transition、Hitbox/VFX 派发。</summary>
public sealed class ActionExecutor : IActionExecutor, IActionHitReceiver
{
    readonly Transform _actorRoot;
    readonly CharacterAnimationService animationController;
    readonly CharacterController _motor;
    readonly CharacterRootMotionDriver _rootMotion;
    /// <summary>同物体 CombatModeService；仅用于 SwitchCombatMode StartBehavior。</summary>
    readonly CombatModeService _combatMode;
    /// <summary>Cancel 窗口下一招解析委托；不用于 Locomotion 起手。</summary>
    readonly ActionResolverService _resolverService;
    readonly List<ICombatFrameConsumer> _frameConsumers = new();
    readonly List<IActionEventConsumer> _eventConsumers = new();
    readonly ActionSession _session = new();
    IActionInputBuffer _inputBuffer;
    IActionStartContext _startContext;

    /// <summary>当前招式会话；外部只读，状态写入集中在本执行器。</summary>
    public ActionSession Session => _session;

    public bool IsPlaying => _session.IsActive;
    public bool IsHitStopPaused => _session.IsHitStopPaused;
    public bool HasConfirmedHitThisAction => _session.HasConfirmedHit;
    public bool CanCancelByMovement =>
        _session.IsActive && _session.CurrentAction.IsInMovementCancelWindow(_session.ElapsedSeconds);
    public bool CanRotateByInput =>
        _session.IsActive && _session.CurrentAction.IsInRotationWindow(_session.ElapsedSeconds);
    public ActionDefinition CurrentAction => _session.CurrentAction;

    /// <summary>当前招式已播放秒数。</summary>
    public float ElapsedSeconds => _session.ElapsedSeconds;

    /// <summary>当前招式逻辑帧（与 ActionDefinition.sampleRate 对齐）。</summary>
    public int CurrentFrame =>
        _session.IsActive ? _session.CurrentAction.FrameAt(_session.ElapsedSeconds) : 0;

    float IActionExecutor.ElapsedSeconds => _session.ElapsedSeconds;

    int IActionExecutor.CurrentFrame =>
        _session.IsActive ? _session.CurrentAction.FrameAt(_session.ElapsedSeconds) : 0;

    /// <summary>Logic Tick 帧推进；编辑器 Scrub 与 Play Mode 共用。</summary>
    public event Action<CombatFrameContext> FrameAdvanced;

    /// <summary>创建纯 C# 招式执行器；所有依赖在 Bootstrap 阶段一次性注入。</summary>
    public ActionExecutor(
        Transform actorRoot,
        CharacterController motor,
        CharacterAnimationService animation,
        CharacterRootMotionDriver rootMotion,
        CombatModeService combatMode,
        ActionResolverService resolverService)
    {
        _actorRoot = actorRoot;
        _motor = motor;
        animationController = animation;
        _rootMotion = rootMotion;
        _combatMode = combatMode;
        _resolverService = resolverService;
    }

    /// <summary>注册 Logic Tick 消费者（Hitbox、VFX 等）。</summary>
    public void RegisterFrameConsumer(ICombatFrameConsumer consumer)
    {
        if (consumer != null && !_frameConsumers.Contains(consumer))
            _frameConsumers.Add(consumer);
    }

    /// <summary>注册 ActionEvent 轨道消费者；用于逐步替代旧 Hitbox/VFX 双轨。</summary>
    public void RegisterEventConsumer(IActionEventConsumer consumer)
    {
        if (consumer != null && !_eventConsumers.Contains(consumer))
            _eventConsumers.Add(consumer);
    }

    public void BindInputBuffer(IActionInputBuffer inputBuffer) => _inputBuffer = inputBuffer;

    public void BindActionStartContext(IActionStartContext startContext) => _startContext = startContext;

    public bool TryStart(ActionDefinition action)
    {
        if (_session.IsActive || action == null || action.AnimationClip == null || animationController == null)
            return false;

        ExecuteStartBehaviors(action);
        BeginAction(action);
        return true;
    }

    public void Tick(float deltaTime)
    {
        if (!_session.IsActive || _session.IsHitStopPaused)
            return;

        ActionDefinition current = _session.CurrentAction;
        _session.Advance(deltaTime);
        ApplyScriptedDisplacement(deltaTime);
        SyncLogicFrameFromElapsed();

        if (TryResolveCancelWindows())
            return;

        if (TryResolveTransitions())
            return;

        if (_session.ElapsedSeconds >= current.DurationSeconds)
            Stop();
    }

    /// <summary>编辑器 Scrub 与 Play Mode 共用的 Logic Tick 入口；要求招式已在播放中。</summary>
    public void UpdateFrame(int frameIndex)
    {
        if (!_session.IsActive || _session.IsHitStopPaused)
            return;

        frameIndex = Mathf.Clamp(frameIndex, 0, Mathf.Max(0, _session.CurrentAction.TotalFrames - 1));
        _session.SetFrame(frameIndex);
        AdvanceLogicFramesThrough(frameIndex);
    }

    public void Stop()
    {
        if (_session.IsActive)
            NotifyActionEnded();

        _session.Stop();
        _rootMotion?.SetActive(false);
    }

    /// <summary>卡肉期间暂停招式逻辑时间推进（由 HitStopController 驱动）。</summary>
    public void SetHitStopPaused(bool paused) => _session.SetHitStopPaused(paused);

    /// <summary>每招仅触发一次卡肉；返回 false 表示本招已触发过。</summary>
    public bool TryConsumeHitStopTrigger()
    {
        return _session.TryConsumeHitStopTrigger();
    }

    /// <summary>HitboxFrameConsumer 命中回流；支撑 OnHitConfirm Transition。</summary>
    public void NotifyHit(in ActionHitContext context)
    {
        if (!_session.IsActive || context.Action != _session.CurrentAction)
            return;

        _session.ConfirmHit();
    }

    /// <summary>按 priority 扫描 CancelWindow：消费匹配输入，下一招解析委托给 ActionResolverService。</summary>
    bool TryResolveCancelWindows()
    {
        ActionDefinition current = _session.CurrentAction;
        if (_inputBuffer == null || current == null || _resolverService == null)
            return false;

        foreach (ResolvedCancelWindow window in current.GetCancelWindowsSorted())
        {
            if (!current.IsInCancelWindow(window, _session.ElapsedSeconds))
                continue;

            if (window.CancelType != CancelType.Action)
                continue;

            if (!TryConsumeMatchingInput(window, out string matchedInputId))
                continue;

            var request = new ActionRequest(matchedInputId);
            var context = new ActionResolveContext(
                ActionResolveOrigin.CancelWindow,
                current,
                _actorRoot,
                _startContext);

            if (!_resolverService.TryResolveNext(in request, in context, out ActionDefinition nextAction))
                continue;

            if (nextAction == null || nextAction.AnimationClip == null)
                continue;

            ClearOtherActionBuffers(matchedInputId);
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
            if (_inputBuffer.HasBuffer(inputId))
            {
                _inputBuffer.TryConsumeBuffer(inputId);
                matchedInputId = inputId;
                return true;
            }
        }

        return false;
    }

    /// <summary>清空除已消费输入外的其它出招表输入缓冲，避免 Cancel 后残留触发。</summary>
    void ClearOtherActionBuffers(string keepInputId)
    {
        if (_inputBuffer == null || _resolverService == null)
            return;

        foreach (string inputId in _resolverService.EnumerateActiveInputIds())
        {
            if (inputId == keepInputId)
                continue;

            _inputBuffer.TryConsumeBuffer(inputId);
        }
    }

    /// <summary>按 priority 扫描 Transition，首个满足条件的自动衔接或 Stop。</summary>
    bool TryResolveTransitions()
    {
        ActionDefinition current = _session.CurrentAction;
        if (current == null)
            return false;

        foreach (ActionTransition transition in current.GetTransitionsSorted())
        {
            if (!current.IsTransitionEligible(
                    transition,
                    _session.ElapsedSeconds,
                    _session.HasConfirmedHit))
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

        CombatModeSwitchResult result = _combatMode.TrySetMode(mode, policy, _session.IsActive);
        if (result != CombatModeSwitchResult.RequiresStopCurrentAction)
            return;

        Stop();
        _combatMode.TrySetMode(mode, policy, false);
    }

    void BeginAction(ActionDefinition action)
    {
        _session.Begin(action);
        _rootMotion?.SetActive(action.UseRootMotion);
        animationController.PlayClip(action.AnimationClip, action.CrossFadeDuration);

        NotifyActionBegan(action);
        DispatchCombatFrame(0, -1);
        _session.LastProcessedFrame = 0;
    }

    void SyncLogicFrameFromElapsed()
    {
        int frame = _session.CurrentAction.FrameAt(_session.ElapsedSeconds);
        AdvanceLogicFramesThrough(frame);
    }

    /// <summary>从上一帧推进到 targetFrame，避免单帧大 delta 漏掉中间 Hitbox/VFX。</summary>
    void AdvanceLogicFramesThrough(int targetFrame)
    {
        if (targetFrame <= _session.LastProcessedFrame)
            return;

        for (int frame = _session.LastProcessedFrame + 1; frame <= targetFrame; frame++)
            DispatchCombatFrame(frame, frame - 1);

        _session.LastProcessedFrame = targetFrame;
    }

    void DispatchCombatFrame(int frameIndex, int previousFrameIndex)
    {
        var context = new CombatFrameContext(
            _session.CurrentAction,
            frameIndex,
            previousFrameIndex,
            _session.ElapsedSeconds,
            _actorRoot);
        FrameAdvanced?.Invoke(context);

        foreach (ICombatFrameConsumer consumer in _frameConsumers)
            consumer.OnCombatFrameAdvanced(in context);

        DispatchActionEvents(in context);
    }

    /// <summary>按帧派发 ActionEvent 轨道；旧 Hitbox/VFX 字段仍由 ICombatFrameConsumer 兼容消费。</summary>
    void DispatchActionEvents(in CombatFrameContext frameContext)
    {
        if (frameContext.Action == null)
            return;

        ActionEvent[] actionEvents = frameContext.Action.ActionEvents;
        if (actionEvents.Length == 0)
            return;

        foreach (ActionEvent actionEvent in actionEvents)
        {
            if (actionEvent == null)
                continue;

            if (!actionEvent.ShouldFireBetweenFrames(
                    frameContext.PreviousFrameIndex,
                    frameContext.FrameIndex))
                continue;

            var eventContext = new ActionEventContext(
                frameContext.Action,
                actionEvent,
                frameContext.FrameIndex,
                frameContext.PreviousFrameIndex,
                frameContext.ElapsedSeconds,
                frameContext.ActorRoot);

            foreach (IActionEventConsumer consumer in _eventConsumers)
                consumer.OnActionEvent(in eventContext);
        }
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

    void ApplyScriptedDisplacement(float deltaTime)
    {
        ActionDefinition current = _session.CurrentAction;
        if (_motor == null || current == null || !current.HasScriptedDisplacement)
            return;

        if (!current.IsInDisplacementWindow(_session.ElapsedSeconds))
            return;

        Vector3 forward = _actorRoot.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return;

        forward.Normalize();
        float signedSpeed = current.DisplacementSpeed;
        _motor.Move(forward * (signedSpeed * deltaTime));
    }
}
