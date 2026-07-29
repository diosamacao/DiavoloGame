using UnityEngine;

/// <summary>单角色运行实例，集中持有输入、移动、状态、动作和战斗服务。</summary>
public sealed class CharacterActor : System.IDisposable
{
    readonly ICharacterInputSource _inputSource;
    readonly InputManager _inputManager;
    readonly GameplayIntentProducer _intentProducer;
    readonly CharacterMotor _motor;
    readonly CharacterStateMachine _stateMachine;
    readonly CharacterActionDriver _actionDriver;
    readonly CombatModeService _combatMode;
    readonly CharacterAnimationService _animation;

    /// <summary>角色输入中枢，玩家相机等系统可读取 LookIntent。</summary>
    public InputManager Input => _inputManager;

    /// <summary>动画门面；供注册系统与卡肉使用。</summary>
    public CharacterAnimationService Animation => _animation;

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
        ICharacterInputSource inputSource,
        InputManager inputManager,
        GameplayIntentProducer intentProducer,
        CharacterMotor motor,
        CharacterStateMachine stateMachine,
        CharacterActionDriver actionDriver,
        CombatModeService combatMode,
        CharacterAnimationService animation)
    {
        _inputSource = inputSource;
        _inputManager = inputManager;
        _intentProducer = intentProducer;
        _motor = motor;
        _stateMachine = stateMachine;
        _actionDriver = actionDriver;
        _combatMode = combatMode;
        _animation = animation;
    }

    /// <summary>启用输入源。</summary>
    public void Enable() => _inputSource.Enable();

    /// <summary>禁用输入源。</summary>
    public void Disable() => _inputSource.Disable();

    /// <summary>更新相机 Transform，用于相机相对移动。</summary>
    public void SetCameraTransform(Transform cameraTransform)
    {
        _motor.SetCameraTransform(cameraTransform);
    }

    /// <summary>中断当前行为并进入受击硬直；可选 Action 只作为表现，不参与选招。</summary>
    public void EnterHit(float durationSeconds, ActionDefinition hitAction = null)
    {
        if (CurrentState == CharacterStateType.Death)
            return;

        ClearControlledInput();
        var request = new CharacterReactionRequest(durationSeconds, hitAction);
        _stateMachine.EnterHit(in request);
    }

    /// <summary>中断当前行为并进入不可逆死亡状态。</summary>
    public void EnterDeath(ActionDefinition deathAction = null)
    {
        ClearControlledInput();
        var request = new CharacterReactionRequest(0f, deathAction);
        _stateMachine.EnterDeath(in request);
    }

    /// <summary>按固定顺序推进输入、动作路由、重力、状态机与动画淡入。</summary>
    public void Tick(float deltaTime)
    {
        _inputManager.IngestFrame(_inputSource.CaptureFrame());
        _intentProducer.Tick(deltaTime);
        _actionDriver.ProcessGameplayInput();
        _motor.TickGravity(deltaTime);
        _stateMachine.Tick(deltaTime);
        // 状态机内可能 Play 新 Clip，同帧末推进 CrossFade 权重。
        _animation.Tick(deltaTime);
    }

    /// <summary>释放动画 PlayableGraph 等资源。</summary>
    public void Dispose() => _animation?.Dispose();

    /// <summary>受击与死亡优先级高于输入，必须同步清掉连续量和动作缓冲。</summary>
    void ClearControlledInput()
    {
        _inputManager.IngestFrame(PlayerInputFrame.Empty);
        _inputManager.ClearBufferedMoveIntent();
        _actionDriver.ClearPendingActions();
    }
}
