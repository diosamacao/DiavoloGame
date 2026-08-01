/// <summary>角色动作状态：锁定 Locomotion 动画，并按 ActionSim 实例结果返回移动状态。</summary>
public class ActionState : CharacterState
{
    ActionDefinition _lastActiveAction;
    int _activeActionInstanceId;

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
        ActionSimSnapshot snapshot = Context.ActionSim != null
            ? Context.ActionSim.Snapshot
            : default;
        _lastActiveAction = snapshot.Content as ActionDefinition;
        _activeActionInstanceId = snapshot.InstanceId;
        Context.Animation.SetLocked(true);
    }

    /// <summary>离开 Action；Dodge 退出时写入一次性 Sprint 恢复请求。</summary>
    public override void Exit()
    {
        // 三种退出路径都汇聚于此；记录最后有效招式，避免自然收招后 Session 已清空。
        ActionDefinition exitAction = Context.ActionSim != null
            ? Context.ActionSim.Snapshot.Content as ActionDefinition
            : null;
        exitAction ??= _lastActiveAction;
        if (exitAction != null && exitAction.ActionType == CombatActionType.Dodge)
            Context.SetLocomotionResumeRequest(LocomotionResumeRequest.SprintAfterDodge);

        Context.ActionSim?.Stop();
        Context.ActionRotation?.Reset();
        Context.Animation.SetLocked(false);
        Context.Animation.ResetPlaybackState();
        _lastActiveAction = null;
    }

    /// <summary>记录当前整数帧动作并执行动作转向；会话由 CharacterActor 单次推进。</summary>
    public override void Tick(float deltaTime)
    {
        ActionSimSnapshot snapshot = Context.ActionSim != null
            ? Context.ActionSim.Snapshot
            : default;
        ActionDefinition current = snapshot.Content as ActionDefinition;
        if (current != null)
        {
            _lastActiveAction = current;
            _activeActionInstanceId = snapshot.InstanceId;
        }

        Context.Movement.ClearMoveSnapshot();
        SyncMotorSnapshot();
        Context.ActionRotation?.Tick(deltaTime);
    }

    /// <summary>自动衔接与自然结束处理后，根据逻辑会话结果退出或接管新动作。</summary>
    public void ResolvePostCombat()
    {
        ActionSim actionSim = Context.ActionSim;
        if (_activeActionInstanceId <= 0 || actionSim == null)
        {
            Context.StateMachine.TryChangeState(CharacterStateType.Locomotion);
            return;
        }

        if (!actionSim.HasEndedActionInstance(_activeActionInstanceId))
            return;

        ActionSimSnapshot snapshot = actionSim.Snapshot;
        if (snapshot.InstanceId > 0)
        {
            _activeActionInstanceId = snapshot.InstanceId;
            _lastActiveAction = snapshot.Content as ActionDefinition;
            return;
        }

        Context.StateMachine.TryChangeState(CharacterStateType.Locomotion);
    }

    /// <summary>Action 状态清空 Locomotion 输入快照，避免动作中播放移动动画。</summary>
    void SyncMotorSnapshot()
    {
        Context.MoveInputMagnitude = Context.Movement.MoveInputMagnitude;
        Context.RunThreshold = Context.Movement.RunThreshold;
        Context.IsGrounded = Context.Movement.IsGrounded;
    }

}
