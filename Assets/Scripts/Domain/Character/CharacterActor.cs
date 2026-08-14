using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>单角色运行实例，集中持有输入、移动、状态、动作和战斗服务。</summary>
public sealed class CharacterActor :
    IDisposable,
    ISimulationActor,
    ISimulationInputParticipant,
    IRenderFrameSampler,
    ISimulationRenderable,
    ISimulationPostCombatActor,
    ISimSoftBodyParticipant,
    ILocalCameraTargetSource,
    ICharacterFacingDebugTarget
{
    readonly ILocalInputSampler _localInput;
    readonly InputManager _inputManager;
    readonly GameplayIntentProducer _intentProducer;
    readonly CharacterMotor _motor;
    readonly CharacterStateMachine _stateMachine;
    readonly CharacterActionDriver _actionDriver;
    readonly ActionSim _actionSim;
    readonly CharacterActionPresentationBridge _actionPresentation;
    readonly CombatModeService _combatMode;
    readonly CharacterAnimationService _animation;
    readonly CharacterPresentationBridge _presentation;
    readonly CharacterVisualMotionBridge _visualMotion;
    readonly NumericSystem _numeric;
    readonly CharacterVitality _vitality;
    readonly GameplayIntentBuffer _intentBuffer;
    readonly CharacterTargetingState _targetingState;
    readonly Transform _simulationRoot;
    InputFrameBuffer _inputFrames;
    InputFrame _lastSimulationInput;
    SimActorId _actorId;
    long _currentFrameIndex = -1;
    bool _wasActionActive;
    int _actionLateralPeakMm;
    int _prevMotorXMm;
    int _prevMotorZMm;
    bool _hasPrevMotorSample;

    static readonly GameplayIntentType[] EmptyIntents = Array.Empty<GameplayIntentType>();
    static readonly BufferedIntentDebug[] EmptyBuffers = Array.Empty<BufferedIntentDebug>();
    readonly BufferedIntentDebug[] _bufferDebugScratch = new BufferedIntentDebug[8];

    /// <summary>角色输入中枢，玩法系统只读取量化逻辑帧。</summary>
    public InputManager Input => _inputManager;

    /// <summary>当前 SimulationWorld 分配的稳定身份；注册前为 Invalid。</summary>
    public SimActorId SimulationId => _actorId;

    /// <summary>最近一次权威 Step 摄入的量化输入；未步进时为空帧。</summary>
    public InputFrame LastSimulationInput => _lastSimulationInput;

    /// <summary>本地相机使用的渲染帧 Look；AI 与回放 Actor 返回零。</summary>
    public Vector2 LookInput => _localInput?.LookInput ?? Vector2.zero;

    /// <summary>本渲染帧是否按下纯表现 CameraLock。</summary>
    public bool CameraLockPressedThisFrame =>
        _localInput != null && _localInput.CameraLockPressedThisFrame;

    /// <summary>动画门面；供注册系统与卡肉使用。</summary>
    public CharacterAnimationService Animation => _animation;

    /// <summary>当前角色的纯动作模拟核。</summary>
    public ActionSim ActionSim => _actionSim;

    /// <summary>水平逻辑电机；供 World 软弹开读写。</summary>
    public CharacterMotorSim MotorSim => _motor.Sim;

    /// <summary>死亡或软体抑制窗内不参与互撞软弹开。</summary>
    public bool ParticipatesInSoftBodySeparation =>
        CurrentState != CharacterStateType.Death
        && (_motor == null || !_motor.Sim.IsSoftBodySuppressed);

    /// <summary>供相机与表现系统跟随的插值锚点。</summary>
    public Transform PresentationRoot => _presentation.PresentationRoot;

    /// <summary>模型所在视觉根（含倾身等）；调试箭头用模型朝向时优先取此。</summary>
    public Transform VisualMotionRoot => _visualMotion?.VisualRoot;

    /// <summary>当前渲染帧插值后的位置。</summary>
    public Vector3 RenderedPosition => _presentation.RenderedPosition;

    /// <summary>
    /// 调试：逻辑帧缓存的镜头相对 wish（与 Motor/Locomotion 采样一致）。
    /// 勿在渲染帧重算，否则相机会跟朝向每帧改 PlanarBasis 导致黄箭抖动。
    /// </summary>
    public Vector3 DebugMoveWishWorldDirection
    {
        get
        {
            if (_motor == null || _inputManager == null || !_inputManager.HasMoveIntent)
                return Vector3.zero;
            return _motor.DebugWishWorldDirection;
        }
    }

    /// <inheritdoc />
    public bool HasFacingDebugPose => _presentation != null;

    /// <inheritdoc />
    public Vector3 FacingDebugFeetWorld => RenderedPosition;

    /// <inheritdoc />
    public Vector3 FacingDebugWishWorld => DebugMoveWishWorldDirection;

    /// <inheritdoc />
    public Vector3 FacingDebugModelForward
    {
        get
        {
            if (_visualMotion?.VisualRoot != null)
                return _visualMotion.VisualRoot.forward;
            if (_presentation?.PresentationRoot != null)
                return _presentation.PresentationRoot.forward;
            return _simulationRoot != null ? _simulationRoot.forward : Vector3.forward;
        }
    }

    /// <summary>当前移动输入幅度。</summary>
    public float MoveInputMagnitude => _motor.MoveInputMagnitude;

    /// <summary>当前跑步阈值。</summary>
    public float RunThreshold => _motor.RunThreshold;

    /// <summary>当前是否着地。</summary>
    public bool IsGrounded => _motor.IsGrounded;

    /// <summary>当前战斗模式服务。</summary>
    public ICombatModeService CombatMode => _combatMode;

    /// <summary>当前顶层角色状态，供 AI 感知与生命周期控制器读取。</summary>
    public CharacterStateType CurrentState => _stateMachine.CurrentStateId;

    /// <summary>当前 Sprint 视觉倾身（度）；非 Locomotion 为 0。不进复制快照。</summary>
    public float SprintLeanRollDegrees => _stateMachine.SprintLeanRollDegrees;

    /// <summary>死亡动作是否已播放完成。</summary>
    public bool DeathPresentationComplete => _stateMachine.DeathPresentationComplete;

    /// <summary>玩法意图缓冲（只读观测 / Debug HUD）。</summary>
    public GameplayIntentBuffer IntentBuffer => _intentBuffer;

    /// <summary>唯一 SelectedTarget 的只读快照。</summary>
    public CharacterTargetingSnapshot TargetingSnapshot => _targetingState.Snapshot;

    /// <summary>阵营 Id，与 Targeting 一致；复制快照直接填写。</summary>
    public int TeamId => _targetingState.TeamId;

    /// <summary>为 Camera/UI 把 SelectedTargetId 映射到只读表现目标。</summary>
    public bool TryGetSelectedTarget(out ITargetable target) =>
        _targetingState.TryGetSelectedTarget(out target);

    /// <summary>L-DIR3：Locomotion 是否处于 FaceTarget（仅 Profile 声明）；相机跟朝向应关闭。</summary>
    public bool IsLocomotionFaceTargetActive => _stateMachine.IsLocomotionFaceTargetActive;

    /// <summary>数值中枢（Attribute + Effect + Flags）。</summary>
    public NumericSystem Numeric => _numeric;

    /// <summary>Health 边沿（扣血 / Hit / Death 事件）。</summary>
    public CharacterVitality Vitality => _vitality;

    /// <summary>创建角色实例；所有依赖由工厂一次性注入。</summary>
    public CharacterActor(
        ILocalInputSampler localInput,
        InputManager inputManager,
        GameplayIntentProducer intentProducer,
        CharacterMotor motor,
        CharacterStateMachine stateMachine,
        CharacterActionDriver actionDriver,
        ActionSim actionSim,
        CharacterActionPresentationBridge actionPresentation,
        CombatModeService combatMode,
        CharacterAnimationService animation,
        CharacterPresentationBridge presentation,
        CharacterVisualMotionBridge visualMotion,
        NumericSystem numeric,
        CharacterVitality vitality,
        GameplayIntentBuffer intentBuffer,
        CharacterTargetingState targetingState,
        Transform simulationRoot)
    {
        _localInput = localInput;
        _inputManager = inputManager;
        _intentProducer = intentProducer;
        _motor = motor;
        _stateMachine = stateMachine;
        _actionDriver = actionDriver;
        _actionSim = actionSim;
        _actionPresentation = actionPresentation;
        _combatMode = combatMode;
        _animation = animation;
        _presentation = presentation;
        _visualMotion = visualMotion;
        _numeric = numeric;
        _vitality = vitality;
        _intentBuffer = intentBuffer;
        _targetingState = targetingState ?? throw new ArgumentNullException(nameof(targetingState));
        _simulationRoot = simulationRoot;
    }

    /// <summary>组装只读调试快照；供 CombatDebugHudController LateUpdate 采样。</summary>
    public CharacterDebugSnapshot BuildDebugSnapshot()
    {
        ActionSimSnapshot snap = _actionSim != null ? _actionSim.Snapshot : default;
        string actionName = string.Empty;
        int totalFrames = 0;
        if (snap.IsActive && snap.Content != null)
        {
            totalFrames = snap.Content.TotalFrames;
            if (snap.Content is UnityEngine.Object unityContent)
                actionName = unityContent.name;
        }

        GameplayIntentType[] frameIntents = EmptyIntents;
        if (_intentBuffer != null && _intentBuffer.FrameIntents.Count > 0)
        {
            frameIntents = new GameplayIntentType[_intentBuffer.FrameIntents.Count];
            for (int i = 0; i < frameIntents.Length; i++)
                frameIntents[i] = _intentBuffer.FrameIntents[i];
        }

        BufferedIntentDebug[] buffers = EmptyBuffers;
        if (_intentBuffer != null)
        {
            int count = _intentBuffer.CopyBufferedForDebug(_bufferDebugScratch);
            if (count > 0)
            {
                buffers = new BufferedIntentDebug[count];
                Array.Copy(_bufferDebugScratch, buffers, count);
            }
        }

        bool hasSelectedTarget = _targetingState.TryGetSelectedTarget(out ITargetable selectedTarget);
        string selectedTargetName = string.Empty;
        float selectedTargetDistance = 0f;
        if (hasSelectedTarget && selectedTarget?.AimTransform != null && _simulationRoot != null)
        {
            selectedTargetName = selectedTarget.AimTransform.name;
            Vector3 delta = selectedTarget.AimTransform.position - _simulationRoot.position;
            delta.y = 0f;
            selectedTargetDistance = delta.magnitude;
        }

        CharacterMotorSim motor = _motor.Sim;
        // HUD 只读 Numeric：属性点 + Flags + ActiveEffects
        NumericDebugSnapshot numericSnap = _numeric.BuildDebugSnapshot();
        AttributeSet attrs = _numeric.Attributes;
        CombatContextFlags flags = _numeric.Flags;
        return new CharacterDebugSnapshot(
            CurrentState,
            snap.IsActive,
            actionName,
            snap.CurrentFrame,
            totalFrames,
            snap.FreezeFrames,
            _vitality.CurrentHealth,
            _vitality.MaxHealth,
            attrs.GetPoints(AttributeId.Energy),
            attrs.GetPoints(AttributeId.MaxEnergy),
            attrs.GetCurrent(AttributeId.EnergyRegenMilliPerFrame),
            attrs.GetPoints(AttributeId.Decibel),
            attrs.GetPoints(AttributeId.MaxDecibel),
            attrs.GetPoints(AttributeId.DodgeCharges),
            attrs.GetPoints(AttributeId.MaxDodgeCharges),
            flags.DodgeRechargeFramesLeft,
            flags.IsInCombat,
            flags.InCombatHoldFrames,
            numericSnap.PerfectDodgeCounterFrames,
            attrs.GetPoints(AttributeId.Attack),
            attrs.GetPoints(AttributeId.Defense),
            numericSnap.OutgoingDamageMultMilli,
            numericSnap.IncomingDamageMultMilli,
            numericSnap.Effects,
            ResolveNextSpecialFormLabel(),
            hasSelectedTarget,
            selectedTargetName,
            selectedTargetDistance,
            motor.PositionMm.X,
            motor.PositionMm.Z,
            motor.YMm,
            motor.FacingMilliDeg,
            motor.SoftBodyMass,
            motor.SoftBodyImmovable,
            _actionLateralPeakMm,
            frameIntents,
            buffers);
    }

    /// <summary>HUD：反击缓冲优先显示 Counter；否则预判 Special 同键 EX/普通。</summary>
    string ResolveNextSpecialFormLabel()
    {
        if (_numeric != null && _numeric.Flags.HasPerfectDodgeCounter)
            return "Counter";

        ActionGraph graph = _combatMode?.ActiveGraph;
        if (graph == null || _actionSim == null)
            return "-";

        var entries = new List<ActionDefinition>(4);
        IReadOnlyList<ActionGraphNode> nodes = graph.Nodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            ActionGraphNode node = nodes[i];
            if (node == null || !node.IsEntry || node.Action == null)
                continue;
            if (node.Intent != GameplayIntentType.Special)
                continue;
            entries.Add(node.Action);
        }

        if (entries.Count == 0)
            return "-";

        bool isEx = ActionEnergyFormSelector.WouldSelectExSpecial(
            entries.Count,
            i => entries[i].ResourceSpec?.ResourceTag ?? ActionResourceTag.None,
            i => _actionSim.CanAffordContent(entries[i]));
        return isEx ? "EX" : "Special";
    }

    /// <summary>启用本地设备采样；AI Actor 无设备源时为空操作。</summary>
    public void Enable() => _localInput?.Enable();

    /// <summary>禁用本地设备采样；AI Actor 无设备源时为空操作。</summary>
    public void Disable() => _localInput?.Disable();

    /// <summary>暂存本地 Orbit yaw；下一次渲染采样将其固化进 InputFrame。</summary>
    public void StageMoveReferenceYaw(float yawDegrees) =>
        _localInput?.StageMoveReferenceYaw(yawDegrees);

    /// <summary>执行上层已解析的受击请求并进入整数帧硬直；Actor 不负责选招。</summary>
    public void EnterHit(in CharacterReactionRequest request)
    {
        if (CurrentState == CharacterStateType.Death)
            return;

        ClearControlledInput();
        // 受击打断时模型短时回锚（若动作 Stop 事件未到也兜底）
        _visualMotion?.EndAction(VisualResidualExitPolicy.BlendToZero);
        _visualMotion?.SetLeanRollDegrees(0f);
        _stateMachine.EnterHit(in request);
    }

    /// <summary>执行上层已解析的死亡表现并进入不可逆死亡状态。</summary>
    public void EnterDeath(in CharacterReactionRequest request)
    {
        ClearControlledInput();
        SnapVisualResidual();
        _visualMotion?.SetLeanRollDegrees(0f);
        _stateMachine.EnterDeath(in request);
    }

    /// <summary>注册时绑定稳定 ActorId 与 World 输入历史。</summary>
    public void BindSimulationInput(SimActorId actorId, InputFrameBuffer inputFrames)
    {
        if (!actorId.IsValid)
            throw new ArgumentException("CharacterActor 必须绑定有效 SimActorId。", nameof(actorId));

        _actorId = actorId;
        _inputFrames = inputFrames ?? throw new ArgumentNullException(nameof(inputFrames));
    }

    /// <summary>把本地设备样本合并进下一逻辑帧；AI/回放 Actor 不执行设备采样。</summary>
    public void SampleRenderFrame(long targetFrame)
    {
        if (_localInput == null || _inputFrames == null || !_actorId.IsValid)
            return;

        InputFrame sample = _localInput.Sample(targetFrame, _actorId);
        _inputFrames.MergeLocalSample(in sample);
    }

    /// <summary>由 SimulationWorld 按固定顺序推进输入、动作路由、重力、状态机与动画淡入。</summary>
    public void Step(long frameIndex, float fixedDeltaSeconds, in InputFrame inputFrame)
    {
        _currentFrameIndex = frameIndex;
        _lastSimulationInput = inputFrame;
        _presentation.BeginSimulationStep();
        // 逻辑步内残差贴帧，避免挂点读到上一渲染插值
        _visualMotion?.ApplyLogicLocalPose();
        try
        {
            // 软体抑制倒计时：须在本帧 ApplyStep 置位之前递减
            _motor?.Sim.TickSoftBodySuppress();
            _inputManager.IngestFrame(inputFrame);
            // Targeting 必须先于 Action 路由/推进，使同帧切敌立即作用于尚未解析的动作逻辑。
            _targetingState.Step(_actorId, _motor.Sim, in inputFrame);
            _intentProducer.Step();
            _actionDriver.ProcessGameplayInput();
            // ActionSim 是唯一动作推进路径；表现桥只消费本步产生的只读事件。
            _actionSim?.Step();
            _actionPresentation?.ApplyStep(fixedDeltaSeconds);
            _motor.TickGravity(fixedDeltaSeconds);
            _stateMachine.Tick(fixedDeltaSeconds);
            // L-DIR4：倾身只写 VisualMotionRoot，不改 Motor/Sim 权威朝向
            _visualMotion?.SetLeanRollDegrees(_stateMachine.SprintLeanRollDegrees);
            // Manual Playable：同帧末推进时间与 CrossFade。
            // 未烘焙招式仍可能由此 Evaluate 产生 Native RM delta；已烘焙招式 RM 在 ApplyStep 已关闭。
            _animation.Tick(fixedDeltaSeconds);
            UpdateActionLateralPeakSample();

            // 卡肉期间暂停 Numeric.Step（被动回能/充能/Effect/旗标）；动作或受击态刷新接战门闩
            if (_actionSim != null && !_actionSim.IsFrozen)
            {
                if (_actionSim.IsActive
                    || CurrentState == CharacterStateType.Hit
                    || CurrentState == CharacterStateType.Action)
                {
                    _numeric.NotifyInCombat();
                }

                _numeric.Step();
            }
        }
        finally
        {
            _presentation.EndSimulationStep();
            _visualMotion?.ApplyLogicLocalPose();
        }
    }

    /// <summary>
    /// Wave 0：记录招式会话内 Motor 世界位移在角色右向的峰峰值，用于对照横摆是否进了逻辑根。
    /// </summary>
    void UpdateActionLateralPeakSample()
    {
        bool active = _actionSim != null && _actionSim.IsActive;
        CharacterMotorSim motor = _motor.Sim;
        if (active && !_wasActionActive)
        {
            _actionLateralPeakMm = 0;
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
            if (lateralMm > _actionLateralPeakMm)
                _actionLateralPeakMm = lateralMm;
        }

        _prevMotorXMm = motor.PositionMm.X;
        _prevMotorZMm = motor.PositionMm.Z;
        _hasPrevMotorSample = true;
        _wasActionActive = active;
    }

    /// <summary>在整帧命中结算后处理 OnHitConfirm/OnWhiff 等自动衔接与自然结束。</summary>
    public void ResolvePostCombat(long frameIndex)
    {
        if (frameIndex != _currentFrameIndex)
            throw new InvalidOperationException("CharacterActor PostCombat 必须与最近 Step 属于同一逻辑帧。");

        _actionSim?.ResolvePostCombat();
        _stateMachine.ResolvePostCombat();
        _actionPresentation?.ApplyPostCombat();
    }

    /// <summary>软弹开提交后同步 Transform，并刷新本帧表现终点 Pose。</summary>
    public void OnSoftBodySeparationApplied()
    {
        _motor.SyncRootPlanarFromSim();
        _presentation.RefreshCurrentPoseFromSimulationRoot();
    }

    /// <summary>把前后逻辑 Pose 插值到表现锚点，再插值视觉残差到模型根。</summary>
    public void Render(float interpolationAlpha)
    {
        _presentation.Render(interpolationAlpha);
        // BlendOut 跟渲染帧走，避免逻辑 60Hz 与显示帧率脱节
        _visualMotion?.Render(interpolationAlpha, Time.deltaTime);
    }

    /// <summary>死亡/传送时立刻清掉视觉残差，避免模型停在偏移。</summary>
    public void SnapVisualResidual() =>
        _visualMotion?.EndAction(VisualResidualExitPolicy.SnapToZero);

    /// <summary>释放动画 PlayableGraph 等资源。</summary>
    public void Dispose() => _animation?.Dispose();

    /// <summary>受击与死亡优先级高于输入，必须同步清掉连续量和动作缓冲。</summary>
    void ClearControlledInput()
    {
        _inputManager.IngestFrame(InputFrame.Empty(
            Math.Max(0, _currentFrameIndex),
            _actorId));
        _inputManager.ClearBufferedMoveIntent();
        _actionDriver.ClearPendingActions();
    }
}
