public interface IState<TStateId, TContext>
{
    TStateId Id { get; }

    void Bind(TContext context);

    void Enter();

    void Exit();

    void Tick(float deltaTime);

    bool CanTransitionTo(TStateId next);
}
