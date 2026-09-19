using System;
using UnityEngine;

/// <summary>独占单角色 Party 状态、退场序列、支援意图与换人落位行为。</summary>
public sealed class CharacterPartyLifecycle
{
    /// <summary>普通退场内部阶段；原招交接后只认 SwitchOut 自身 Recovery。</summary>
    enum ExitMode
    {
        None = 0,
        WaitForCurrentRecovery = 1,
        PlayingSwitchOut = 2,
    }

    readonly CharacterMotor _motor;
    readonly CharacterStateMachine _stateMachine;
    readonly ActionSim _actionSim;
    readonly IActionPresentationSink _actionPresentation;
    readonly CharacterAnimationService _animation;
    readonly ICharacterPresentationSink _presentation;
    readonly NumericSystem _numeric;
    readonly GameplayIntentBuffer _intentBuffer;
    readonly CharacterTargetingState _targetingState;
    readonly Func<SimActorId> _simulationId;
    readonly Action _clearControlledInput;
    readonly Action _alignSimulationRootToMotor;
    readonly Action _snapPresentationToSimulation;
    PartyMemberState _state = PartyMemberState.Active;
    GameplayIntentType _queuedExternalIntent = GameplayIntentType.None;
    ExitMode _exitMode;
    int _switchOutActionInstanceId;
    int _assistParryHitStopCarryFrames;

    /// <summary>创建角色 Party 生命周期；所有状态变更只允许经此对象执行。</summary>
    internal CharacterPartyLifecycle(
        CharacterMotor motor,
        CharacterStateMachine stateMachine,
        ActionSim actionSim,
        IActionPresentationSink actionPresentation,
        CharacterAnimationService animation,
        ICharacterPresentationSink presentation,
        NumericSystem numeric,
        GameplayIntentBuffer intentBuffer,
        CharacterTargetingState targetingState,
        Func<SimActorId> simulationId,
        Action clearControlledInput,
        Action alignSimulationRootToMotor,
        Action snapPresentationToSimulation)
    {
        _motor = motor;
        _stateMachine = stateMachine;
        _actionSim = actionSim;
        _actionPresentation = actionPresentation;
        _animation = animation;
        _presentation = presentation;
        _numeric = numeric;
        _intentBuffer = intentBuffer;
        _targetingState = targetingState;
        _simulationId = simulationId ?? throw new ArgumentNullException(nameof(simulationId));
        _clearControlledInput =
            clearControlledInput ?? throw new ArgumentNullException(nameof(clearControlledInput));
        _alignSimulationRootToMotor = alignSimulationRootToMotor
            ?? throw new ArgumentNullException(nameof(alignSimulationRootToMotor));
        _snapPresentationToSimulation = snapPresentationToSimulation
            ?? throw new ArgumentNullException(nameof(snapPresentationToSimulation));
    }

    /// <summary>当前阵容槽状态；非 Party 角色默认保持 Active。</summary>
    public PartyMemberState State => _state;

    /// <summary>已启动的 SwitchOut 是否进入可提交后台的 Recovery。</summary>
    public bool IsExitReady
    {
        get
        {
            if (_state != PartyMemberState.Exiting)
                return false;

            ActionSimSnapshot action = _actionSim.Snapshot;
            return _exitMode == ExitMode.PlayingSwitchOut
                && _switchOutActionInstanceId > 0
                && action.IsActive
                && action.InstanceId == _switchOutActionInstanceId
                && action.Content is ActionDefinition definition
                && definition.IsRecoveryAtFrame(action.CurrentFrame);
        }
    }

    /// <summary>切换槽生命周期并同步显隐；后台或死亡状态会终止输入和未完成动作。</summary>
    public void SetState(PartyMemberState state)
    {
        bool wasVisible =
            _state == PartyMemberState.Active || _state == PartyMemberState.Exiting;
        bool visible = state == PartyMemberState.Active || state == PartyMemberState.Exiting;
        if (wasVisible && !visible)
        {
            // 隐藏前回收挂点 VFX，避免粒子随父节点冻结并在下次上场复活。
            _actionPresentation?.ResetForVisibilityLoss();
            _animation?.StopAdditive();
        }

        _state = state;
        if (_presentation?.PresentationRoot != null)
            _presentation.PresentationRoot.gameObject.SetActive(visible);

        if (state == PartyMemberState.Inactive
            || state == PartyMemberState.Dead
            || state == PartyMemberState.Empty)
        {
            if (state == PartyMemberState.Inactive)
                StopActionForPartyTransition();
            _exitMode = ExitMode.None;
            _switchOutActionInstanceId = 0;
            _queuedExternalIntent = GameplayIntentType.None;
            _clearControlledInput();
        }
    }

    /// <summary>开始普通退场；原招到首次 Recovery 后转入独立 SwitchOut。</summary>
    public void BeginExit()
    {
        if (_state != PartyMemberState.Active)
            throw new InvalidOperationException("只有 Active 角色可以开始普通退场。");

        ActionSimSnapshot action = _actionSim.Snapshot;
        bool hasActiveAction = action.IsActive;
        bool alreadyInRecovery = hasActiveAction
            && action.Content is ActionDefinition definition
            && definition.IsRecoveryAtFrame(action.CurrentFrame);
        SetState(PartyMemberState.Exiting);
        _exitMode = hasActiveAction
            ? ExitMode.WaitForCurrentRecovery
            : ExitMode.PlayingSwitchOut;
        _switchOutActionInstanceId = 0;
        if (!hasActiveAction)
        {
            // 纯帧 Hurt 无 ActionSim；先离开 Hit 才能从 Locomotion Entry 起 SwitchOut。
            ReleaseHitOrActionForPartyTransition();
            QueueExternalIntent(GameplayIntentType.SwitchOut);
        }
        else if (alreadyInRecovery)
        {
            BeginSwitchOutAfterCurrentAction();
        }
    }

    /// <summary>SwitchOut 进入 Recovery 后提交后台状态。</summary>
    public void CompleteExit()
    {
        if (!IsExitReady)
            throw new InvalidOperationException("角色尚未满足普通退场条件。");

        SetState(PartyMemberState.Inactive);
    }

    /// <summary>排队协调器裁定的外部意图；下一次 Step 在设备意图之后注入。</summary>
    public void QueueExternalIntent(GameplayIntentType intent)
    {
        if (intent == GameplayIntentType.None)
            throw new ArgumentException("外部意图不能为 None。", nameof(intent));
        if (_queuedExternalIntent != GameplayIntentType.None)
            throw new InvalidOperationException("同一 Actor 已有待处理的外部意图。");
        _queuedExternalIntent = intent;
    }

    /// <summary>在 Producer 开帧后发出协调器意图；成功写入缓冲后才清除队列。</summary>
    internal void EmitQueuedExternalIntent()
    {
        if (_queuedExternalIntent == GameplayIntentType.None)
            return;

        _intentBuffer.Emit(_queuedExternalIntent);
        _queuedExternalIntent = GameplayIntentType.None;
    }

    /// <summary>招架窗接触时武装支援派生，并把待处理意图提升为成功动作。</summary>
    public void NotifyAssistParryContact()
    {
        _numeric.ArmAssistFollowUp();
        if (IsPlayingAssistParrySuccess()
            || _queuedExternalIntent == GameplayIntentType.AssistParrySuccess)
        {
            return;
        }

        if (_queuedExternalIntent == GameplayIntentType.None
            || _queuedExternalIntent == GameplayIntentType.AssistParry
            || _queuedExternalIntent == GameplayIntentType.Parry)
        {
            _queuedExternalIntent = GameplayIntentType.AssistParrySuccess;
        }
    }

    /// <summary>记下弹刀卡肉帧数，待 AssistParrySuccess 起手后写入新动作实例。</summary>
    public void ArmAssistParryHitStopCarry(int frames) =>
        _assistParryHitStopCarryFrames = frames > 0 ? frames : 0;

    /// <summary>ActionSim 起手回调；只处理角色内部的弹刀卡肉转移。</summary>
    public void NotifyActionBegun(GameplayIntentType intent)
    {
        if (intent == GameplayIntentType.AssistParrySuccess
            && _assistParryHitStopCarryFrames > 0
            && _actionSim != null
            && _actionSim.IsActive)
        {
            _actionSim.RequestHitStop(
                _actionSim.InstanceId,
                _assistParryHitStopCarryFrames,
                oncePerAction: true);
        }

        _assistParryHitStopCarryFrames = 0;
    }

    /// <summary>当前 Action 位于支援闪光窗时，将只读 Cue 写入世界公告板。</summary>
    public bool TryPublishAssistCue(WorldAssistCueBoard board)
    {
        if (board == null || _actionSim == null || !_actionSim.IsActive)
            return false;
        if (_actionSim.Snapshot.Content is not ActionDefinition action)
            return false;
        if (!action.TryGetAssistCueAtFrame(
                _actionSim.CurrentFrame,
                out AssistCueNotifyState state))
        {
            return false;
        }

        Vector3 parry = state.ParryLocalOffsetMm;
        Vector3 evade = state.EvadeLocalOffsetMm;
        board.Publish(new AssistCue(
            _simulationId(),
            state.Kind,
            state.RequiresRanged,
            state.RemainingFramesAt(_actionSim.CurrentFrame),
            Mathf.RoundToInt(parry.x),
            Mathf.RoundToInt(parry.y),
            Mathf.RoundToInt(parry.z),
            Mathf.RoundToInt(evade.x),
            Mathf.RoundToInt(evade.y),
            Mathf.RoundToInt(evade.z)));
        return true;
    }

    /// <summary>InstantReplace 上场时继承旧位姿，并按 Cue 经碰撞移动到弹刀或回避点。</summary>
    public void PlaceForAssistSwitchFrom(
        CharacterActor outgoing,
        in AssistCue cue,
        bool evade)
    {
        if (outgoing == null)
            throw new ArgumentNullException(nameof(outgoing));

        CharacterMotorSim outgoingMotor = outgoing.MotorSim;
        SimVec2 from = outgoingMotor.PositionMm;
        _motor.Sim.TeleportMm(from.X, outgoingMotor.YMm, from.Z);

        if (cue.IsValid && _targetingState.TryGetSelectedCombatPose(out SimCombatPose pose))
        {
            Vector3 localM = new(
                MotionQuantization.MmToMeters(evade ? cue.EvadeLocalXMm : cue.ParryLocalXMm),
                0f,
                MotionQuantization.MmToMeters(evade ? cue.EvadeLocalZMm : cue.ParryLocalZMm));
            Vector3 world = pose.TransformPoint(localM);
            int desiredX = MotionQuantization.MetersToMm(world.x);
            int desiredZ = MotionQuantization.MetersToMm(world.z);
            _motor.Sim.TryMoveWorldMm(desiredX - from.X, desiredZ - from.Z);
        }

        AlignSwitchFacing(outgoingMotor.FacingMilliDeg);
        _alignSimulationRootToMotor();
        _snapPresentationToSimulation();
    }

    /// <summary>普通换人时从旧位置经静态碰撞移动到其局部右侧。</summary>
    public void PlaceForNormalSwitchFrom(CharacterActor outgoing)
    {
        if (outgoing == null)
            throw new ArgumentNullException(nameof(outgoing));

        CharacterMotorSim outgoingMotor = outgoing.MotorSim;
        SimVec2 outgoingPosition = outgoingMotor.PositionMm;
        SimVec2 desired = PartySwitchPlacement.ResolveNormalSwitchPosition(
            outgoingPosition,
            outgoingMotor.FacingMilliDeg);
        _motor.Sim.TeleportMm(
            outgoingPosition.X,
            outgoingMotor.YMm,
            outgoingPosition.Z);
        _motor.Sim.TryMoveWorldMm(
            desired.X - outgoingPosition.X,
            desired.Z - outgoingPosition.Z);
        AlignSwitchFacing(outgoingMotor.FacingMilliDeg);
        _alignSimulationRootToMotor();
        _snapPresentationToSimulation();
    }

    /// <summary>继承旧朝向；若本槽已有 SelectedTarget 则立即朝向其逻辑位置。</summary>
    public void AlignSwitchFacing(int inheritedFacingMilliDeg)
    {
        int facingMilliDeg = inheritedFacingMilliDeg;
        if (_targetingState.TryGetSelectedDirection(_motor.Sim, out Vector3 direction))
        {
            float facingDegrees = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            facingMilliDeg = Mathf.RoundToInt(facingDegrees * 1000f);
        }
        _motor.Sim.SetFacingMilliDeg(facingMilliDeg);
    }

    /// <summary>受击打断已启动的 SwitchOut 时退回等待交接，避免槽永久卡在 Exiting。</summary>
    internal void NotifyEnteredHit()
    {
        if (_state != PartyMemberState.Exiting || _exitMode != ExitMode.PlayingSwitchOut)
            return;

        _exitMode = ExitMode.WaitForCurrentRecovery;
        _switchOutActionInstanceId = 0;
        if (_queuedExternalIntent == GameplayIntentType.SwitchOut)
            _queuedExternalIntent = GameplayIntentType.None;
    }

    /// <summary>PostCombat 末尾推进原招 Recovery 到 SwitchOut 的唯一退场序列。</summary>
    internal void AdvanceAfterPostCombat()
    {
        if (_state != PartyMemberState.Exiting)
            return;

        ActionSimSnapshot action = _actionSim.Snapshot;
        if (_exitMode == ExitMode.WaitForCurrentRecovery)
        {
            bool reachedHandoff = !action.IsActive
                || (action.Content is ActionDefinition definition
                    && definition.IsRecoveryAtFrame(action.CurrentFrame));
            if (!reachedHandoff)
                return;

            BeginSwitchOutAfterCurrentAction();
            return;
        }

        if (_exitMode == ExitMode.PlayingSwitchOut
            && _switchOutActionInstanceId == 0
            && action.IsActive)
        {
            _switchOutActionInstanceId = action.InstanceId;
        }
    }

    /// <summary>当前图节点已是 AssistParrySuccess 时，连续接触不再创建新动作实例。</summary>
    bool IsPlayingAssistParrySuccess()
    {
        if (_actionSim == null || !_actionSim.IsActive)
            return false;

        ActionSimSnapshot snap = _actionSim.Snapshot;
        return snap.Graph is ActionGraph graph
            && graph.TryGetNode(snap.NodeId, out ActionGraphNode node)
            && node.Intent == GameplayIntentType.AssistParrySuccess;
    }

    /// <summary>终止已到交接点的原招，并为下一逻辑帧排队独立 SwitchOut。</summary>
    void BeginSwitchOutAfterCurrentAction()
    {
        StopActionForPartyTransition();
        _exitMode = ExitMode.PlayingSwitchOut;
        _switchOutActionInstanceId = 0;
        QueueExternalIntent(GameplayIntentType.SwitchOut);
    }

    /// <summary>切入 SwitchOut 或隐入后台前终止原招、缓冲并回到 Locomotion。</summary>
    void StopActionForPartyTransition()
    {
        if (_actionSim.IsActive)
            _actionSim.Stop();
        _intentBuffer.ClearAllBuffers();
        ReleaseHitOrActionForPartyTransition();
    }

    /// <summary>Driver 只从 Locomotion 起 SwitchOut，因此交接前必须离开 Hit/Action。</summary>
    void ReleaseHitOrActionForPartyTransition()
    {
        CharacterStateType state = _stateMachine.CurrentStateId;
        if (state == CharacterStateType.Action || state == CharacterStateType.Hit)
            _stateMachine.TryChangeState(CharacterStateType.Locomotion, force: true);
    }
}
