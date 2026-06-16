public abstract class CharacterState : StateBase<CharacterStateType, CharacterContext>
{
    public CharacterStateType StateType => Id;
}
