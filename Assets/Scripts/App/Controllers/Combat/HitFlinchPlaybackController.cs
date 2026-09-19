using UnityEngine;

/// <summary>
/// Observer 复制命中时为 Proxy 播放 Flinch Additive；本地 Actor 由注入的表现 Sink 处理。
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

}
