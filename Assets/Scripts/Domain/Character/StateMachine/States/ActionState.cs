/// <summary>角色动作状态：锁定 Locomotion 动画，推进 ActionExecutor 并在动作结束后回到移动状态。</summary>
public class ActionState : CharacterState
{
    ActionDefinition _lastActiveAction;

    public override CharacterStateType Id => CharacterStateType.Action;

    public override bool CanTransitionTo(CharacterStateType next)
    {
        if (next == CharacterStateType.Locomotion)
            return true;

        return base.CanTransitionTo(next);
    }

    /// <summary>进入 Action，缓存当前招式并锁定 Locomotion 动画写入。</summary>
    public override void Enter()
    {
        _lastActiveAction = Context.ActionExecutor?.CurrentAction;
        Context.Animation.SetLocked(true);
    }

    /// <summary>离开 Action；Dodge 退出时写入一次性 Sprint 恢复请求。</summary>
    public override void Exit()
    {
        // 三种退出路径都汇聚于此；记录最后有效招式，避免自然收招后 Session 已清空。
        ActionDefinition exitAction = Context.ActionExecutor?.CurrentAction ?? _lastActiveAction;
        if (exitAction != null && exitAction.ActionType == CombatActionType.Dodge)
            Context.SetLocomotionResumeRequest(LocomotionResumeRequest.SprintAfterDodge);

        Context.ActionExecutor?.Stop();
        Context.Animation.SetLocked(false);
        Context.Animation.ResetPlaybackState();
        _lastActiveAction = null;
    }

    /// <summary>推进动作、记录最后有效招式并执行动作转向。</summary>
    public override void Tick(float deltaTime)
    {
        ActionDefinition current = Context.ActionExecutor?.CurrentAction;
        if (current != null)
            _lastActiveAction = current;

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
        if (executor.CurrentAction != null)
            _lastActiveAction = executor.CurrentAction;
        return executor.IsPlaying;
    }
}
