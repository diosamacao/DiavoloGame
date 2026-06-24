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
        Context.Movement.TickLocomotion(deltaTime);
        SyncMotorSnapshot();
        AnimationKey target = ResolveLocomotionKey();
        Context.Animation.Play(target);
    }

    /// <summary>Locomotion 移动后同步当前帧快照，供动画选择读取。</summary>
    void SyncMotorSnapshot()
    {
        Context.MoveInputMagnitude = Context.Movement.MoveInputMagnitude;
        Context.RunThreshold = Context.Movement.RunThreshold;
        Context.IsGrounded = Context.Movement.IsGrounded;
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
