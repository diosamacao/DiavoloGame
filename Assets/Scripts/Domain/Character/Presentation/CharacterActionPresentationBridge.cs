using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>消费 ActionSim 事件并驱动动画、位移、时间轴和角色侧起手副作用。</summary>
public sealed class CharacterActionPresentationBridge
{
    readonly ActionSim _actionSim;
    readonly Transform _actorRoot;
    readonly CharacterMotor _motor;
    readonly CharacterAnimationService _animation;
    readonly CharacterRootMotionDriver _rootMotion;
    readonly CombatModeService _combatMode;
    readonly IActionStartContext _startContext;
    readonly List<ICombatFrameConsumer> _frameConsumers = new();
    readonly List<IActionNotifyConsumer> _notifyConsumers = new();
    readonly ActionTimelineRunner _timelineRunner;
    readonly CharacterVisualMotionBridge _visualMotion;
    readonly CharacterTargetingState _targetingState;
    readonly IActionMotionWorldQuery _worldQuery;
    readonly bool _presentationEnabled;
    readonly List<ActionSimEvent> _events = new(16);
    Transform _defaultAttachPoint;
    ActionDefinition _animationAction;
    int _animationSegmentIndex = -1;
    bool _hitStopPresentationActive;
    float _normalAnimationSpeed = 1f;

    /// <summary>创建表现桥；所有 Unity 依赖只在该桥中消费纯模拟事件。</summary>
    public CharacterActionPresentationBridge(
        ActionSim actionSim,
        Transform actorRoot,
        CharacterMotor motor,
        CharacterAnimationService animation,
        CharacterRootMotionDriver rootMotion,
        CombatModeService combatMode,
        IActionStartContext startContext,
        ActionTimelineRunner timelineRunner,
        Transform defaultAttachPoint,
        CharacterVisualMotionBridge visualMotion = null,
        CharacterTargetingState targetingState = null,
        IActionMotionWorldQuery worldQuery = null,
        bool presentationEnabled = true)
    {
        _actionSim = actionSim ?? throw new ArgumentNullException(nameof(actionSim));
        _actorRoot = actorRoot;
        _motor = motor ?? throw new ArgumentNullException(nameof(motor));
        _animation = animation;
        _rootMotion = rootMotion;
        _combatMode = combatMode;
        _startContext = startContext;
        _timelineRunner = timelineRunner ?? new ActionTimelineRunner();
        _defaultAttachPoint = defaultAttachPoint != null ? defaultAttachPoint : actorRoot;
        _visualMotion = visualMotion;
        _targetingState = targetingState;
        _worldQuery = worldQuery;
        _presentationEnabled = presentationEnabled;
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

    /// <summary>角色退出可见阵容状态时，立即清理会随表现节点残留到下次登场的消费者实例。</summary>
    public void ResetForVisibilityLoss()
    {
        for (int i = 0; i < _notifyConsumers.Count; i++)
        {
            if (_notifyConsumers[i] is IActionVisibilityResetConsumer resettable)
                resettable.ResetForVisibilityLoss();
        }
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
        // 骨骼冻结跟随 ActionSim.freezeFrames；VFX 卡肉由 HitStopController 按逻辑帧递减
        if (_presentationEnabled)
            SyncHitStopPresentation(snapshot.IsFrozen);

        if (snapshot.IsActive
            && !snapshot.IsComplete
            && snapshot.Content is ActionDefinition current)
        {
            // 卡肉帧仍刷新 SoftBody 抑制，避免叠人窗被 Tick 清掉
            ApplySoftBodySuppressForFrame(current, snapshot.CurrentFrame);
            if (!snapshot.IsFrozen)
            {
                // 未冻结：对齐 Clip 相位并应用查表位移
                if (_presentationEnabled)
                    SyncAnimation(current, snapshot.CurrentFrame);
                ApplyDisplacementForAction(current, snapshot.CurrentFrame, stepDelta);
            }
        }

        // Wave 2：逻辑帧贴齐视觉残差（冻结时也保持当前帧残差，供挂点对齐）
        if (snapshot.IsActive && snapshot.Content is ActionDefinition residualAction)
        {
            int residualFrame = snapshot.CurrentFrame;
            if (residualFrame >= residualAction.TotalFrames)
                residualFrame = Mathf.Max(0, residualAction.TotalFrames - 1);
            _visualMotion?.CaptureSimulationFrame(residualAction, residualFrame, actionActive: true);
        }

        // 普通推进帧：位移后再派发 Hitbox/VFX 等过点事件
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
        // 命中结算可能刚写入 freezeFrames，同帧立即冻结骨骼
        if (_presentationEnabled)
            SyncHitStopPresentation(_actionSim.Snapshot.IsFrozen);

        // PostCombat 可能刚排队切招/停止，再排一次事件
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

        ActionGraph graph = actionEvent.Graph as ActionGraph;
        ExecuteStartBehaviors(graph, actionEvent.NodeId);
        // Action 位移仅 Baked/Scripted；禁止 Animator RM → Motor
        _rootMotion?.SetActive(false);
        for (int i = 0; i < _frameConsumers.Count; i++)
            _frameConsumers[i].OnActionBegan(action);
    }

    /// <summary>结束实例时先通知帧和音效消费者，再关闭 Root Motion，并退出视觉残差。</summary>
    void HandleStopped()
    {
        for (int i = 0; i < _frameConsumers.Count; i++)
            _frameConsumers[i].OnActionEnded();
        for (int i = 0; i < _notifyConsumers.Count; i++)
            _notifyConsumers[i].OnActionEnded();

        _rootMotion?.SetActive(false);
        _motor.Sim.ClearSoftBodySuppress();
        _animationAction = null;
        _animationSegmentIndex = -1;
        if (_presentationEnabled)
            SyncHitStopPresentation(frozen: false);
        // 取消/受击/自然结束统一短时回锚，避免模型停在偏移处
        _visualMotion?.EndAction(VisualResidualExitPolicy.BlendToZero);
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
        // Seek 前确保 RM 关闭，避免姿态跳变写入 Motor
        _rootMotion?.SetActive(false);

        _animation.PlayClip(segment.clip, action.ResolveSegmentCrossFade(segmentIndex));
        _animation.SeekClip(query.SegmentLocalTime);
        _animationAction = action;
        _animationSegmentIndex = segmentIndex;
    }

    /// <summary>按 Snapshot.FreezeFrames 启停攻击者动画 Speed=0。</summary>
    void SyncHitStopPresentation(bool frozen)
    {
        if (_animation == null)
            return;

        if (frozen)
        {
            if (_hitStopPresentationActive)
                return;

            _normalAnimationSpeed = _animation.Speed > 0f ? _animation.Speed : 1f;
            _animation.SetSpeed(0f);
            _hitStopPresentationActive = true;
            return;
        }

        if (!_hitStopPresentationActive)
            return;

        _animation.SetSpeed(_normalAnimationSpeed);
        _hitStopPresentationActive = false;
    }

    /// <summary>按 BaseMotionMode 施加基础位移，再叠 Modifier，最后执行 MotionCommand。</summary>
    void ApplyDisplacementForAction(ActionDefinition action, int frame, float fixedDeltaSeconds)
    {
        switch (ResolveDisplacementSource(action))
        {
            case ActionDisplacementSource.BakedMotion:
                ApplyBakedMotionDisplacement(action, frame);
                break;
            case ActionDisplacementSource.ScriptedTimeline:
                ApplyScriptedDisplacement(action, frame, fixedDeltaSeconds);
                break;
        }

        // Base → Modifier → Command（唯一组合顺序）
        ApplyTargetAdhesionForFrame(action, frame);
        ApplyMotionCommandsForFrame(action, frame);
    }

    /// <summary>SoftBodySuppress 窗内刷新抑制计数（仍碰静物墙）。</summary>
    void ApplySoftBodySuppressForFrame(ActionDefinition action, int frame)
    {
        if (action != null && action.Timeline.IsSoftBodySuppressActiveAtFrame(frame))
            _motor.Sim.SetSoftBodySuppressFrames(1);
    }

    /// <summary>TargetAdhesion：连线动态 desired + 剩余帧均摊，经 Motor 世界毫米移动。</summary>
    void ApplyTargetAdhesionForFrame(ActionDefinition action, int frame)
    {
        if (action == null || _worldQuery == null)
            return;

        MotionModifierNotifyState window = action.Timeline.GetActiveTargetAdhesionAtFrame(frame);
        if (window == null)
            return;

        SimActorId targetId = ResolveAdhesionTargetId(window);
        if (!targetId.IsValid)
            return;

        if (!_worldQuery.TryGetCommittedCombatPose(targetId, out SimCombatPose pose))
            return;

        SimVec2 actorMm = _motor.Sim.PositionMm;
        int targetXMm = MotionQuantization.MetersToMm(pose.Position.x);
        int targetZMm = MotionQuantization.MetersToMm(pose.Position.z);
        float yaw = MotionQuantization.MilliDegToDegrees(_motor.Sim.FacingMilliDeg);
        // Notify → Simulation 纯参，避免 Simulation 依赖 Timeline 类型
        var adhesion = new ActionMotionAdhesionParams(
            window.StartFrame,
            window.EndFrame,
            window.HorizontalOffsetMm,
            window.LateralOffsetMm,
            window.MaxCorrectionMmPerFrame,
            window.MaxAcquireDistanceMm,
            window.MaxAngleMilliDeg);

        if (!ActionMotionAdhesion.TryComputeCorrectionMm(
                actorMm.X,
                actorMm.Z,
                yaw,
                targetXMm,
                targetZMm,
                in adhesion,
                frame,
                out int correctionXMm,
                out int correctionZMm))
        {
            return;
        }

        _motor.MoveWorldMm(correctionXMm, correctionZMm);
    }

    /// <summary>按窗口 TargetSource 解析吸附目标 Id。</summary>
    SimActorId ResolveAdhesionTargetId(MotionModifierNotifyState window)
    {
        if (window == null)
            return SimActorId.Invalid;

        return ResolveMotionTargetId(window.TargetSource);
    }

    /// <summary>
    /// 本帧触发的 MotionCommand（previous=frame-1 → current=frame）；
    /// Relocate / SnapFacing 经 ActionMotionResolver 写入 MotorSim。
    /// </summary>
    void ApplyMotionCommandsForFrame(ActionDefinition action, int frame)
    {
        if (action == null || _worldQuery == null)
            return;

        MotionCommandNotify[] commands = action.Timeline.MotionCommandNotifies;
        if (commands == null || commands.Length == 0)
            return;

        int previousFrame = frame - 1;
        float heightY = _actorRoot != null ? _actorRoot.position.y : 0f;
        SimActorId selectedTargetId = _targetingState != null
            ? _targetingState.Snapshot.SelectedTargetId
            : SimActorId.Invalid;

        // 同帧多 Command：Priority 高者先执行（与 Timeline 点事件一致）
        var fired = new System.Collections.Generic.List<MotionCommandNotify>(4);
        for (int i = 0; i < commands.Length; i++)
        {
            MotionCommandNotify command = commands[i];
            if (command != null && command.ShouldFireBetweenFrames(previousFrame, frame))
                fired.Add(command);
        }

        if (fired.Count == 0)
            return;

        fired.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        for (int i = 0; i < fired.Count; i++)
        {
            MotionCommandNotify command = fired[i];
            SimCombatPose actorPose = SimCombatPose.FromMotor(_motor.Sim, heightY);
            ActionMotionResolveResult result = ActionMotionResolver.ExecuteCommand(
                command,
                _motor.Sim,
                _motor.Sim.CollisionWorld,
                in actorPose,
                selectedTargetId,
                _worldQuery);

            if (result.Applied)
            {
                _motor.SyncRootPoseFromSim();
                if (result.SoftBodySuppressFrames > 0)
                    _motor.Sim.SetSoftBodySuppressFrames(result.SoftBodySuppressFrames);
                continue;
            }

            // CancelCommand：忽略；CancelAction：结束当前招式
            if (command.FallbackPolicy == MotionFallbackPolicy.CancelAction)
                _actionSim.Stop();
        }
    }

    /// <summary>按 TargetSource 解析 SimActorId。</summary>
    SimActorId ResolveMotionTargetId(MotionTargetSource source) =>
        source == MotionTargetSource.SelectedTarget && _targetingState != null
            ? _targetingState.Snapshot.SelectedTargetId
            : SimActorId.Invalid;

    /// <summary>解析当前招式位移权威。</summary>
    static ActionDisplacementSource ResolveDisplacementSource(ActionDefinition action)
    {
        if (action == null)
            return ActionDisplacementSource.None;

        ActionExecutionPolicy policy = action.ExecutionPolicy;
        return ActionMotionRuntimePolicy.Resolve(
            policy.BaseMotionMode,
            action.BakedMotion.IsReady,
            action.Timeline.HasScriptedMovement);
    }

    /// <summary>按 currentFrame 查烘焙表施加水平位移；权威经 MotorSim，不读表 yaw。</summary>
    void ApplyBakedMotionDisplacement(ActionDefinition action, int frame)
    {
        if (action == null)
            return;

        ActionBakedMotion motion = action.BakedMotion;
        if (!motion.IsReady || !motion.TryGetDelta(frame, out SimVec2 deltaMm, out _))
            return;

        _motor.MoveLocalMm(deltaMm);
    }

    /// <summary>脚本位移窗口：世界前向速度经 MotorSim。</summary>
    void ApplyScriptedDisplacement(ActionDefinition action, int frame, float fixedDeltaSeconds)
    {
        if (action == null || !action.Timeline.HasScriptedMovement)
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
        _motor.MovePlanar(forward * (signedSpeed * fixedDeltaSeconds), fixedDeltaSeconds);
    }

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
