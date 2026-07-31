using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 命中卡肉表现控制器：订阅帧末 AttackHitEvent，只冻结动画与关联 VFX 粒子。
/// 可挂在 Player 或场景 Managers 上；按命中 Hitbox 的反馈载荷驱动。
/// </summary>
[DisallowMultipleComponent]
public class HitStopController : AppControllerBase
{
    Transform _activeAttacker;
    CharacterAnimationService _activeAnimation;
    readonly Dictionary<Transform, int> _lastTriggeredActionInstance = new();
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
        _lastTriggeredActionInstance.Clear();
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

        CharacterAnimationService animation = entry.Animation;

        if (feedback.HitStopOncePerAction
            && context.ActionInstanceId > 0)
        {
            if (_lastTriggeredActionInstance.TryGetValue(
                    context.Attacker,
                    out int consumedInstance)
                && consumedInstance == context.ActionInstanceId)
            {
                return;
            }

            _lastTriggeredActionInstance[context.Attacker] = context.ActionInstanceId;
        }

        float duration = feedback.ResolveHitStopDuration(context.Action.SampleRate);
        if (duration <= 0f)
            return;

        ApplyHitStop(context.Attacker, animation, duration);
    }

    /// <summary>对攻击者施加卡肉；若已在卡肉中则延长剩余时间。</summary>
    void ApplyHitStop(
        Transform attacker,
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
            _activeAnimation = animation;
            _normalAnimationSpeed = animation.Speed > 0f ? animation.Speed : 1f;

            animation.SetSpeed(0f);
            GetSystem<CombatFeedbackSystem>()?.BeginHitStop(attacker);
        }

        _remainingSeconds = Mathf.Max(_remainingSeconds, durationSeconds);
    }

    void EndHitStop()
    {
        _remainingSeconds = 0f;

        if (_activeAnimation != null)
            _activeAnimation.SetSpeed(_normalAnimationSpeed);

        _activeAttacker = null;
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
