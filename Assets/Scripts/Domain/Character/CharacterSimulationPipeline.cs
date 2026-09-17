using System;
using UnityEngine;

/// <summary>独占角色固定帧 Step/PostCombat 执行顺序及其逐帧观测状态。</summary>
internal sealed class CharacterSimulationPipeline
{
    readonly InputManager _inputManager;
    readonly GameplayIntentProducer _intentProducer;
    readonly CharacterMotor _motor;
    readonly CharacterStateMachine _stateMachine;
    readonly CharacterActionDriver _actionDriver;
    readonly ActionSim _actionSim;
    readonly CharacterActionPresentationBridge _actionPresentation;
    readonly CharacterAnimationService _animation;
    readonly CharacterPresentationBridge _presentation;
    readonly CharacterVisualMotionBridge _visualMotion;
    readonly NumericSystem _numeric;
    readonly CharacterVitality _vitality;
    readonly CharacterTargetingState _targetingState;
    readonly Transform _simulationRoot;
    readonly Action _emitExternalIntent;
    readonly Action _postCombatCompleted;
    bool _wasActionActive;
    int _prevMotorXMm;
    int _prevMotorZMm;
    bool _hasPrevMotorSample;

    /// <summary>最近完成 Step 的逻辑帧；未步进时为 -1。</summary>
    public long CurrentFrameIndex { get; private set; } = -1;

    /// <summary>最近一次 Step 实际摄入的输入；后台或死亡角色为空帧。</summary>
    public InputFrame LastSimulationInput { get; private set; }

    /// <summary>当前招式会话内相对角色右向的最大单帧水平位移。</summary>
    public int ActionLateralPeakMm { get; private set; }

    /// <summary>注入固定帧所需子系统；调用顺序仅由本对象维护。</summary>
    public CharacterSimulationPipeline(
        InputManager inputManager,
        GameplayIntentProducer intentProducer,
        CharacterMotor motor,
        CharacterStateMachine stateMachine,
        CharacterActionDriver actionDriver,
        ActionSim actionSim,
        CharacterActionPresentationBridge actionPresentation,
        CharacterAnimationService animation,
        CharacterPresentationBridge presentation,
        CharacterVisualMotionBridge visualMotion,
        NumericSystem numeric,
        CharacterVitality vitality,
        CharacterTargetingState targetingState,
        Transform simulationRoot,
        Action emitExternalIntent,
        Action postCombatCompleted)
    {
        _inputManager = inputManager;
        _intentProducer = intentProducer;
        _motor = motor;
        _stateMachine = stateMachine;
        _actionDriver = actionDriver;
        _actionSim = actionSim;
        _actionPresentation = actionPresentation;
        _animation = animation;
        _presentation = presentation;
        _visualMotion = visualMotion;
        _numeric = numeric;
        _vitality = vitality;
        _targetingState = targetingState;
        _simulationRoot = simulationRoot;
        _emitExternalIntent = emitExternalIntent
            ?? throw new ArgumentNullException(nameof(emitExternalIntent));
        _postCombatCompleted = postCombatCompleted;
    }

    /// <summary>按既定顺序推进一次角色固定帧；不得改用 Unity 渲染帧时间。</summary>
    public void Step(
        long frameIndex,
        float fixedDeltaSeconds,
        in InputFrame inputFrame,
        SimActorId actorId,
        PartyMemberState partyState)
    {
        InputFrame effectiveInput = partyState == PartyMemberState.Active
            && _stateMachine.CurrentStateId != CharacterStateType.Death
            ? inputFrame
            : InputFrame.Empty(frameIndex, actorId);
        CurrentFrameIndex = frameIndex;
        LastSimulationInput = effectiveInput;
        _vitality?.ClearReplicationEdge();
        // Step 期间锁住表现锚点，防止渲染层读到逻辑位移的半帧状态。
        _presentation.BeginSimulationStep();
        _visualMotion?.ApplyLogicLocalPose();
        try
        {
            // 抑制倒计时必须先于本帧位移；Targeting 必须先于尚未解析的动作输入。
            _motor?.Sim.TickSoftBodySuppress();
            _inputManager.IngestFrame(effectiveInput);
            _targetingState.Step(actorId, _motor.Sim, in effectiveInput);
            _intentProducer.Step();
            // Producer 会先清当帧意图，所以协调器意图只能在其后注入。
            _emitExternalIntent();

            StepActionClock();
            _actionPresentation?.ApplyStep(fixedDeltaSeconds);
            _motor.TickGravity(fixedDeltaSeconds);
            _stateMachine.Tick(fixedDeltaSeconds);
            _visualMotion?.SetLeanRollDegrees(_stateMachine.SprintLeanRollDegrees);
            // Manual Playable 与本逻辑帧同末点推进，不能改为渲染帧 Update。
            _animation?.Tick(fixedDeltaSeconds);
            UpdateActionLateralPeakSample();

            // Freeze 同时冻结动作和数值时钟，避免卡肉期间 Effect/资源暗中递减。
            if (_actionSim != null && !_actionSim.IsFrozen)
            {
                if (_actionSim.IsActive
                    || _stateMachine.CurrentStateId == CharacterStateType.Hit
                    || _stateMachine.CurrentStateId == CharacterStateType.Action)
                {
                    _numeric.NotifyInCombat();
                }

                _numeric.Step();
            }
        }
        finally
        {
            // 即使子系统抛错也必须释放锚点锁，避免后续渲染永久停在旧 Pose。
            _presentation.EndSimulationStep();
            _visualMotion?.ApplyLogicLocalPose();
        }
    }

    /// <summary>在同帧命中结算后按固定顺序处理动作、状态、表现与角色生命周期。</summary>
    public void ResolvePostCombat(long frameIndex)
    {
        if (frameIndex != CurrentFrameIndex)
        {
            throw new InvalidOperationException(
                "CharacterActor PostCombat 必须与最近 Step 属于同一逻辑帧。");
        }

        _actionSim?.ResolvePostCombat();
        _stateMachine.ResolvePostCombat();
        _actionPresentation?.ApplyPostCombat();
        _postCombatCompleted?.Invoke();
    }

    /// <summary>消费动作意图并推进 ActionSim 整数帧时钟。</summary>
    void StepActionClock()
    {
        _actionDriver.ProcessGameplayInput();
        _actionSim?.Step();
    }

    /// <summary>记录招式期间逻辑根横向位移峰值，供调试快照对照 Root Motion。</summary>
    void UpdateActionLateralPeakSample()
    {
        bool active = _actionSim != null && _actionSim.IsActive;
        CharacterMotorSim motor = _motor.Sim;
        if (active && !_wasActionActive)
        {
            ActionLateralPeakMm = 0;
            _hasPrevMotorSample = false;
        }

        if (active && _hasPrevMotorSample && _simulationRoot != null)
        {
            int dx = motor.PositionMm.X - _prevMotorXMm;
            int dz = motor.PositionMm.Z - _prevMotorZMm;
            Vector3 worldDelta = new(
                MotionQuantization.MmToMeters(dx),
                0f,
                MotionQuantization.MmToMeters(dz));
            float lateralMeters = Vector3.Dot(worldDelta, _simulationRoot.right);
            int lateralMm = Mathf.Abs(MotionQuantization.MetersToMm(lateralMeters));
            if (lateralMm > ActionLateralPeakMm)
                ActionLateralPeakMm = lateralMm;
        }

        _prevMotorXMm = motor.PositionMm.X;
        _prevMotorZMm = motor.PositionMm.Z;
        _hasPrevMotorSample = true;
        _wasActionActive = active;
    }
}
