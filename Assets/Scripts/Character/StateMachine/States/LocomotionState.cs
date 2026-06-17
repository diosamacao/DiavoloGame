public class LocomotionState : CharacterState
{
    const float MoveInputThreshold = 0.01f;

    public override CharacterStateType Id => CharacterStateType.Locomotion;

    public override bool CanTransitionTo(CharacterStateType next)
    {
        if (next == CharacterStateType.Action)
            return true;

        return base.CanTransitionTo(next);
    }

    public override void Tick(float deltaTime)
    {
        AnimationKey target = ResolveLocomotionKey();
        Context.Animation.Play(target);
    }

    AnimationKey ResolveLocomotionKey()
    {
        if (Context.MoveInputMagnitude < MoveInputThreshold)
            return AnimationKey.Idle;

        if (Context.MoveInputMagnitude <= Context.RunThreshold)
            return AnimationKey.Walk;

        return AnimationKey.Run;
    }
}
