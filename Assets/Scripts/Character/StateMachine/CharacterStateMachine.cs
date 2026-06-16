using System.Collections.Generic;
using UnityEngine;

public abstract class CharacterStateMachine : MonoBehaviour, ICharacterStateMachine
{
    readonly Dictionary<CharacterStateType, CharacterState> _states =
        new Dictionary<CharacterStateType, CharacterState>();

    CharacterState _currentState;
    CharacterStateType _currentStateType;

    protected CharacterContext Context { get; private set; }

    public CharacterStateType CurrentStateType => _currentStateType;

    protected virtual void Awake()
    {
        CharacterAnimationController animation = GetComponent<CharacterAnimationController>();
        Context = new CharacterContext(transform, animation, GetComponent<CharacterController>());
        Context.StateMachine = this;
        RegisterStates();
        ChangeState(CharacterStateType.Locomotion, force: true);
    }

    protected virtual void RegisterStates()
    {
        RegisterState(new LocomotionState());
        RegisterState(new ActionState());
    }

    protected void RegisterState(CharacterState state)
    {
        state.Bind(Context);
        _states[state.StateType] = state;
    }

    protected virtual void Update()
    {
        UpdateContext();
        _currentState?.Tick(Time.deltaTime);
    }

    protected abstract void UpdateContext();

    public bool TryChangeState(CharacterStateType next, bool force = false)
    {
        if (!force && _currentStateType == next)
            return false;

        if (!force && _currentState != null && !_currentState.CanTransitionTo(next))
            return false;

        ChangeState(next, force);
        return true;
    }

    void ChangeState(CharacterStateType next, bool force)
    {
        _currentState?.Exit();
        _currentStateType = next;
        _currentState = _states[next];
        _currentState.Enter();
    }
}
