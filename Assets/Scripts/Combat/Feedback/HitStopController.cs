using UnityEngine;

/// <summary>
/// 命中卡肉控制器：订阅 CombatHitFeedback，冻结攻击者 Animator、暂停 ActionRuntime 与关联 VFX 粒子。
/// 可挂在 Player 或场景 Managers 上；按 ActionDefinition 配置驱动。
/// </summary>
[DisallowMultipleComponent]
public class HitStopController : MonoBehaviour
{
    Transform _activeAttacker;
    Animator _activeAnimator;
    ActionRuntimeController _activeRuntime;
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
        CombatHitFeedback.AttackHitDetailed += HandleAttackHit;
    }

    void OnDisable()
    {
        CombatHitFeedback.AttackHitDetailed -= HandleAttackHit;
        ForceEndHitStop();
    }

    /// <summary>命中回调：按 ActionDefinition 配置触发攻击者侧卡肉。</summary>
    void HandleAttackHit(ActionHitContext context, Transform targetTransform)
    {
        if (context.Action == null || context.Attacker == null)
            return;

        if (!context.Action.ShouldHitStopOnHit())
            return;

        CombatRuntimeRegistry.TryGet(context.Attacker, out ActionRuntimeController runtime, out Animator animator);
        if (context.Action.HitStopOncePerAction && runtime != null && !runtime.TryConsumeHitStopTrigger())
            return;

        float duration = context.Action.HitStopDurationSeconds;
        if (duration <= 0f)
            return;

        ApplyHitStop(context.Attacker, runtime, animator, duration);
    }

    /// <summary>对攻击者施加卡肉；若已在卡肉中则延长剩余时间。</summary>
    void ApplyHitStop(
        Transform attacker,
        ActionRuntimeController runtime,
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
            _activeRuntime = runtime;
            _activeAnimator = animator;
            _normalAnimatorSpeed = animator.speed > 0f ? animator.speed : 1f;

            runtime?.SetHitStopPaused(true);
            animator.speed = 0f;
            CombatHitStop.NotifyBegan(attacker);
        }

        _remainingSeconds = Mathf.Max(_remainingSeconds, durationSeconds);
    }

    void EndHitStop()
    {
        _remainingSeconds = 0f;

        if (_activeRuntime != null)
            _activeRuntime.SetHitStopPaused(false);

        if (_activeAnimator != null)
            _activeAnimator.speed = _normalAnimatorSpeed;

        _activeAttacker = null;
        _activeRuntime = null;
        _activeAnimator = null;
        CombatHitStop.NotifyEnded();
    }

    void ForceEndHitStop()
    {
        if (_remainingSeconds <= 0f && _activeAttacker == null)
            return;

        _remainingSeconds = 0f;
        EndHitStop();
    }

}
