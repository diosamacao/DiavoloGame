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
        Context.Movement.ClearMoveSnapshot();
        SyncMotorSnapshot();
        if (!AdvanceActionRuntime(deltaTime))
        {
            Context.StateMachine.TryChangeState(CharacterStateType.Locomotion);
            return;
        }

        Context.ActionRotation?.Tick();
    }

    /// <summary>Action 状态清空 Locomotion 输入快照，避免动作中播放移动动画。</summary>
    void SyncMotorSnapshot()
    {
        Context.MoveInputMagnitude = Context.Movement.MoveInputMagnitude;
        Context.RunThreshold = Context.Movement.RunThreshold;
        Context.IsGrounded = Context.Movement.IsGrounded;
    }

    bool AdvanceActionRuntime(float deltaTime)
    {
        IActionRuntime runtime = Context.ActionRuntime;
        if (runtime == null)
            return false;

        runtime.Tick(deltaTime);
        return runtime.IsPlaying;
    }
}
