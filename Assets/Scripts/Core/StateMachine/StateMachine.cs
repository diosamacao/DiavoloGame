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

    /// <summary>
    /// 纠偏恢复到指定态，不走 Exit/Enter（避免 Idle.Enter 清掉 Sprint 计时）。
    /// 调用方须已写好 Context 并自行 Play/Seek Clip。
    /// </summary>
    public void RestoreCurrent(TContext context, TStateId stateId)
    {
        _context = context;
        foreach (IState<TStateId, TContext> state in _states.Values)
            state.Bind(_context);

        if (!_states.TryGetValue(stateId, out IState<TStateId, TContext> next))
            throw new InvalidOperationException($"StateMachine: 未注册状态 {stateId}。");

        _currentStateId = stateId;
        _currentState = next;
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
