/// <summary>角色状态机运行时；不挂载到 GameObject，由 PlayerCharacterRuntime 持有并 Tick。</summary>
public sealed class CharacterStateMachine : ICharacterStateMachine
{
    readonly StateMachine<CharacterStateType, CharacterContext> _machine = new();

    CharacterContext Context => _machine.Context;

    public CharacterStateType CurrentStateId => _machine.CurrentStateId;

    /// <summary>创建角色状态机并注册基础 Locomotion / Action 状态。</summary>
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
    }

    void RegisterState(CharacterState state) => _machine.RegisterState(state);

    /// <summary>推进当前状态。</summary>
    public void Tick(float deltaTime)
    {
        _machine.Tick(deltaTime);
    }

    public bool TryChangeState(CharacterStateType next, bool force = false) =>
        _machine.TryChangeState(next, force);

    /// <summary>由 Motor 层每帧 Push Locomotion 快照；在 Tick 之前调用，替代子类拉取 PlayerController。</summary>
    public void PushMotorSnapshot(float moveInputMagnitude, float runThreshold, bool isGrounded)
    {
        Context.MoveInputMagnitude = moveInputMagnitude;
        Context.RunThreshold = runThreshold;
        Context.IsGrounded = isGrounded;
    }
}
