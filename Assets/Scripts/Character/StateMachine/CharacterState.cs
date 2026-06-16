public abstract class CharacterState
{
    protected CharacterContext Context;

    public void Bind(CharacterContext context) => Context = context;

    public virtual void Enter() { }

    public virtual void Exit() { }

    public virtual void Tick(float deltaTime) { }

    public virtual bool CanTransitionTo(CharacterStateType next)
    {
        if (next == StateType)
            return false;

        return (int)next > (int)StateType;
    }

    public abstract CharacterStateType StateType { get; }
}
