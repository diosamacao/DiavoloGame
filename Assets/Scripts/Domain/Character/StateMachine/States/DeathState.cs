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
        Context.ActionExecutor?.Stop();

        if (request.ResolvedAction == null)
        {
            Context.DeathPresentationComplete = true;
            return;
        }

        if (Context.ActionExecutor?.TryStart(request.ResolvedAction) == true)
            _deathActionInstanceId = Context.ActionExecutor.CurrentActionInstanceId;
        else
            Context.DeathPresentationComplete = true;
    }

    /// <summary>死亡状态只锁定移动；动作整数帧由 CharacterActor 统一推进。</summary>
    public override void Tick(float deltaTime)
    {
        Context.Movement.ClearMoveSnapshot();
    }

    /// <summary>命中统一结算后，以逻辑动作会话结束标记死亡表现完成。</summary>
    public void ResolvePostCombat()
    {
        if (_deathActionInstanceId > 0
            && Context.ActionExecutor?.HasEndedActionInstance(_deathActionInstanceId) == true)
        {
            Context.DeathPresentationComplete = true;
        }
    }
}
