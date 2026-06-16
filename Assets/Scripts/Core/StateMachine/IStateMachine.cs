public interface IStateMachine<TStateId>
    where TStateId : struct, System.Enum
{
    TStateId CurrentStateId { get; }

    bool TryChangeState(TStateId next, bool force = false);
}
