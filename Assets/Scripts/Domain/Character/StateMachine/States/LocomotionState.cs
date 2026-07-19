/// <summary>顶层 Locomotion 状态：委托 LocomotionService 推进相位、位移与动画。</summary>
public class LocomotionState : CharacterState
{
    public override CharacterStateType Id => CharacterStateType.Locomotion;

    public override bool CanTransitionTo(CharacterStateType next)
    {
        if (next == CharacterStateType.Action)
            return true;

        return base.CanTransitionTo(next);
    }

    /// <summary>进入 Locomotion，并消费 Action 边界留下的一次性恢复请求。</summary>
    public override void Enter()
    {
        LocomotionResumeRequest request = Context.ConsumeLocomotionResumeRequest();
        Context.Locomotion.Enter(in request);
    }

    public override void Exit() => Context.Locomotion.Exit();

    public override void Tick(float deltaTime)
    {
        Context.Locomotion.Tick(deltaTime);
        SyncMotorSnapshot();
    }

    /// <summary>同步 Motor 快照到 Context，供外部只读。</summary>
    void SyncMotorSnapshot()
    {
        Context.MoveInputMagnitude = Context.Movement.MoveInputMagnitude;
        Context.RunThreshold = Context.Movement.RunThreshold;
        Context.IsGrounded = Context.Movement.IsGrounded;
    }
}
