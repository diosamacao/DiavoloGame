using UnityEngine;

/// <summary>
/// 订 Flinch：只对可见 Playable 叠 Additive，不 Play 主轨、不锁走跑、不写 ActionSim。
/// </summary>
[DisallowMultipleComponent]
public sealed class HitFlinchPlaybackController : AppControllerBase
{
    [Tooltip("Profile 未绑 HitShake 时的回退 Clip；须 Additive 导入、无根移。")]
    [SerializeField] AnimationClip fallbackFlinchClip;
    [Tooltip("可选上半身 Mask；空则全骨骼叠加。")]
    [SerializeField] AvatarMask fallbackFlinchMask;
    [SerializeField] float fadeDuration = 0.05f;

    void OnEnable() => CharacterActor.FlinchIssued += OnFlinchIssued;

    void OnDisable() => CharacterActor.FlinchIssued -= OnFlinchIssued;

    /// <summary>权威 Flinch 后叠可见体层 1；Listen 无头敌人打 Observer Proxy。</summary>
    void OnFlinchIssued(CharacterActor actor, AnimationKey key, ActionHitContext context)
    {
        if (actor == null)
            return;

        CharacterAnimationService animation = ResolvePresentation(actor);
        if (animation == null || !animation.HasPlayback)
            return;

        // 只走 PlayAdditive：Locomotion / Action 主轨继续 Tick，CurrentKey 不变。
        if (!animation.TryPlayAdditive(key, fallbackFlinchMask, fadeDuration))
        {
            if (fallbackFlinchClip == null)
            {
                Debug.LogWarning(
                    "HitFlinchPlayback: 可见体没有 HitShake Clip，且未拖 fallback。逻辑仍不停招。",
                    this);
                return;
            }

            animation.PlayAdditive(fallbackFlinchClip, fallbackFlinchMask, fadeDuration);
        }

        GetArchitecture().SendEvent(new HitFlinchEvent(
            actor.SimulationId,
            key,
            context.AttackerId,
            context.ActionInstanceId));
    }

    /// <summary>Full Actor 优先，否则 Listen Ghost；都没有则不播。</summary>
    static CharacterAnimationService ResolvePresentation(CharacterActor actor)
    {
        CombatWorldController world = CombatWorldController.Current;
        if (world != null
            && world.TryResolvePresentation(actor, out CharacterAnimationService resolved)
            && resolved != null
            && resolved.HasPlayback)
        {
            return resolved;
        }

        if (RemoteCharacterProxy.TryFindLivePresentation(
                actor.SimulationId,
                out CharacterAnimationService live)
            && live != null
            && live.HasPlayback)
        {
            return live;
        }

        CharacterAnimationService local = actor.Animation;
        return local != null && local.HasPlayback ? local : null;
    }
}
