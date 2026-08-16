/// <summary>角色死亡终态；清空移动与攻击并等待可选死亡 Action 播放完成。</summary>
public sealed class DeathState : CharacterState
{
    int _deathActionInstanceId;

    /// <summary>死亡状态 id。</summary>
    public override CharacterStateType Id => CharacterStateType.Death;

    /// <summary>死亡为终态，不允许常规状态转换。</summary>
    public override bool CanTransitionTo(CharacterStateType next) => false;

    /// <summary>消费死亡表现请求并开始可选死亡 Action。</summary>
    public override void Enter()
    {
        CharacterReactionRequest request = Context.ConsumeReactionRequest();
        Context.DeathPresentationComplete = false;
        _deathActionInstanceId = 0;
        Context.Movement.ClearMoveSnapshot();
        Context.Animation.SetLocked(true);
        Context.ActionSim?.Stop();

        if (request.ResolvedAction == null)
        {
            Context.DeathPresentationComplete = true;
            return;
        }

        ActionSimResolveResult result =
            ActionSimResolveResult.FromContent(request.ResolvedAction);
        if (Context.ActionSim != null && Context.ActionSim.TryStart(in result))
            _deathActionInstanceId = Context.ActionSim.InstanceId;
        else
            Context.DeathPresentationComplete = true;
    }

    /// <summary>死亡状态只锁定移动；动作整数帧由 CharacterActor 统一推进。</summary>
    public override void Tick(float deltaTime)
    {
        // 死亡终态只锁移动；死亡招式由 Actor 统一推帧
        Context.Movement.ClearMoveSnapshot();
    }

    /// <summary>命中统一结算后，以逻辑动作会话结束标记死亡表现完成。</summary>
    public void ResolvePostCombat()
    {
        if (_deathActionInstanceId > 0
            && Context.ActionSim?.HasEndedActionInstance(_deathActionInstanceId) == true)
        {
            Context.DeathPresentationComplete = true;
        }
    }
}
