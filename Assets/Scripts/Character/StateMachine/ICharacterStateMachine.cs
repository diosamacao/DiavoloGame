public interface ICharacterStateMachine
{
    CharacterStateType CurrentStateType { get; }
    bool TryChangeState(CharacterStateType next, bool force = false);
}
