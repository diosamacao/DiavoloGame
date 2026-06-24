/// <summary>角色动作状态：锁定 Locomotion 动画，推进 ActionExecutor 并在动作结束后回到移动状态。</summary>
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
        Context.ActionExecutor?.Stop();
        Context.Animation.SetLocked(false);
        Context.Animation.ResetPlaybackState();
    }

    public override void Tick(float deltaTime)
    {
        Context.Movement.ClearMoveSnapshot();
        SyncMotorSnapshot();
        if (!AdvanceActionExecutor(deltaTime))
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

    /// <summary>推进单角色动作执行器；动作结束时返回 false 以回到 Locomotion。</summary>
    bool AdvanceActionExecutor(float deltaTime)
    {
        IActionExecutor executor = Context.ActionExecutor;
        if (executor == null)
            return false;

        executor.Tick(deltaTime);
        return executor.IsPlaying;
    }
}
