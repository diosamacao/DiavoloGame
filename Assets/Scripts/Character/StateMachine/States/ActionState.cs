public class ActionState : CharacterState
{
    public override CharacterStateType Id => CharacterStateType.Action;

    public override bool CanTransitionTo(CharacterStateType next)
    {
        if (next == CharacterStateType.Locomotion)
            return true;

        return base.CanTransitionTo(next);
    }

    public override void Enter()
    {
        Context.Animation.SetLocked(true);
    }

    public override void Exit()
    {
        Context.ActionRuntime?.Stop();
        Context.Animation.SetLocked(false);
        Context.Animation.ResetPlaybackState();
    }

    public override void Tick(float deltaTime)
    {
        IActionRuntime runtime = Context.ActionRuntime;
        if (runtime == null)
        {
            Context.StateMachine.TryChangeState(CharacterStateType.Locomotion);
            return;
        }

        runtime.Tick(deltaTime);

        if (!runtime.IsPlaying)
            Context.StateMachine.TryChangeState(CharacterStateType.Locomotion);
    }
}
