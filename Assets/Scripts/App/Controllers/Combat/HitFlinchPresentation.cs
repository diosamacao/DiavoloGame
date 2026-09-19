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

}
