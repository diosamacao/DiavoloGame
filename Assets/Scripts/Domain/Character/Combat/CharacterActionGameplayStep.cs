using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>消费 ActionSim 输出并按固定顺序执行动作位移、SoftBody 与 Hitbox Gameplay。</summary>
public sealed class CharacterActionGameplayStep
{
    readonly ActionSim _actionSim;
    readonly Transform _actorRoot;
    readonly CharacterMotor _motor;
    readonly CombatModeService _combatMode;
    readonly IActionStartContext _startContext;
    readonly List<ICombatFrameConsumer> _frameConsumers = new();
    readonly CharacterTargetingState _targetingState;
    readonly IActionMotionWorldQuery _worldQuery;
    readonly List<ActionSimEvent> _events = new(16);

    /// <summary>创建动作 Gameplay 固定帧执行器；表现只经只读 Sink 消费同批事件。</summary>
    public CharacterActionGameplayStep(
        ActionSim actionSim,
        Transform actorRoot,
        CharacterMotor motor,
        CombatModeService combatMode,
        IActionStartContext startContext,
        CharacterTargetingState targetingState = null,
        IActionMotionWorldQuery worldQuery = null)
    {
        _actionSim = actionSim ?? throw new ArgumentNullException(nameof(actionSim));
        _actorRoot = actorRoot;
        _motor = motor ?? throw new ArgumentNullException(nameof(motor));
        _combatMode = combatMode;
        _startContext = startContext;
        _targetingState = targetingState;
        _worldQuery = worldQuery;
    }

    /// <summary>注册整数动作帧 Gameplay 消费者；同一实例不会重复注册。</summary>
    public void RegisterFrameConsumer(ICombatFrameConsumer consumer)
    {
        if (consumer != null && !_frameConsumers.Contains(consumer))
            _frameConsumers.Add(consumer);
    }

    /// <summary>在 ActionSim.Step 后按原有两阶段顺序执行 Gameplay，并把只读输出交给表现 Sink。</summary>
    public void ApplyStep(float fixedDeltaSeconds, IActionPresentationSink presentationSink)
    {
        IActionPresentationSink sink = presentationSink ?? NullActionPresentationSink.Instance;
        float stepDelta = Mathf.Max(0f, fixedDeltaSeconds);
        _events.Clear();
        _actionSim.DrainEvents(_events);

        // 生命周期与终止哨兵先处理；普通推进帧必须等位移后再 Collect。
        for (int i = 0; i < _events.Count; i++)
        {
            ActionSimEvent actionEvent = _events[i];
            if (actionEvent.Type == ActionSimEventType.Started)
                HandleStarted(in actionEvent);
            else if (actionEvent.Type == ActionSimEventType.Stopped)
                HandleStopped();

            if (actionEvent.Type != ActionSimEventType.FrameAdvanced
                || actionEvent.PreviousFrame < 0
                || actionEvent.Frame >= actionEvent.Content.TotalFrames)
            {
                sink.ConsumeEvent(in actionEvent);
            }
        }

        ActionSimSnapshot snapshot = _actionSim.Snapshot;
        sink.ApplyBeforeGameplay(in snapshot, stepDelta);
        if (snapshot.IsActive
            && !snapshot.IsComplete
            && snapshot.Content is ActionDefinition current)
        {
            ApplySoftBodySuppressForFrame(current, snapshot.CurrentFrame);
            if (!snapshot.IsFrozen)
                ApplyDisplacementForAction(current, snapshot.CurrentFrame, stepDelta);
        }
        sink.ApplyAfterGameplay(in snapshot);

        // 普通推进帧在位移后派发，保持 Hitbox Collect 的历史固定顺序。
        for (int i = 0; i < _events.Count; i++)
        {
            ActionSimEvent actionEvent = _events[i];
            if (actionEvent.Type != ActionSimEventType.FrameAdvanced
                || actionEvent.PreviousFrame < 0
                || actionEvent.Frame >= actionEvent.Content.TotalFrames)
            {
                continue;
            }

            DispatchGameplayFrame(in actionEvent);
            sink.ConsumeEvent(in actionEvent);
        }
    }

    /// <summary>在命中结算后消费同帧新增事件；Gameplay 仍先于表现解释。</summary>
    public void ApplyPostCombat(IActionPresentationSink presentationSink)
    {
        IActionPresentationSink sink = presentationSink ?? NullActionPresentationSink.Instance;
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
                    DispatchGameplayFrame(in actionEvent);
                    break;
            }

            sink.ConsumeEvent(in actionEvent);
        }
    }

    /// <summary>执行图节点起手行为并通知 Gameplay 帧消费者。</summary>
    void HandleStarted(in ActionSimEvent actionEvent)
    {
        if (actionEvent.Content is not ActionDefinition action)
            return;

        ExecuteStartBehaviors(actionEvent.Graph as ActionGraph, actionEvent.NodeId);
        for (int i = 0; i < _frameConsumers.Count; i++)
            _frameConsumers[i].OnActionBegan(action);
    }

    /// <summary>结束动作 Gameplay 会话并清理 SoftBody 抑制。</summary>
    void HandleStopped()
    {
        for (int i = 0; i < _frameConsumers.Count; i++)
            _frameConsumers[i].OnActionEnded();
        _motor.Sim.ClearSoftBodySuppress();
    }

    /// <summary>仅将有效动作帧派发给 Gameplay 消费者；终止哨兵不产生判定。</summary>
    void DispatchGameplayFrame(in ActionSimEvent actionEvent)
    {
        if (actionEvent.Content is not ActionDefinition action
            || actionEvent.Frame >= action.TotalFrames)
        {
            return;
        }

        var context = new CombatFrameContext(
            action,
            actionEvent.Frame,
            actionEvent.PreviousFrame,
            _actorRoot,
            actionEvent.InstanceId);
        for (int i = 0; i < _frameConsumers.Count; i++)
            _frameConsumers[i].OnCombatFrameAdvanced(in context);
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

        ApplyTargetAdhesionForFrame(action, frame);
        ApplyMotionCommandsForFrame(action, frame);
    }

    /// <summary>SoftBodySuppress 窗内刷新抑制计数，同时保留静物碰撞。</summary>
    void ApplySoftBodySuppressForFrame(ActionDefinition action, int frame)
    {
        if (action != null && action.Timeline.IsSoftBodySuppressActiveAtFrame(frame))
            _motor.Sim.SetSoftBodySuppressFrames(1);
    }

    /// <summary>按当前逻辑目标执行逐帧 TargetAdhesion 修正。</summary>
    void ApplyTargetAdhesionForFrame(ActionDefinition action, int frame)
    {
        if (action == null || _worldQuery == null)
            return;

        MotionModifierNotifyState window = action.Timeline.GetActiveTargetAdhesionAtFrame(frame);
        if (window == null)
            return;

        SimActorId targetId = ResolveMotionTargetId(window.TargetSource);
        if (!targetId.IsValid
            || !_worldQuery.TryGetCommittedCombatPose(targetId, out SimCombatPose pose))
        {
            return;
        }

        SimVec2 actorMm = _motor.Sim.PositionMm;
        int targetXMm = MotionQuantization.MetersToMm(pose.Position.x);
        int targetZMm = MotionQuantization.MetersToMm(pose.Position.z);
        float yaw = MotionQuantization.MilliDegToDegrees(_motor.Sim.FacingMilliDeg);
        var adhesion = new ActionMotionAdhesionParams(
            window.StartFrame,
            window.EndFrame,
            window.HorizontalOffsetMm,
            window.LateralOffsetMm,
            window.MaxCorrectionMmPerFrame,
            window.MaxAcquireDistanceMm,
            window.MaxAngleMilliDeg);

        if (ActionMotionAdhesion.TryComputeCorrectionMm(
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
            _motor.MoveWorldMm(correctionXMm, correctionZMm);
        }
    }

    /// <summary>按优先级执行当前帧 MotionCommand，并应用失败策略。</summary>
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
        var fired = new List<MotionCommandNotify>(4);
        for (int i = 0; i < commands.Length; i++)
        {
            MotionCommandNotify command = commands[i];
            if (command != null && command.ShouldFireBetweenFrames(previousFrame, frame))
                fired.Add(command);
        }

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

            if (command.FallbackPolicy == MotionFallbackPolicy.CancelAction)
                _actionSim.Stop();
        }
    }

    /// <summary>把 MotionTargetSource 映射为当前逻辑目标。</summary>
    SimActorId ResolveMotionTargetId(MotionTargetSource source) =>
        source == MotionTargetSource.SelectedTarget && _targetingState != null
            ? _targetingState.Snapshot.SelectedTargetId
            : SimActorId.Invalid;

    /// <summary>解析动作基础位移唯一真源。</summary>
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

    /// <summary>按整数帧查表并向逻辑 Motor 施加本地位移。</summary>
    void ApplyBakedMotionDisplacement(ActionDefinition action, int frame)
    {
        if (action == null)
            return;

        ActionBakedMotion motion = action.BakedMotion;
        if (motion.IsReady && motion.TryGetDelta(frame, out SimVec2 deltaMm, out _))
            _motor.MoveLocalMm(deltaMm);
    }

    /// <summary>按脚本窗口速度向逻辑 Motor 施加世界前向位移。</summary>
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

    /// <summary>读取图节点并按配置顺序执行当前实例的起手 Gameplay 行为。</summary>
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

    /// <summary>按节点策略切换模式；要求停旧招时以新实例已提交语义重试。</summary>
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
