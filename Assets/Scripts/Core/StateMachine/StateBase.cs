using System;

public abstract class StateBase<TStateId, TContext> : IState<TStateId, TContext>
    where TStateId : struct, Enum
{
    protected TContext Context { get; private set; }

    public abstract TStateId Id { get; }

    public virtual void Bind(TContext context) => Context = context;

    public virtual void Enter() { }

    public virtual void Exit() { }

    public virtual void Tick(float deltaTime) { }

    public virtual bool CanTransitionTo(TStateId next)
    {
        if (Id.Equals(next))
            return false;

        return Convert.ToInt32(next) > Convert.ToInt32(Id);
    }
}
