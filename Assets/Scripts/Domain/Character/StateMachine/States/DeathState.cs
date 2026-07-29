/// <summary>角色死亡终态；清空移动与攻击并等待可选死亡 Action 播放完成。</summary>
public sealed class DeathState : CharacterState
{
    /// <summary>死亡状态 id。</summary>
    public override CharacterStateType Id => CharacterStateType.Death;

    /// <summary>死亡为终态，不允许常规状态转换。</summary>
    public override bool CanTransitionTo(CharacterStateType next) => false;

    /// <summary>消费死亡表现请求并开始可选死亡 Action。</summary>
    public override void Enter()
    {
        CharacterReactionRequest request = Context.ConsumeReactionRequest();
        Context.DeathPresentationComplete = false;
        Context.Movement.ClearMoveSnapshot();
        Context.Animation.SetLocked(true);
        Context.ActionExecutor?.Stop();

        if (request.ResolvedAction == null
            || Context.ActionExecutor?.TryStart(request.ResolvedAction) != true)
            Context.DeathPresentationComplete = true;
    }

    /// <summary>推进死亡 Action；播放结束后通知生命周期控制器可回收角色。</summary>
    public override void Tick(float deltaTime)
    {
        Context.Movement.ClearMoveSnapshot();
        if (Context.DeathPresentationComplete)
            return;

        Context.ActionExecutor?.Tick(deltaTime);
        if (Context.ActionExecutor?.IsPlaying != true)
            Context.DeathPresentationComplete = true;
    }
}
