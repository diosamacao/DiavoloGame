using UnityEngine;

public abstract class CharacterStateMachine : MonoBehaviour, ICharacterStateMachine
{
    readonly StateMachine<CharacterStateType, CharacterContext> _machine = new();

    protected CharacterContext Context => _machine.Context;

    public CharacterStateType CurrentStateId => _machine.CurrentStateId;

    public CharacterStateType CurrentStateType => CurrentStateId;

    protected virtual void Awake()
    {
        CharacterAnimationController animation = GetComponent<CharacterAnimationController>();
        CharacterContext context = new CharacterContext(
            transform,
            animation,
            GetComponent<CharacterController>());

        context.StateMachine = this;
        ConfigureContext(context);
        RegisterStates();
        _machine.Initialize(context, CharacterStateType.Locomotion);
    }

    protected virtual void RegisterStates()
    {
        RegisterState(new LocomotionState());
        RegisterState(new ActionState());
    }

    protected void RegisterState(CharacterState state) => _machine.RegisterState(state);

    protected virtual void Update()
    {
        UpdateContext();
        _machine.Tick(Time.deltaTime);
    }

    protected abstract void UpdateContext();

    protected virtual void ConfigureContext(CharacterContext context) { }

    public bool TryChangeState(CharacterStateType next, bool force = false) =>
        _machine.TryChangeState(next, force);
}
