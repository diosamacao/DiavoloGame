using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>单角色动作执行器：播放已解析动作并推进帧、取消、图衔接与统一时间轴派发。</summary>
public sealed class ActionExecutor : IActionExecutor, IActionHitReceiver
{
    readonly Transform _actorRoot;
    readonly CharacterAnimationService animationController;
    readonly CharacterController _motor;
    readonly CharacterRootMotionDriver _rootMotion;
    /// <summary>同物体 CombatModeService；仅执行 ActionGraph 节点声明的模式切换。</summary>
    readonly CombatModeService _combatMode;
    /// <summary>Cancel 窗口下一招解析委托；不用于 Locomotion 起手。</summary>
    readonly ActionResolverService _resolverService;
    readonly List<ICombatFrameConsumer> _frameConsumers = new();
    readonly List<IActionNotifyConsumer> _notifyConsumers = new();
    readonly ActionTimelineRunner _timelineRunner = new();
    readonly ActionSession _session = new();
    readonly HashSet<GameplayIntentType> _cancelCandidateIntents = new();
    readonly HashSet<GameplayIntentType> _cancelRouteCandidateIntents = new();
    /// <summary>当前开放窗口的已缓冲候选，按 Cancel 优先级降序尝试。</summary>
    readonly List<GameplayIntentType> _cancelBufferedIntents = new(8);
    IActionInputBuffer _inputBuffer;
    IActionStartContext _startContext;
    Transform _timelineAttachPoint;
    int _lastEndedActionInstanceId;
    ActionResolveResult _pendingTransition;
    bool _hasPendingTransition;
    bool _pendingStop;

    /// <summary>当前招式会话；外部只读，状态写入集中在本执行器。</summary>
    public ActionSession Session => _session;

    public bool IsPlaying => _session.IsActive;
    public bool HasConfirmedHitThisAction => _session.HasConfirmedHit;
    /// <summary>开启移动取消的 Recovery Phase 允许返回 Locomotion。</summary>
    public bool CanCancelByMovement =>
        _session.IsActive
        && _session.CurrentAction.AllowsRecoveryMovementCancelAtFrame(CurrentFrame);
    public bool CanRotateByInput =>
        _session.IsActive && _session.CurrentAction.IsInRotationWindow(CurrentFrame);
    public ActionDefinition CurrentAction => _session.CurrentAction;

    /// <summary>当前权威动作帧；可等于 TotalFrames 表示完整时长结束。</summary>
    public int CurrentFrame => _session.IsActive ? _session.CurrentFrame : 0;

    /// <summary>当前动作会话编号；无动作时为 0。</summary>
    public int CurrentActionInstanceId => _session.InstanceId;

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

    /// <summary>注册统一 ActionNotify 时间轴消费者（VFX、SFX、相机等）。</summary>
    public void RegisterNotifyConsumer(IActionNotifyConsumer consumer)
    {
        if (consumer != null && !_notifyConsumers.Contains(consumer))
            _notifyConsumers.Add(consumer);
    }

    /// <summary>绑定 Notify 默认挂点；未绑定时使用角色根节点。</summary>
    public void BindTimelineAttachPoint(Transform attachPoint) => _timelineAttachPoint = attachPoint;

    public void BindInputBuffer(IActionInputBuffer inputBuffer) => _inputBuffer = inputBuffer;

    public void BindActionStartContext(IActionStartContext startContext) => _startContext = startContext;

    public bool TryStart(ActionDefinition action) =>
        TryStart(ActionResolveResult.FromAction(action));

    /// <summary>播放解析结果（可带图游标）；仅在当前无招式时成功。</summary>
    public bool TryStart(in ActionResolveResult resolveResult)
    {
        if (_session.IsActive || !resolveResult.IsValid || animationController == null)
            return false;

        ExecuteStartBehaviors(in resolveResult);
        BeginAction(in resolveResult);
        return true;
    }

    /// <summary>
    /// 高优硬打断：Session 活跃时，候选招 InterruptPriority 严格大于当前招，
    /// 且当前帧 IsInterruptibleAtFrame，则 TransitionTo 并清理其它动作缓冲。
    /// </summary>
    public bool TryInterrupt(in ActionResolveResult resolveResult)
    {
        if (!_session.IsActive || !resolveResult.IsValid || animationController == null)
            return false;

        ActionDefinition current = _session.CurrentAction;
        ActionDefinition next = resolveResult.Action;
        if (current == null || next == null)
            return false;

        if (next.ExecutionPolicy.InterruptPriority
            <= current.ExecutionPolicy.InterruptPriority)
            return false;

        if (!current.IsInterruptibleAtFrame(CurrentFrame))
            return false;

        ClearOtherActionBuffers(resolveResult.Intent);
        TransitionTo(in resolveResult);
        return true;
    }

    /// <summary>推进一个 SimulationWorld 固定帧；自动 Transition 和自然结束延迟到 PostCombat。</summary>
    public void Step(float fixedDeltaSeconds)
    {
        bool committedTransition = CommitPendingTransition();
        if (!_session.IsActive)
            return;

        // 延迟切招在本 World 帧只执行目标 frame 0，禁止同一步递归推进新动作。
        if (!committedTransition)
            _session.AdvanceFrame(SimulationConfig.DefaultLogicHz);
        SyncAnimationSegment();
        ApplyScriptedDisplacement(fixedDeltaSeconds);
        AdvanceLogicFramesThrough(CurrentFrame);

        // 终止哨兵帧只负责派发窗口 Exit，不能再接受 Cancel 或 Recovery 重开。
        if (_session.IsComplete)
            return;

        if (TryResolveCancelWindows())
            return;

        if (TryResolveRecoveryEntry())
            return;
    }

    /// <summary>停止当前动作并清理逻辑会话与 RootMotion 开关。</summary>
    public void Stop()
    {
        ClearPendingTransition();
        if (_session.IsActive)
            NotifyActionEnded();

        _session.Stop();
        _rootMotion?.SetActive(false);
    }

    /// <summary>查询指定动作会话是否已经停止或切换。</summary>
    public bool HasEndedActionInstance(int instanceId) =>
        instanceId > 0 && _lastEndedActionInstanceId == instanceId;

    /// <summary>HitboxFrameConsumer 命中回流；支撑 OnHitConfirm Transition。</summary>
    public void NotifyHit(in ActionHitContext context)
    {
        if (!_session.IsActive || context.Action != _session.CurrentAction)
            return;

        _session.ConfirmHit();
    }

    /// <summary>统一命中结算后解析自动衔接与动作结束，保证 OnHitConfirm 仍在命中所属逻辑帧生效。</summary>
    public void ResolvePostCombat()
    {
        if (!_session.IsActive)
            return;

        if (_hasPendingTransition || _pendingStop)
            return;

        if (TryResolveTransitions())
            return;

        if (_session.IsActive && _session.IsComplete)
            Stop();
    }

    /// <summary>
    /// 汇总当前帧的 Normal / Perfect 窗口；先按输入意图优先级，再对同一意图优先尝试 Perfect。
    /// </summary>
    bool TryResolveCancelWindows()
    {
        ActionDefinition current = _session.CurrentAction;
        if (_inputBuffer == null || current == null || _resolverService == null)
            return false;

        bool perfectActive =
            current.IsCancelWindowActiveAtFrame(CancelWindowType.Perfect, CurrentFrame);
        bool normalActive =
            current.IsCancelWindowActiveAtFrame(CancelWindowType.Normal, CurrentFrame);
        if (!perfectActive && !normalActive)
            return false;

        _cancelCandidateIntents.Clear();
        if (_session.HasGraphCursor)
        {
            if (perfectActive)
            {
                _cancelRouteCandidateIntents.Clear();
                _session.CurrentGraph.CollectCancelCandidateIntents(
                    _session.CurrentNodeId,
                    CancelWindowType.Perfect,
                    _cancelRouteCandidateIntents);
                _cancelCandidateIntents.UnionWith(_cancelRouteCandidateIntents);
            }

            if (normalActive)
            {
                _cancelRouteCandidateIntents.Clear();
                _session.CurrentGraph.CollectCancelCandidateIntents(
                    _session.CurrentNodeId,
                    CancelWindowType.Normal,
                    _cancelRouteCandidateIntents);
                _cancelCandidateIntents.UnionWith(_cancelRouteCandidateIntents);
            }
        }
        else
        {
            foreach (GameplayIntentType intent in _resolverService.EnumerateActiveIntents())
                _cancelCandidateIntents.Add(intent);
        }

        CollectBufferedCancelIntentsSorted();
        for (int i = 0; i < _cancelBufferedIntents.Count; i++)
        {
            GameplayIntentType intent = _cancelBufferedIntents[i];
            if (perfectActive
                && TryResolveCancelIntent(CancelWindowType.Perfect, intent))
            {
                return true;
            }

            if (normalActive
                && TryResolveCancelIntent(CancelWindowType.Normal, intent))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>解析单个意图在指定窗口类型上的边，并在成功时消费缓冲、排队下一帧 Action。</summary>
    bool TryResolveCancelIntent(CancelWindowType windowType, GameplayIntentType intent)
    {
        var request = new ActionRequest(intent);
        var context = new ActionResolveContext(
            ActionResolveOrigin.CancelWindow,
            _session.CurrentAction,
            _actorRoot,
            _startContext,
            windowType,
            _session.CurrentNodeId,
            hasCancelRoute: true);

        if (!_resolverService.TryResolveNext(
                in request,
                in context,
                out ActionResolveResult resolveResult)
            || !resolveResult.IsValid)
        {
            return false;
        }

        _inputBuffer.TryConsumeBuffer(intent);
        ClearOtherActionBuffers(intent);
        QueueTransition(in resolveResult);
        return true;
    }

    /// <summary>
    /// 开启 Entry Restart 的 Recovery Phase 软重开：无需 CancelWindow 或逐节点回根边，
    /// 直接用有效缓冲匹配当前 Graph 的 Entry；成功结果统一排队到下一 World 帧。
    /// </summary>
    bool TryResolveRecoveryEntry()
    {
        ActionDefinition current = _session.CurrentAction;
        if (_inputBuffer == null
            || current == null
            || _resolverService == null
            || !current.AllowsRecoveryEntryRestartAtFrame(CurrentFrame))
        {
            return false;
        }

        _cancelCandidateIntents.Clear();
        foreach (GameplayIntentType intent in _resolverService.EnumerateActiveIntents())
            _cancelCandidateIntents.Add(intent);

        CollectBufferedCancelIntentsSorted();
        for (int i = 0; i < _cancelBufferedIntents.Count; i++)
        {
            GameplayIntentType intent = _cancelBufferedIntents[i];
            var request = new ActionRequest(intent);
            var context = new ActionResolveContext(
                ActionResolveOrigin.RecoveryEntry,
                current,
                _actorRoot,
                _startContext,
                currentNodeId: _session.CurrentNodeId);

            if (!_resolverService.TryResolveStart(in request, in context, out ActionResolveResult result)
                || !result.IsValid)
            {
                continue;
            }

            _inputBuffer.TryConsumeBuffer(intent);
            ClearOtherActionBuffers(intent);
            QueueTransition(in result);
            return true;
        }

        return false;
    }

    /// <summary>从候选集筛出已缓冲意图，并按 Cancel 优先级降序排列。</summary>
    void CollectBufferedCancelIntentsSorted()
    {
        _cancelBufferedIntents.Clear();
        foreach (GameplayIntentType intent in _cancelCandidateIntents)
        {
            if (_inputBuffer.HasBuffer(intent))
                _cancelBufferedIntents.Add(intent);
        }

        _cancelBufferedIntents.Sort(CompareCancelIntentPriority);
    }

    /// <summary>Cancel 候选比较：优先级高者在前；同级保持稳定无关的枚举序。</summary>
    static int CompareCancelIntentPriority(GameplayIntentType a, GameplayIntentType b)
    {
        int byPriority = GameplayIntentCancelPriority.Get(b).CompareTo(GameplayIntentCancelPriority.Get(a));
        return byPriority != 0 ? byPriority : a.CompareTo(b);
    }

    /// <summary>
    /// 清空除已消费输入外的其它出招表输入缓冲。
    /// 连段消费 Attack 时按 <see cref="GameplayIntentCancelPriority.ShouldRetainAfterConsume"/> 保留 LongPressedAttack。
    /// </summary>
    void ClearOtherActionBuffers(GameplayIntentType keepIntent)
    {
        if (_inputBuffer == null || _resolverService == null)
            return;

        foreach (GameplayIntentType intent in _resolverService.EnumerateActiveIntents())
        {
            if (GameplayIntentCancelPriority.ShouldRetainAfterConsume(keepIntent, intent))
                continue;

            _inputBuffer.TryConsumeBuffer(intent);
        }
    }

    /// <summary>解析无输入自动衔接，并把结果排队到下一 World 帧提交。</summary>
    bool TryResolveTransitions()
    {
        if (!_session.HasGraphCursor
            || _session.CurrentAction == null
            || !_session.CurrentGraph.TryResolveAutomaticTransition(
                _session.CurrentNodeId,
                _session.CurrentAction,
                CurrentFrame,
                _session.HasConfirmedHit,
                out ActionResolveResult result,
                out bool shouldStop))
        {
            return false;
        }

        if (shouldStop)
        {
            _pendingStop = true;
            return true;
        }

        QueueTransition(in result);
        return true;
    }

    void TransitionTo(in ActionResolveResult resolveResult)
    {
        ClearPendingTransition();
        NotifyActionEnded();
        ExecuteStartBehaviors(in resolveResult);
        BeginAction(resolveResult);
    }

    /// <summary>记录当前帧判定出的切招，统一在下一 World 帧开始时提交。</summary>
    void QueueTransition(in ActionResolveResult resolveResult)
    {
        if (_hasPendingTransition || _pendingStop || !resolveResult.IsValid)
            return;

        _pendingTransition = resolveResult;
        _hasPendingTransition = true;
    }

    /// <summary>提交上一 World 帧排队的停止或切招；返回本帧是否刚开始目标 frame 0。</summary>
    bool CommitPendingTransition()
    {
        if (_pendingStop)
        {
            ClearPendingTransition();
            Stop();
            return false;
        }

        if (!_hasPendingTransition)
            return false;

        ActionResolveResult transition = _pendingTransition;
        ClearPendingTransition();
        TransitionTo(in transition);
        return true;
    }

    /// <summary>清空尚未提交的帧边界切招，供受击、死亡与硬打断覆盖旧决定。</summary>
    void ClearPendingTransition()
    {
        _pendingTransition = default;
        _hasPendingTransition = false;
        _pendingStop = false;
    }

    void BeginAction(in ActionResolveResult resolveResult)
    {
        ActionDefinition action = resolveResult.Action;
        _session.Begin(action);
        if (resolveResult.HasGraphCursor)
            _session.SetGraphCursor(resolveResult.Graph, resolveResult.NodeId);

        _rootMotion?.SetActive(action.ExecutionPolicy.UseRootMotion);
        PlayAnimationSegment(action, 0);

        NotifyActionBegan(action);
        DispatchCombatFrame(0, -1);
        _session.LastProcessedFrame = 0;
    }

    /// <summary>执行图节点声明的起手上下文行为；直接播放动作没有此类副作用。</summary>
    void ExecuteStartBehaviors(in ActionResolveResult resolveResult)
    {
        if (_startContext == null || !resolveResult.TryGetNode(out ActionGraphNode node))
            return;

        foreach (ActionGraphStartBehaviorType behavior in node.StartBehaviors)
            ExecuteStartBehavior(node, behavior);
    }

    /// <summary>解释单个节点起手行为，不向 ActionDefinition 反查上下文配置。</summary>
    void ExecuteStartBehavior(ActionGraphNode node, ActionGraphStartBehaviorType behavior)
    {
        switch (behavior)
        {
            case ActionGraphStartBehaviorType.FaceBufferedMoveIntent:
                _startContext.FaceBufferedMoveIntent();
                break;
            case ActionGraphStartBehaviorType.SwitchCombatMode:
                TrySwitchCombatMode(node.SwitchCombatModeTarget, node.SwitchCombatModePolicy);
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

    /// <summary>按当前整数动作帧切入对应动画段；同段不重复 Play。</summary>
    void SyncAnimationSegment()
    {
        ActionDefinition action = _session.CurrentAction;
        if (action == null || animationController == null)
            return;

        if (!action.TryGetSegmentAtFrame(
                CurrentFrame,
                out int segmentIndex,
                out ActionAnimationSegment segment,
                out _))
            return;

        if (segment.clip == null || segmentIndex == _session.CurrentAnimationSegmentIndex)
            return;

        PlayAnimationSegment(action, segmentIndex);
    }

    void PlayAnimationSegment(ActionDefinition action, int segmentIndex)
    {
        ActionAnimationSegment[] segments = action.AnimationSegments;
        if (segmentIndex < 0 || segmentIndex >= segments.Length)
            return;

        ActionAnimationSegment segment = segments[segmentIndex];
        if (segment.clip == null)
            return;

        float fade = action.ResolveSegmentCrossFade(segmentIndex);
        animationController.PlayClip(segment.clip, fade);

        // 段裁切：从 Clip 内 startFrame 对应时间起播。
        if (segment.startFrame > 0
            && segment.TryGetFrameRange(action.SampleRate, out int startInclusive, out _))
        {
            animationController.SeekClip(startInclusive / action.SampleRate);
        }

        _session.CurrentAnimationSegmentIndex = segmentIndex;
    }

    /// <summary>从上一帧推进到 targetFrame，稳定派发跨过的动作采样帧。</summary>
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
            _actorRoot);
        FrameAdvanced?.Invoke(context);

        foreach (ICombatFrameConsumer consumer in _frameConsumers)
            consumer.OnCombatFrameAdvanced(in context);

        _timelineRunner.Dispatch(in context, _timelineAttachPoint, _notifyConsumers);
    }

    void NotifyActionBegan(ActionDefinition action)
    {
        foreach (ICombatFrameConsumer consumer in _frameConsumers)
            consumer.OnActionBegan(action);
    }

    void NotifyActionEnded()
    {
        _lastEndedActionInstanceId = _session.InstanceId;
        foreach (ICombatFrameConsumer consumer in _frameConsumers)
            consumer.OnActionEnded();

        for (int i = 0; i < _notifyConsumers.Count; i++)
            _notifyConsumers[i].OnActionEnded();
    }

    void ApplyScriptedDisplacement(float deltaTime)
    {
        ActionDefinition current = _session.CurrentAction;
        if (_motor == null || current == null || !current.HasScriptedDisplacement)
            return;

        MovementNotifyState movement = current.GetActiveMovementStateAtFrame(CurrentFrame);
        if (movement == null)
            return;

        Vector3 forward = _actorRoot.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return;

        forward.Normalize();
        float signedSpeed = movement.ResolveSpeed(current.SampleRate);
        _motor.Move(forward * (signedSpeed * deltaTime));
    }
}
