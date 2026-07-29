/// <summary>角色状态机；不挂载到 GameObject，由 CharacterActor 持有并 Tick。</summary>
public sealed class CharacterStateMachine : ICharacterStateMachine
{
    readonly StateMachine<CharacterStateType, CharacterContext> _machine = new();

    CharacterContext Context => _machine.Context;

    public CharacterStateType CurrentStateId => _machine.CurrentStateId;

    /// <summary>创建角色状态机并注册 Locomotion、Action、Hit 与 Death 状态。</summary>
    public CharacterStateMachine(CharacterContext context)
    {
        context.StateMachine = this;
        RegisterStates();
        _machine.Initialize(context, CharacterStateType.Locomotion);
    }

    void RegisterStates()
    {
        RegisterState(new LocomotionState());
        RegisterState(new ActionState());
        RegisterState(new HitState());
        RegisterState(new DeathState());
    }

    void RegisterState(CharacterState state) => _machine.RegisterState(state);

    /// <summary>推进当前状态。</summary>
    public void Tick(float deltaTime)
    {
        _machine.Tick(deltaTime);
    }

    public bool TryChangeState(CharacterStateType next, bool force = false) =>
        _machine.TryChangeState(next, force);

    /// <summary>死亡表现是否已经播放完成。</summary>
    public bool DeathPresentationComplete => Context.DeathPresentationComplete;

    /// <summary>强制进入或重入受击状态，并覆盖上一条反应请求。</summary>
    public void EnterHit(in CharacterReactionRequest request)
    {
        Context.SetReactionRequest(in request);
        _machine.TryChangeState(CharacterStateType.Hit, force: true);
    }

    /// <summary>写入死亡表现并强制进入不可逆 Death 状态。</summary>
    public void EnterDeath(in CharacterReactionRequest request)
    {
        if (CurrentStateId == CharacterStateType.Death)
            return;

        Context.SetReactionRequest(in request);
        _machine.TryChangeState(CharacterStateType.Death, force: true);
    }

    /// <summary>由 Motor 层每帧 Push Locomotion 快照；在 Tick 之前调用，替代子类拉取 PlayerController。</summary>
    public void PushMotorSnapshot(float moveInputMagnitude, float runThreshold, bool isGrounded)
    {
        Context.MoveInputMagnitude = moveInputMagnitude;
        Context.RunThreshold = runThreshold;
        Context.IsGrounded = isGrounded;
    }
}
