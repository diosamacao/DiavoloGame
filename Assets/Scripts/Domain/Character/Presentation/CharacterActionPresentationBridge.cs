using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>消费 ActionSim 事件并驱动动画、位移、时间轴和角色侧起手副作用。</summary>
public sealed class CharacterActionPresentationBridge
{
    readonly ActionSim _actionSim;
    readonly Transform _actorRoot;
    readonly CharacterController _controller;
    readonly CharacterAnimationService _animation;
    readonly CharacterRootMotionDriver _rootMotion;
    readonly CombatModeService _combatMode;
    readonly IActionStartContext _startContext;
    readonly List<ICombatFrameConsumer> _frameConsumers = new();
    readonly List<IActionNotifyConsumer> _notifyConsumers = new();
    readonly ActionTimelineRunner _timelineRunner;
    readonly List<ActionSimEvent> _events = new(16);
    Transform _defaultAttachPoint;
    ActionDefinition _animationAction;
    int _animationSegmentIndex = -1;

    /// <summary>创建表现桥；所有 Unity 依赖只在该桥中消费纯模拟事件。</summary>
    public CharacterActionPresentationBridge(
        ActionSim actionSim,
        Transform actorRoot,
        CharacterController controller,
        CharacterAnimationService animation,
        CharacterRootMotionDriver rootMotion,
        CombatModeService combatMode,
        IActionStartContext startContext,
        ActionTimelineRunner timelineRunner,
        Transform defaultAttachPoint)
    {
        _actionSim = actionSim ?? throw new ArgumentNullException(nameof(actionSim));
        _actorRoot = actorRoot;
        _controller = controller;
        _animation = animation;
        _rootMotion = rootMotion;
        _combatMode = combatMode;
        _startContext = startContext;
        _timelineRunner = timelineRunner ?? new ActionTimelineRunner();
        _defaultAttachPoint = defaultAttachPoint != null ? defaultAttachPoint : actorRoot;
    }

    /// <summary>注册整数动作帧消费者；同一实例不会重复注册。</summary>
    public void RegisterFrameConsumer(ICombatFrameConsumer consumer)
    {
        if (consumer != null && !_frameConsumers.Contains(consumer))
            _frameConsumers.Add(consumer);
    }

    /// <summary>注册统一时间轴通知消费者；同一实例不会重复注册。</summary>
    public void RegisterNotifyConsumer(IActionNotifyConsumer consumer)
    {
        if (consumer != null && !_notifyConsumers.Contains(consumer))
            _notifyConsumers.Add(consumer);
    }

    /// <summary>更新时间轴默认挂点；空值回退角色根。</summary>
    public void BindDefaultAttachPoint(Transform attachPoint) =>
        _defaultAttachPoint = attachPoint != null ? attachPoint : _actorRoot;

    /// <summary>在 ActionSim.Step 后消费本逻辑步事件并按帧顺序应用表现。</summary>
    public void ApplyStep(float fixedDeltaSeconds)
    {
        float stepDelta = Mathf.Max(0f, fixedDeltaSeconds);
        _events.Clear();
        _actionSim.DrainEvents(_events);

        // 生命周期与 frame 0 先于本步位移；普通推进帧则在位移后派发判定。
        for (int i = 0; i < _events.Count; i++)
        {
            ActionSimEvent actionEvent = _events[i];
            if (actionEvent.Type == ActionSimEventType.Started)
                HandleStarted(in actionEvent);
            else if (actionEvent.Type == ActionSimEventType.Stopped)
                HandleStopped();
            else if (actionEvent.Type == ActionSimEventType.FrameAdvanced
                && (actionEvent.PreviousFrame < 0
                    || actionEvent.Frame >= actionEvent.Content.TotalFrames))
                DispatchFrameEvent(in actionEvent);
        }

        ActionSimSnapshot snapshot = _actionSim.Snapshot;
        if (snapshot.IsActive
            && !snapshot.IsComplete
            && snapshot.Content is ActionDefinition current)
        {
            SyncAnimation(current, snapshot.CurrentFrame);
            // 有就绪运动表则只信表；否则脚本位移 /（未烘焙时）Animator RM
            if (!ApplyBakedMotionDisplacement(current, snapshot.CurrentFrame))
                ApplyScriptedDisplacement(current, snapshot.CurrentFrame, stepDelta);
        }

        for (int i = 0; i < _events.Count; i++)
        {
            ActionSimEvent actionEvent = _events[i];
            if (actionEvent.Type == ActionSimEventType.FrameAdvanced
                && actionEvent.PreviousFrame >= 0
                && actionEvent.Frame < actionEvent.Content.TotalFrames)
                DispatchFrameEvent(in actionEvent);
        }
    }

    /// <summary>在战斗结算与状态收尾后消费同帧新增的停止或反应动作事件。</summary>
    public void ApplyPostCombat()
    {
        _events.Clear();
        _actionSim.DrainEvents(_events);
        for (int i = 0; i < _events.Count; i++)
        {
            ActionSimEvent actionEvent = _events[i];
            switch (actionEvent.Type)
            {
                case ActionSimEventType.Started:
                    HandleStarted(in actionEvent);
                    break;
                case ActionSimEventType.Stopped:
                    HandleStopped();
                    break;
                case ActionSimEventType.FrameAdvanced:
                    DispatchFrameEvent(in actionEvent);
                    break;
            }
        }
    }

    /// <summary>新实例已由 Sim 建立后执行节点行为，再开启 Root Motion 并通知消费者。</summary>
    void HandleStarted(in ActionSimEvent actionEvent)
    {
        if (actionEvent.Content is not ActionDefinition action)
            return;

        ExecuteStartBehaviors(actionEvent.Graph as ActionGraph, actionEvent.NodeId);
        // 烘焙表就绪后禁止 OnAnimatorMove，避免与查表位移双加
        _rootMotion?.SetActive(ShouldUseAnimatorRootMotion(action));
        for (int i = 0; i < _frameConsumers.Count; i++)
            _frameConsumers[i].OnActionBegan(action);
    }

    /// <summary>结束实例时先通知帧和音效消费者，再关闭 Root Motion。</summary>
    void HandleStopped()
    {
        for (int i = 0; i < _frameConsumers.Count; i++)
            _frameConsumers[i].OnActionEnded();
        for (int i = 0; i < _notifyConsumers.Count; i++)
            _notifyConsumers[i].OnActionEnded();

        _rootMotion?.SetActive(false);
        _animationAction = null;
        _animationSegmentIndex = -1;
    }

    /// <summary>按整数帧派发判定与时间轴；终止哨兵只产生区间 Exit。</summary>
    void DispatchFrameEvent(in ActionSimEvent actionEvent)
    {
        if (actionEvent.Content is not ActionDefinition action)
            return;

        var context = new CombatFrameContext(
            action,
            actionEvent.Frame,
            actionEvent.PreviousFrame,
            _actorRoot,
            actionEvent.InstanceId);
        if (actionEvent.Frame >= action.TotalFrames)
        {
            _timelineRunner.DispatchTerminalExits(
                in context,
                _defaultAttachPoint,
                _notifyConsumers);
            return;
        }

        SyncAnimation(action, actionEvent.Frame);
        DispatchFrame(in context);
    }

    /// <summary>派发战斗消费者和统一时间轴，保持两者读取同一帧上下文。</summary>
    void DispatchFrame(in CombatFrameContext context)
    {
        for (int i = 0; i < _frameConsumers.Count; i++)
            _frameConsumers[i].OnCombatFrameAdvanced(in context);

        _timelineRunner.Dispatch(in context, _defaultAttachPoint, _notifyConsumers);
    }

    /// <summary>仅在动作/段切换时 Play+Seek；同段内由 Animation.Tick 固定步长推进。</summary>
    void SyncAnimation(ActionDefinition action, int frame)
    {
        ActionFrameQueryResult query = ActionFrameQuery.Query(action, frame);
        if (_animation == null || !query.HasAnimationSegment)
            return;

        int segmentIndex = query.SegmentIndex;
        if (_animationAction == action && _animationSegmentIndex == segmentIndex)
            return;

        ActionAnimationSegment segment = query.Segment;
        bool restoreAnimatorRm = false;
        if (_rootMotion != null && ShouldUseAnimatorRootMotion(action))
        {
            // Seek/Evaluate(0) 的姿态跳变不能写进 CharacterController。
            _rootMotion.SetActive(false);
            restoreAnimatorRm = true;
        }

        _animation.PlayClip(segment.clip, action.ResolveSegmentCrossFade(segmentIndex));
        _animation.SeekClip(query.SegmentLocalTime);
        _animationAction = action;
        _animationSegmentIndex = segmentIndex;

        if (restoreAnimatorRm)
            _rootMotion.SetActive(true);
    }

    /// <summary>按 currentFrame 查烘焙表施加水平位移；朝向只由 ActionRotation/索敌/输入控制，不读表 yaw。</summary>
    bool ApplyBakedMotionDisplacement(ActionDefinition action, int frame)
    {
        if (_controller == null || action == null)
            return false;

        ActionBakedMotion motion = action.BakedMotion;
        if (!motion.IsReady || !motion.TryGetDelta(frame, out SimVec2 deltaMm, out _))
            return false;

        Vector3 localDelta = new(
            MotionQuantization.MmToMeters(deltaMm.X),
            0f,
            MotionQuantization.MmToMeters(deltaMm.Z));

        Transform root = _actorRoot != null ? _actorRoot : _controller.transform;
        Vector3 worldDelta = root.rotation * localDelta;
        worldDelta.y = 0f;
        if (worldDelta.sqrMagnitude > 0.0000001f)
            _controller.Move(worldDelta);

        return true;
    }

    /// <summary>临时通过 CharacterController 执行脚本位移；有烘焙表时不会走到这里。</summary>
    void ApplyScriptedDisplacement(ActionDefinition action, int frame, float fixedDeltaSeconds)
    {
        if (_controller == null || !action.HasScriptedDisplacement)
            return;

        MovementNotifyState movement = action.GetActiveMovementStateAtFrame(frame);
        if (movement == null)
            return;

        Vector3 forward = _actorRoot != null ? _actorRoot.forward : Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            return;

        forward.Normalize();
        float signedSpeed = movement.ResolveSpeed(action.SampleRate);
        _controller.Move(forward * (signedSpeed * fixedDeltaSeconds));
    }

    /// <summary>仅未烘焙且策略要求时启用 Animator Root Motion。</summary>
    static bool ShouldUseAnimatorRootMotion(ActionDefinition action) =>
        action != null
        && ActionMotionRuntimePolicy.ShouldUseAnimatorRootMotion(
            action.ExecutionPolicy.UseRootMotion,
            action.BakedMotion.IsReady);

    /// <summary>读取图节点并按配置顺序执行当前实例的起手行为。</summary>
    void ExecuteStartBehaviors(ActionGraph graph, string nodeId)
    {
        if (graph == null || !graph.TryGetNode(nodeId, out ActionGraphNode node))
            return;

        foreach (ActionGraphStartBehaviorType behavior in node.StartBehaviors)
        {
            switch (behavior)
            {
                case ActionGraphStartBehaviorType.FaceBufferedMoveIntent:
                    _startContext?.FaceBufferedMoveIntent();
                    break;
                case ActionGraphStartBehaviorType.SwitchCombatMode:
                    TrySwitchCombatMode(node);
                    break;
            }
        }
    }

    /// <summary>Sim 已提交新实例；要求停旧招时直接以无活动旧招语义重试，绝不停止新实例。</summary>
    void TrySwitchCombatMode(ActionGraphNode node)
    {
        if (_combatMode == null)
            return;

        CombatModeSwitchResult result = _combatMode.TrySetMode(
            node.SwitchCombatModeTarget,
            node.SwitchCombatModePolicy,
            isActionPlaying: true);
        if (result == CombatModeSwitchResult.RequiresStopCurrentAction)
        {
            _combatMode.TrySetMode(
                node.SwitchCombatModeTarget,
                node.SwitchCombatModePolicy,
                isActionPlaying: false);
        }
    }
}
