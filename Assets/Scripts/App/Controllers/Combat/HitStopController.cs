using UnityEngine;

/// <summary>
/// 命中卡肉控制器：订阅 AttackHitEvent，冻结攻击者动画 Speed、暂停 ActionExecutor 与关联 VFX 粒子。
/// 可挂在 Player 或场景 Managers 上；按命中 Hitbox 的反馈载荷驱动。
/// </summary>
[DisallowMultipleComponent]
public class HitStopController : AppControllerBase
{
    Transform _activeAttacker;
    CharacterAnimationService _activeAnimation;
    ActionExecutor _activeExecutor;
    float _normalAnimationSpeed = 1f;
    float _remainingSeconds;

    void Update()
    {
        if (_remainingSeconds <= 0f)
            return;

        _remainingSeconds -= Time.unscaledDeltaTime;
        if (_remainingSeconds <= 0f)
            EndHitStop();
    }

    void OnEnable()
    {
        RegisterEvent<AttackHitEvent>(HandleAttackHit);
    }

    void OnDisable()
    {
        UnregisterEvent<AttackHitEvent>(HandleAttackHit);
        ForceEndHitStop();
    }

    /// <summary>命中回调：按 Hitbox 反馈载荷触发攻击者侧卡肉。</summary>
    void HandleAttackHit(AttackHitEvent hitEvent)
    {
        ActionHitContext context = hitEvent.Context;
        if (context.Action == null || context.Hitbox == null || context.Attacker == null)
            return;

        HitFeedbackSettings feedback = context.Hitbox.Payload.Feedback;
        if (!feedback.UseHitStop)
            return;

        CombatActorSystem actorSystem = GetSystem<CombatActorSystem>();
        if (actorSystem == null)
            return;

        if (!actorSystem.TryGet(context.Attacker, out CombatActorEntry entry))
            return;

        ActionExecutor executor = entry.ActionExecutor;
        CharacterAnimationService animation = entry.Animation;

        if (feedback.HitStopOncePerAction
            && executor != null
            && !executor.TryConsumeHitStopTrigger())
            return;

        float duration = feedback.ResolveHitStopDuration(context.Action.SampleRate);
        if (duration <= 0f)
            return;

        ApplyHitStop(context.Attacker, executor, animation, duration);
    }

    /// <summary>对攻击者施加卡肉；若已在卡肉中则延长剩余时间。</summary>
    void ApplyHitStop(
        Transform attacker,
        ActionExecutor executor,
        CharacterAnimationService animation,
        float durationSeconds)
    {
        if (animation == null)
            return;

        bool sameAttacker = _activeAttacker == attacker && _remainingSeconds > 0f;
        if (!sameAttacker)
        {
            ForceEndHitStop();
            _activeAttacker = attacker;
            _activeExecutor = executor;
            _activeAnimation = animation;
            _normalAnimationSpeed = animation.Speed > 0f ? animation.Speed : 1f;

            executor?.SetHitStopPaused(true);
            animation.SetSpeed(0f);
            GetSystem<CombatFeedbackSystem>()?.BeginHitStop(attacker);
        }

        _remainingSeconds = Mathf.Max(_remainingSeconds, durationSeconds);
    }

    void EndHitStop()
    {
        _remainingSeconds = 0f;

        if (_activeExecutor != null)
            _activeExecutor.SetHitStopPaused(false);

        if (_activeAnimation != null)
            _activeAnimation.SetSpeed(_normalAnimationSpeed);

        _activeAttacker = null;
        _activeExecutor = null;
        _activeAnimation = null;
        GetSystem<CombatFeedbackSystem>()?.EndHitStop();
    }

    void ForceEndHitStop()
    {
        if (_remainingSeconds <= 0f && _activeAttacker == null)
            return;

        _remainingSeconds = 0f;
        EndHitStop();
    }
}
