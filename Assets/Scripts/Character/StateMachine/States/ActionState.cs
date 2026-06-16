public class ActionState : CharacterState
{
    public override CharacterStateType StateType => CharacterStateType.Action;

    public override void Enter()
    {
        Context.Animation.SetLocked(true);
    }

    public override void Exit()
    {
        Context.Animation.SetLocked(false);
        Context.Animation.ResetPlaybackState();
    }

    public override void Tick(float deltaTime)
    {
        // Reserved for ActionRuntimeController frame updates.
    }
}
