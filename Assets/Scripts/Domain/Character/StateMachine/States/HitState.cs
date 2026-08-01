/// <summary>角色受击硬直状态；可播放专用 Action，否则按配置整数帧锁定移动与出招。</summary>
public sealed class HitState : CharacterState
{
    int _remainingFrames;
    int _reactionActionInstanceId;

    /// <summary>受击状态 id。</summary>
    public override CharacterStateType Id => CharacterStateType.Hit;

    /// <summary>硬直结束允许回到 Locomotion，连续受击由 Actor 强制重入。</summary>
    public override bool CanTransitionTo(CharacterStateType next) =>
        next == CharacterStateType.Locomotion || next == CharacterStateType.Death;

    /// <summary>消费反应请求，停掉攻击并开始可选受击 Action。</summary>
    public override void Enter()
    {
        CharacterReactionRequest request = Context.ConsumeReactionRequest();
        _remainingFrames = request.DurationFrames;
        _reactionActionInstanceId = 0;
        Context.Movement.ClearMoveSnapshot();
        Context.Animation.SetLocked(true);
        Context.ActionSim?.Stop();

        if (request.ResolvedAction != null)
        {
            // 每次命中都会强制重入 Hit；记录逻辑会话 Id，退出不读取动画播放状态。
            ActionSimResolveResult result =
                ActionSimResolveResult.FromContent(request.ResolvedAction);
            if (Context.ActionSim != null && Context.ActionSim.TryStart(in result))
                _reactionActionInstanceId = Context.ActionSim.InstanceId;
        }
    }

    /// <summary>无反应 Action 时递减固定硬直帧；动作会话在 PostCombat 收尾。</summary>
    public override void Tick(float deltaTime)
    {
        Context.Movement.ClearMoveSnapshot();
        if (_reactionActionInstanceId > 0)
            return;

        if (_remainingFrames > 0)
            _remainingFrames--;
        if (_remainingFrames <= 0)
            Context.StateMachine.TryChangeState(CharacterStateType.Locomotion);
    }

    /// <summary>命中统一结算后，以逻辑动作会话结束标记退出受击状态。</summary>
    public void ResolvePostCombat()
    {
        if (_reactionActionInstanceId <= 0)
            return;

        if (Context.ActionSim?.HasEndedActionInstance(_reactionActionInstanceId) == true)
            Context.StateMachine.TryChangeState(CharacterStateType.Locomotion);
    }

    /// <summary>结束受击表现并恢复 Locomotion 动画写入。</summary>
    public override void Exit()
    {
        Context.ActionSim?.Stop();
        Context.Animation.SetLocked(false);
        Context.Animation.ResetPlaybackState();
    }
}
