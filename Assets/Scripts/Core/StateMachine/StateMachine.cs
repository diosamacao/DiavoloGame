using System;
using System.Collections.Generic;

public class StateMachine<TStateId, TContext>
    where TStateId : struct, Enum
{
    readonly Dictionary<TStateId, IState<TStateId, TContext>> _states =
        new Dictionary<TStateId, IState<TStateId, TContext>>();

    TContext _context;
    IState<TStateId, TContext> _currentState;
    TStateId _currentStateId;

    public TContext Context => _context;

    public TStateId CurrentStateId => _currentStateId;

    public void RegisterState(IState<TStateId, TContext> state) => _states[state.Id] = state;

    public void Initialize(TContext context, TStateId initialState)
    {
        _context = context;

        foreach (IState<TStateId, TContext> state in _states.Values)
            state.Bind(_context);

        ChangeState(initialState, force: true);
    }

    public void Tick(float deltaTime) => _currentState?.Tick(deltaTime);

    public bool TryChangeState(TStateId next, bool force = false)
    {
        if (!force && _currentStateId.Equals(next))
            return false;

        if (!force && _currentState != null && !_currentState.CanTransitionTo(next))
            return false;

        ChangeState(next, force);
        return true;
    }

    void ChangeState(TStateId next, bool force)
    {
        _currentState?.Exit();
        _currentStateId = next;
        _currentState = _states[next];
        _currentState.Enter();
    }
}
