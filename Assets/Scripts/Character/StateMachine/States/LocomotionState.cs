public class LocomotionState : CharacterState
{
    const float MoveInputThreshold = 0.01f;

    public override CharacterStateType Id => CharacterStateType.Locomotion;

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
