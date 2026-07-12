using System;
using UnityEngine;

/// <summary>角色动画播放门面；调用层只依赖本类，后端通过 IAnimationPlayback 可替换为 Animancer。</summary>
public sealed class CharacterAnimationService : IDisposable
{
    CharacterAnimationProfile profile;
    readonly IAnimationPlayback playback;
    readonly Animator animator;
    AnimationKey? _currentKey;
    bool _locked;

    public AnimationKey? CurrentKey => _currentKey;
    public bool IsLocked => _locked;

    /// <summary>驱动骨骼的 Animator（Playable 输出目标）；供 Root Motion 等桥接使用。</summary>
    public Animator Animator => animator;

    /// <summary>当前播放倍率；卡肉时置 0。</summary>
    public float Speed
    {
        get => playback != null ? playback.Speed : 1f;
        set
        {
            if (playback != null)
                playback.Speed = value;
        }
    }

    /// <summary>创建角色动画服务；playback 由工厂注入（Playable 或未来 Animancer）。</summary>
    public CharacterAnimationService(
        IAnimationPlayback animationPlayback,
        Animator targetAnimator,
        CharacterAnimationProfile animationProfile)
    {
        playback = animationPlayback;
        animator = targetAnimator;
        profile = animationProfile;
    }

    /// <summary>切换 Locomotion Profile。</summary>
    public void SetProfile(CharacterAnimationProfile animationProfile) => profile = animationProfile;

    /// <summary>切换 Locomotion Profile 后调用，强制下一帧按新映射重播 AnimationKey。</summary>
    public void ResetPlaybackState() => _currentKey = null;

    public void SetLocked(bool locked) => _locked = locked;

    /// <summary>设置播放倍率；0 冻结骨骼（HitStop）。</summary>
    public void SetSpeed(float speed) => Speed = speed;

    public void Play(AnimationKey key, float? fadeDuration = null)
    {
        if (_locked || profile == null || playback == null || !playback.IsValid)
            return;

        if (_currentKey == key)
            return;

        if (!profile.TryGetClip(key, out AnimationClip clip))
        {
            Debug.LogError($"CharacterAnimationService: Profile 未绑定 {key} 的 Clip。", profile);
            return;
        }

        float fade = fadeDuration ?? profile.DefaultCrossFadeDuration;
        playback.Play(clip, fade);
        _currentKey = key;
    }

    public void PlayClip(AnimationClip clip, float fadeDuration = 0.1f)
    {
        if (playback == null || !playback.IsValid || clip == null)
            return;

        playback.Play(clip, fadeDuration);
        _currentKey = null;
    }

    /// <summary>将当前招式 Clip 跳到指定时间（秒）。</summary>
    public void SeekClip(float timeSeconds) => playback?.Seek(timeSeconds);

    public bool HasFinishedClip(AnimationClip clip)
    {
        if (playback == null || clip == null)
            return true;

        return playback.CurrentClip == clip && playback.HasFinished;
    }

    /// <summary>推进后端淡入等每帧逻辑；由 CharacterActor.Tick 调用。</summary>
    public void Tick(float deltaTime) => playback?.Tick(deltaTime);

    /// <summary>销毁播放后端（PlayableGraph 等）。</summary>
    public void Dispose() => playback?.Dispose();
}
