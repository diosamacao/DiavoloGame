using UnityEngine;

/// <summary>Flinch Additive 的共享播放入口；权威 FlinchIssued 与客机复制命中共用。</summary>
public static class HitFlinchPresentation
{
    /// <summary>对可见体叠 HitShake；不 Play 主轨、不锁 Locomotion。</summary>
    public static bool TryPlay(
        CharacterAnimationService animation,
        AnimationKey key,
        AnimationClip fallbackClip,
        AvatarMask fallbackMask,
        float fadeDuration)
    {
        if (animation == null || !animation.HasPlayback)
            return false;

        if (animation.TryPlayAdditive(key, fallbackMask, fadeDuration))
            return true;

        if (fallbackClip == null)
            return false;

        animation.PlayAdditive(fallbackClip, fallbackMask, fadeDuration);
        return true;
    }

    /// <summary>按 SimActorId 解析 Proxy 或 Full 可见体并播 Additive。</summary>
    public static bool TryPlayOnActor(
        CharacterActor actor,
        AnimationKey key,
        AnimationClip fallbackClip,
        AvatarMask fallbackMask,
        float fadeDuration)
    {
        CharacterAnimationService animation = ResolvePresentation(actor);
        return TryPlay(animation, key, fallbackClip, fallbackMask, fadeDuration);
    }

    /// <summary>客机 Observer：只打 Proxy Playable，不写 ActionSim。</summary>
    public static bool TryPlayOnProxy(
        RemoteCharacterProxy proxy,
        AnimationKey key,
        AnimationClip fallbackClip,
        AvatarMask fallbackMask,
        float fadeDuration)
    {
        return proxy != null
            && TryPlay(proxy.Animation, key, fallbackClip, fallbackMask, fadeDuration);
    }

    /// <summary>Full Actor 优先，否则 Live Proxy；Listen 无头敌人走 Proxy。</summary>
    public static CharacterAnimationService ResolvePresentation(CharacterActor actor)
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

        CharacterAnimationService local = actor?.Animation;
        return local != null && local.HasPlayback ? local : null;
    }
}
