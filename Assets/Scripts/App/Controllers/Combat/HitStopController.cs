using UnityEngine;

/// <summary>
/// 命中卡肉控制器：订阅 AttackHitEvent，冻结攻击者 Animator、暂停 ActionExecutor 与关联 VFX 粒子。
/// 可挂在 Player 或场景 Managers 上；按 ActionDefinition 配置驱动。
/// </summary>
[DisallowMultipleComponent]
public class HitStopController : AppControllerBase
{
    Transform _activeAttacker;
    Animator _activeAnimator;
    ActionExecutor _activeExecutor;
    float _normalAnimatorSpeed = 1f;
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

    /// <summary>命中回调：按 ActionDefinition 配置触发攻击者侧卡肉。</summary>
    void HandleAttackHit(AttackHitEvent hitEvent)
    {
        ActionHitContext context = hitEvent.Context;
        if (context.Action == null || context.Attacker == null)
            return;

        if (!context.Action.ShouldHitStopOnHit())
            return;

        CombatActorSystem actorSystem = GetSystem<CombatActorSystem>();
        if (actorSystem == null)
            return;

        if (!actorSystem.TryGet(context.Attacker, out CombatActorEntry entry))
            return;

        ActionExecutor executor = entry.ActionExecutor;
        Animator animator = entry.Animator;

        if (context.Action.HitStopOncePerAction && executor != null && !executor.TryConsumeHitStopTrigger())
            return;

        float duration = context.Action.HitStopDurationSeconds;
        if (duration <= 0f)
            return;

        ApplyHitStop(context.Attacker, executor, animator, duration);
    }

    /// <summary>对攻击者施加卡肉；若已在卡肉中则延长剩余时间。</summary>
    void ApplyHitStop(
        Transform attacker,
        ActionExecutor executor,
        Animator animator,
        float durationSeconds)
    {
        if (animator == null)
            return;

        bool sameAttacker = _activeAttacker == attacker && _remainingSeconds > 0f;
        if (!sameAttacker)
        {
            ForceEndHitStop();
            _activeAttacker = attacker;
            _activeExecutor = executor;
            _activeAnimator = animator;
            _normalAnimatorSpeed = animator.speed > 0f ? animator.speed : 1f;

            executor?.SetHitStopPaused(true);
            animator.speed = 0f;
            GetSystem<CombatFeedbackSystem>()?.BeginHitStop(attacker);
        }

        _remainingSeconds = Mathf.Max(_remainingSeconds, durationSeconds);
    }

    void EndHitStop()
    {
        _remainingSeconds = 0f;

        if (_activeExecutor != null)
            _activeExecutor.SetHitStopPaused(false);

        if (_activeAnimator != null)
            _activeAnimator.speed = _normalAnimatorSpeed;

        _activeAttacker = null;
        _activeExecutor = null;
        _activeAnimator = null;
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
