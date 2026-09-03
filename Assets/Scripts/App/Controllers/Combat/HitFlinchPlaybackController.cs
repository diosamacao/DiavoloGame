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

    /// <summary>客机复制命中叠 Proxy Additive 时使用同一 fallback。</summary>
    public bool TryPlayOnProxy(RemoteCharacterProxy proxy, AnimationKey key) =>
        HitFlinchPresentation.TryPlayOnProxy(
            proxy,
            key,
            fallbackFlinchClip,
            fallbackFlinchMask,
            fadeDuration);

    void OnEnable() => CharacterActor.FlinchIssued += OnFlinchIssued;

    void OnDisable() => CharacterActor.FlinchIssued -= OnFlinchIssued;

    /// <summary>权威 Flinch 后叠可见体层 1；Listen 无头敌人打 Observer Proxy。</summary>
    void OnFlinchIssued(CharacterActor actor, AnimationKey key, ActionHitContext context)
    {
        if (actor == null)
            return;

        if (!HitFlinchPresentation.TryPlayOnActor(
                actor,
                key,
                fallbackFlinchClip,
                fallbackFlinchMask,
                fadeDuration))
        {
            Debug.LogWarning(
                "HitFlinchPlayback: 可见体没有 HitShake Clip，且未拖 fallback。逻辑仍不停招。",
                this);
            return;
        }

        GetArchitecture().SendEvent(new HitFlinchEvent(
            actor.SimulationId,
            key,
            context.AttackerId,
            context.ActionInstanceId));
    }
}
