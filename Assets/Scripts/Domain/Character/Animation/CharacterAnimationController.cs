using UnityEngine;

/// <summary>角色动画播放入口；运行时由 CharacterConfig 注入 Animator 与 Locomotion Profile。</summary>
public sealed class CharacterAnimationController
{
    CharacterAnimationProfile profile;
    readonly Animator animator;
    readonly int layerIndex;
    AnimationKey? _currentKey;
    bool _locked;

    public AnimationKey? CurrentKey => _currentKey;
    public bool IsLocked => _locked;

    /// <summary>驱动招式的 Animator；可能位于子节点。</summary>
    public Animator Animator => animator;

    /// <summary>创建角色动画服务；Animator 来自 CharacterConfig 实例化出的模型。</summary>
    public CharacterAnimationController(
        Animator targetAnimator,
        CharacterAnimationProfile animationProfile,
        int targetLayerIndex)
    {
        animator = targetAnimator;
        profile = animationProfile;
        layerIndex = targetLayerIndex;
    }

    /// <summary>切换 Locomotion Profile。</summary>
    public void SetProfile(CharacterAnimationProfile animationProfile) => profile = animationProfile;

    /// <summary>切换 Locomotion Profile 后调用，强制下一帧按新映射重播 AnimationKey。</summary>
    public void ResetPlaybackState() => _currentKey = null;

    public void SetLocked(bool locked) => _locked = locked;

    public void Play(AnimationKey key, float? fadeDuration = null)
    {
        if (_locked || profile == null || animator == null)
            return;

        if (_currentKey == key)
            return;

        float fade = fadeDuration ?? profile.DefaultCrossFadeDuration;
        int stateHash = Animator.StringToHash(profile.GetStateName(key));
        animator.CrossFadeInFixedTime(stateHash, fade, layerIndex);
        _currentKey = key;
    }

    public void PlayClip(AnimationClip clip, float fadeDuration = 0.1f)
    {
        if (animator == null || clip == null)
            return;

        animator.CrossFadeInFixedTime(clip.name, fadeDuration, layerIndex);
        _currentKey = null;
    }

    public bool HasFinishedClip(AnimationClip clip)
    {
        if (animator == null || clip == null)
            return true;

        if (animator.IsInTransition(layerIndex))
            return false;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layerIndex);
        return state.IsName(clip.name) && state.normalizedTime >= 1f;
    }
}
