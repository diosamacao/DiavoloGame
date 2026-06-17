using UnityEngine;

[DisallowMultipleComponent]
public class CharacterAnimationController : MonoBehaviour
{
    [SerializeField] CharacterAnimationProfile profile;
    [SerializeField] Animator animator;
    [SerializeField] int layerIndex;

    AnimationKey? _currentKey;
    bool _locked;

    public AnimationKey? CurrentKey => _currentKey;
    public bool IsLocked => _locked;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

    }

    public void SetProfile(CharacterAnimationProfile animationProfile) => profile = animationProfile;

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

    public void ResetPlaybackState() => _currentKey = null;
}
