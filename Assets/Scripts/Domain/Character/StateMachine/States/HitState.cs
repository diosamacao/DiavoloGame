using UnityEngine;

/// <summary>角色受击硬直状态；可播放专用 Action，否则按配置时间锁定移动与出招。</summary>
public sealed class HitState : CharacterState
{
    float _remainingSeconds;

    /// <summary>受击状态 id。</summary>
    public override CharacterStateType Id => CharacterStateType.Hit;

    /// <summary>硬直结束允许回到 Locomotion，连续受击由 Actor 强制重入。</summary>
    public override bool CanTransitionTo(CharacterStateType next) =>
        next == CharacterStateType.Locomotion || next == CharacterStateType.Death;

    /// <summary>消费反应请求，停掉攻击并开始可选受击 Action。</summary>
    public override void Enter()
    {
        CharacterReactionRequest request = Context.ConsumeReactionRequest();
        _remainingSeconds = request.DurationSeconds;
        Context.Movement.ClearMoveSnapshot();
        Context.Animation.SetLocked(true);
        Context.ActionExecutor?.Stop();

        if (request.ResolvedAction != null)
        {
            // 每次命中都会强制重入 Hit；只有动作真正启动后才由播放完成时机接管退出。
            if (Context.ActionExecutor?.TryStart(request.ResolvedAction) == true)
                _remainingSeconds = 0f;
        }
    }

    /// <summary>推进受击表现；Action 播完或计时结束后返回 Locomotion。</summary>
    public override void Tick(float deltaTime)
    {
        Context.Movement.ClearMoveSnapshot();
        if (Context.ActionExecutor?.IsPlaying == true)
        {
            Context.ActionExecutor.Tick(deltaTime);
            return;
        }

        _remainingSeconds = Mathf.Max(0f, _remainingSeconds - Mathf.Max(0f, deltaTime));
        if (_remainingSeconds <= 0f)
            Context.StateMachine.TryChangeState(CharacterStateType.Locomotion);
    }

    /// <summary>结束受击表现并恢复 Locomotion 动画写入。</summary>
    public override void Exit()
    {
        Context.ActionExecutor?.Stop();
        Context.Animation.SetLocked(false);
        Context.Animation.ResetPlaybackState();
    }
}
