using System;
using UnityEngine;

/// <summary>单角色运行实例，集中持有输入、移动、状态、动作和战斗服务。</summary>
public sealed class CharacterActor :
    IDisposable,
    ISimulationActor,
    ISimulationInputParticipant,
    IRenderFrameSampler,
    ISimulationRenderable,
    ISimulationPostCombatActor
{
    readonly ILocalInputSampler _localInput;
    readonly InputManager _inputManager;
    readonly GameplayIntentProducer _intentProducer;
    readonly CharacterMotor _motor;
    readonly CharacterStateMachine _stateMachine;
    readonly CharacterActionDriver _actionDriver;
    readonly ActionExecutor _actionExecutor;
    readonly CombatModeService _combatMode;
    readonly CharacterAnimationService _animation;
    readonly CharacterPresentationBridge _presentation;
    InputFrameBuffer _inputFrames;
    SimActorId _actorId;
    long _currentFrameIndex = -1;

    /// <summary>角色输入中枢，玩法系统只读取量化逻辑帧。</summary>
    public InputManager Input => _inputManager;

    /// <summary>当前 SimulationWorld 分配的稳定身份；注册前为 Invalid。</summary>
    public SimActorId SimulationId => _actorId;

    /// <summary>本地相机使用的渲染帧 Look；AI 与回放 Actor 返回零。</summary>
    public Vector2 LookInput => _localInput?.LookInput ?? Vector2.zero;

    /// <summary>动画门面；供注册系统与卡肉使用。</summary>
    public CharacterAnimationService Animation => _animation;

    /// <summary>供相机与表现系统跟随的插值锚点。</summary>
    public Transform PresentationRoot => _presentation.PresentationRoot;

    /// <summary>当前渲染帧插值后的位置。</summary>
    public Vector3 RenderedPosition => _presentation.RenderedPosition;

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

    /// <summary>死亡动作是否已播放完成。</summary>
    public bool DeathPresentationComplete => _stateMachine.DeathPresentationComplete;

    /// <summary>创建角色实例；所有依赖由工厂一次性注入。</summary>
    public CharacterActor(
        ILocalInputSampler localInput,
        InputManager inputManager,
        GameplayIntentProducer intentProducer,
        CharacterMotor motor,
        CharacterStateMachine stateMachine,
        CharacterActionDriver actionDriver,
        ActionExecutor actionExecutor,
        CombatModeService combatMode,
        CharacterAnimationService animation,
        CharacterPresentationBridge presentation)
    {
        _localInput = localInput;
        _inputManager = inputManager;
        _intentProducer = intentProducer;
        _motor = motor;
        _stateMachine = stateMachine;
        _actionDriver = actionDriver;
        _actionExecutor = actionExecutor;
        _combatMode = combatMode;
        _animation = animation;
        _presentation = presentation;
    }

    /// <summary>启用本地设备采样；AI Actor 无设备源时为空操作。</summary>
    public void Enable() => _localInput?.Enable();

    /// <summary>禁用本地设备采样；AI Actor 无设备源时为空操作。</summary>
    public void Disable() => _localInput?.Disable();

    /// <summary>更新相机 Transform，用于相机相对移动。</summary>
    public void SetCameraTransform(Transform cameraTransform)
    {
        _motor.SetCameraTransform(cameraTransform);
    }

    /// <summary>执行上层已解析的受击请求并进入整数帧硬直；Actor 不负责选招。</summary>
    public void EnterHit(in CharacterReactionRequest request)
    {
        if (CurrentState == CharacterStateType.Death)
            return;

        ClearControlledInput();
        _stateMachine.EnterHit(in request);
    }

    /// <summary>执行上层已解析的死亡表现并进入不可逆死亡状态。</summary>
    public void EnterDeath(in CharacterReactionRequest request)
    {
        ClearControlledInput();
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
        _presentation.BeginSimulationStep();
        try
        {
            _inputManager.IngestFrame(inputFrame);
            _intentProducer.Step();
            _actionDriver.ProcessGameplayInput();
            _motor.TickGravity(fixedDeltaSeconds);
            // Action 时间只在此处单次推进；各角色 State 不再各自 Tick 执行器。
            _actionExecutor?.Step(fixedDeltaSeconds);
            _stateMachine.Tick(fixedDeltaSeconds);
            // 状态机内可能 Play 新 Clip，同帧末推进 CrossFade 权重。
            _animation.Tick(fixedDeltaSeconds);
        }
        finally
        {
            _presentation.EndSimulationStep();
        }
    }

    /// <summary>在整帧命中结算后处理 OnHitConfirm/OnWhiff 等自动衔接与自然结束。</summary>
    public void ResolvePostCombat(long frameIndex)
    {
        if (frameIndex != _currentFrameIndex)
            throw new InvalidOperationException("CharacterActor PostCombat 必须与最近 Step 属于同一逻辑帧。");

        _actionExecutor?.ResolvePostCombat();
        _stateMachine.ResolvePostCombat();
    }

    /// <summary>把前后逻辑 Pose 插值到模型表现锚点，不修改权威角色根。</summary>
    public void Render(float interpolationAlpha) => _presentation.Render(interpolationAlpha);

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
