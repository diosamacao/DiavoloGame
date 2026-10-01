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
    // 捕获轴只属于当前动作窗口和目标；生命周期切换清空，不能跨动作复用。
    ActionMotionAdhesion.State _adhesion;
    MotionModifierNotifyState _adhesionWindow;
    SimActorId _adhesionTarget;
    int _adhesionTargetX, _adhesionTargetZ;
    int _inputMovementState;
    ActionInputMovement _inputMovementWindow;

    /// <summary>当前动作内移动方向及方向起始帧，供快照复制；动作结束即清空。</summary>
    public int InputMovementState => _inputMovementState;

    /// <summary>本 Tick 实际执行的输入移动请求（冻结为零位移）；PostCombat 换招不覆盖此历史。</summary>
    public ActionInputMovementCommand LastInputMovementCommand { get; private set; }

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
        LastInputMovementCommand = default;
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
            if (current.IsInputMovementActive(snapshot.CurrentFrame))
                LastInputMovementCommand = new ActionInputMovementCommand(SimVec2.Zero, _motor.Sim.FacingMilliDeg,
                    current.ExecutionPolicy.BodyCollisionMode, current.ExecutionPolicy.BodyContactSkinMm);
            else
            {
                _inputMovementState = 0;
                _inputMovementWindow = null;
            }
            if (!snapshot.IsFrozen && (!current.ExecutionPolicy.UsesInputMovement || HasAdvancedFrame(in snapshot)))
                ApplyDisplacementForAction(current, snapshot.CurrentFrame, stepDelta);
            if (current.IsInputMovementAnimationActive(snapshot.CurrentFrame))
                sink.ApplyInputMovement(current, snapshot.CurrentFrame, _inputMovementState);
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

    // FreezeFrames 递减至零的那一步没有推进动作帧，不可凭 IsFrozen=false 重复移动。
    bool HasAdvancedFrame(in ActionSimSnapshot snapshot)
    {
        foreach (ActionSimEvent actionEvent in _events)
            if (actionEvent.Type == ActionSimEventType.FrameAdvanced
                && actionEvent.InstanceId == snapshot.InstanceId && actionEvent.Frame == snapshot.CurrentFrame)
                return true;
        return false;
    }

    /// <summary>执行图节点起手行为并通知 Gameplay 帧消费者。</summary>
    void HandleStarted(in ActionSimEvent actionEvent)
    {
        if (actionEvent.Content is not ActionDefinition action)
            return;

        ResetAdhesion();
        ExecuteStartBehaviors(actionEvent.Graph as ActionGraph, actionEvent.NodeId);
        _inputMovementState = 0;
        _inputMovementWindow = null;
        for (int i = 0; i < _frameConsumers.Count; i++)
            _frameConsumers[i].OnActionBegan(action);
    }

    /// <summary>结束动作 Gameplay 会话并清理 SoftBody 抑制。</summary>
    void HandleStopped()
    {
        for (int i = 0; i < _frameConsumers.Count; i++)
            _frameConsumers[i].OnActionEnded();
        _motor.Sim.ClearSoftBodySuppress();
        _inputMovementState = 0;
        _inputMovementWindow = null;
        ResetAdhesion();
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

    /// <summary>先把基础位移重映射为吸附位移，只提交一次碰撞移动，最后执行 MotionCommand。</summary>
    void ApplyDisplacementForAction(ActionDefinition action, int frame, float fixedDeltaSeconds)
    {
        if (_actorRoot != null) _motor.Sim.SetFacingDegrees(_actorRoot.eulerAngles.y);
        SimVec2 delta = ResolveBaseDisplacement(action, frame, fixedDeltaSeconds);
        if (action.IsInputMovementActive(frame))
            LastInputMovementCommand = new ActionInputMovementCommand(delta, _motor.Sim.FacingMilliDeg,
                action.ExecutionPolicy.BodyCollisionMode, action.ExecutionPolicy.BodyContactSkinMm);
        bool adhered = TryApplyTargetAdhesion(action, frame, fixedDeltaSeconds, ref delta);
        float? scriptedSpeed = null;
        if (!adhered && ResolveDisplacementSource(action) == ActionDisplacementSource.ScriptedTimeline)
        {
            var movement = action.GetActiveMovementStateAtFrame(frame);
            if (movement != null && fixedDeltaSeconds > .0001f
                && Mathf.Abs(movement.ResolveSpeed(action.SampleRate) * fixedDeltaSeconds) >= .000316228f)
                scriptedSpeed = Mathf.Abs(movement.ResolveSpeed(action.SampleRate));
        }
        _motor.MoveActionMm(delta, action.ExecutionPolicy, scriptedSpeed);
        ApplyMotionCommandsForFrame(action, frame);
    }

    /// <summary>SoftBodySuppress 窗内刷新抑制计数，同时保留静物碰撞。</summary>
    void ApplySoftBodySuppressForFrame(ActionDefinition action, int frame)
    {
        if (action != null && action.Timeline.IsSoftBodySuppressActiveAtFrame(frame))
            _motor.Sim.SetSoftBodySuppressFrames(1);
    }

    void ResetAdhesion()
    {
        _adhesion = default; _adhesionWindow = null; _adhesionTarget = SimActorId.Invalid;
    }

    /// <summary>捕获时固定偏移轴，随后只跟随目标平移；丢失目标可按配置继续向最后落点收敛。</summary>
    bool TryApplyTargetAdhesion(ActionDefinition action, int frame, float dt, ref SimVec2 delta)
    {
        var window = action.Timeline.GetActiveTargetAdhesionAtFrame(frame);
        if (window == null) { ResetAdhesion(); return false; }
        if (_adhesionWindow != window) { ResetAdhesion(); _adhesionWindow = window; }
        SimActorId targetId = ResolveMotionTargetId(window.TargetSource);
        if (targetId.IsValid && _worldQuery != null
            && _worldQuery.TryGetCommittedCombatPose(targetId, out SimCombatPose pose))
        {
            if (_adhesionTarget != targetId) _adhesion = default;
            _adhesionTarget = targetId;
            _adhesionTargetX = MotionQuantization.MetersToMm(pose.Position.x);
            _adhesionTargetZ = MotionQuantization.MetersToMm(pose.Position.z);
        }
        else if (window.StopOnTargetLost || !_adhesion.Acquired)
        {
            _adhesion = default;
            return false;
        }
        var parameters = new ActionMotionAdhesionParams(window.StartFrame, window.EndFrame,
            window.HorizontalOffsetMm, window.LateralOffsetMm, window.MaxCorrectionMmPerFrame,
            window.MaxAcquireDistanceMm, window.MaxAngleMilliDeg);
        SimVec2 position = _motor.Sim.PositionMm;
        double progress = ActionMotionAdhesion.BakedProgress(
            ResolveDisplacementSource(action) == ActionDisplacementSource.BakedMotion ? action.BakedMotion : null,
            frame, window.EndFrame);
        if (ResolveDisplacementSource(action) == ActionDisplacementSource.ScriptedTimeline)
        {
            double remaining = 0, current = 0;
            for (int i = frame; i <= window.EndFrame; i++)
            {
                SimVec2 step = ResolveBaseDisplacement(action, i, dt);
                double length = Math.Sqrt((double)step.X * step.X + (double)step.Z * step.Z);
                if (i == frame) current = length;
                remaining += length;
            }
            if (remaining > 0) progress = current / remaining;
        }
        if (!ActionMotionAdhesion.TryComputeDisplacementMm(ref _adhesion,
                position.X, position.Z, MotionQuantization.MilliDegToDegrees(_motor.Sim.FacingMilliDeg),
                _adhesionTargetX, _adhesionTargetZ, in parameters, frame,
                delta.X, delta.Z, progress, out int x, out int z)) return false;
        delta = new SimVec2(x, z);
        return true;
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

    /// <summary>只计算基础世界位移，不提前移动电机，避免烘焙与吸附各移动一次产生过冲。</summary>
    SimVec2 ResolveBaseDisplacement(ActionDefinition action, int frame, float dt)
    {
        switch (ResolveDisplacementSource(action))
        {
            case ActionDisplacementSource.InputMovement:
                if (!action.IsInputMovementActive(frame)) return SimVec2.Zero;
                ActionInputMovement config = action.GetInputMovementAtFrame(frame);
                if (config == null || !config.IsValid) return SimVec2.Zero;
                if (_inputMovementWindow != config)
                {
                    _inputMovementState = ActionInputMoveState.Pack(0, frame);
                    _inputMovementWindow = config;
                }
                Vector3 wish = _motor.ResolveActionMoveWish(config.inputThreshold, out float magnitude);
                Vector3 facing = _actorRoot != null ? _actorRoot.forward : Vector3.forward;
                int direction = (int)LocomotionDirectionModel.Resolve(
                    LocomotionDirectionModel.ToLocalMoveIntent(wish, facing));
                _inputMovementState = config.AdvanceState(_inputMovementState, frame, direction);
                float distanceMm = config.speedMmPerSecond * magnitude * dt;
                return new SimVec2(Mathf.RoundToInt(wish.x * distanceMm), Mathf.RoundToInt(wish.z * distanceMm));
            case ActionDisplacementSource.BakedMotion:
                if (action.BakedMotion.TryGetDelta(frame, out var baked, out _))
                {
                    CharacterMotorSim.RotateLocalToWorld(_motor.Sim.FacingMilliDeg, baked.X, baked.Z, out int x, out int z);
                    return new SimVec2(x, z);
                }
                break;
            case ActionDisplacementSource.ScriptedTimeline:
                var movement = action.GetActiveMovementStateAtFrame(frame);
                if (movement == null) break;
                Vector3 forward = _actorRoot != null ? _actorRoot.forward : Vector3.forward;
                forward.y = 0;
                if (forward.sqrMagnitude < .0001f) break;
                Vector3 world = forward.normalized * (movement.ResolveSpeed(action.SampleRate) * dt);
                if (world.sqrMagnitude < .0000001f) return SimVec2.Zero;
                return new SimVec2(MotionQuantization.MetersToMm(world.x), MotionQuantization.MetersToMm(world.z));
        }
        return SimVec2.Zero;
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
