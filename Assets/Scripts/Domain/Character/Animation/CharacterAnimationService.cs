using System;
using UnityEngine;

/// <summary>角色动画播放门面；调用层只依赖本类，后端通过 IAnimationPlayback 可替换为 Animancer。</summary>
public sealed class CharacterAnimationService : IDisposable, ILocomotionAnimClipQuery
{
    CharacterAnimationProfile profile;
    readonly IAnimationPlayback playback;
    readonly Animator animator;
    AnimationKey? _currentKey;
    bool _locked;

    /// <summary>是否存在可推进的播放后端；Headless Null 后端为 false。</summary>
    public bool HasPlayback => playback != null && playback.IsValid;

    public AnimationKey? CurrentKey => _currentKey;
    public bool IsLocked => _locked;

    /// <summary>Additive 层权重；无叠加或 Headless 时为 0。</summary>
    public float AdditiveWeight => playback != null ? playback.AdditiveWeight : 0f;

    /// <summary>驱动骨骼的 Animator（Playable 输出目标）；供 Root Motion 等桥接使用。</summary>
    public Animator Animator => animator;

    /// <summary>当前主 Clip 归一化时间；供 Locomotion 落脚与相位结束判定。</summary>
    public float NormalizedTime => playback != null ? playback.NormalizedTime : 0f;

    /// <summary>当前主 Clip 是否已播完（循环 Clip 视为未结束）。</summary>
    public bool HasFinishedCurrent =>
        playback != null && playback.CurrentClip != null && playback.HasFinished;

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

    /// <summary>Profile 是否已绑定该逻辑键的 Clip。</summary>
    public bool HasClip(AnimationKey key) =>
        profile != null && profile.TryGetClip(key, out AnimationClip clip) && clip != null;

    /// <summary>
    /// 切换 Locomotion 逻辑键。Headless 无 Graph 时仍记录 CurrentKey，供权威 Capture 复制走跑相位。
    /// </summary>
    public void Play(AnimationKey key, float? fadeDuration = null)
    {
        if (_locked || profile == null)
            return;

        if (_currentKey == key)
            return;

        // Headless Null 后端 IsValid=false：禁止因无骨骼而丢掉逻辑键，否则快照永远是 Idle。
        if (playback == null || !playback.IsValid)
        {
            _currentKey = key;
            return;
        }

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

    /// <summary>叠 Additive 轻受击；不锁走跑、不改 CurrentKey。</summary>
    public void PlayAdditive(AnimationClip clip, AvatarMask mask = null, float fadeDuration = 0.05f)
    {
        if (playback == null || !playback.IsValid || clip == null)
            return;

        playback.PlayAdditive(clip, mask, fadeDuration);
    }

    /// <summary>立刻清 Additive 层；进 Hit / 隐藏 / 切招时由上层调用。</summary>
    public void StopAdditive() => playback?.StopAdditive();

    /// <summary>将当前招式 Clip 跳到指定时间（秒）；仅切段对时使用，勿每逻辑帧调用。</summary>
    public void SeekClip(float timeSeconds) => playback?.Seek(timeSeconds);

    /// <summary>
    /// 按权威归一化时间对齐当前 Locomotion Clip。
    /// 循环片对 1 取模，避免 Seek 被夹到片尾；Evaluate 在 Seek 内完成，调用方不必再 Tick。
    /// </summary>
    public void SeekLocomotionNormalized(float normalizedTime)
    {
        if (playback == null || !playback.IsValid || playback.CurrentClip == null)
            return;

        AnimationClip clip = playback.CurrentClip;
        float wrapped = normalizedTime;
        if (clip.isLooping && wrapped >= 0f)
            wrapped -= Mathf.Floor(wrapped);
        if (wrapped < 0f)
            wrapped = 0f;

        float length = clip.length;
        if (length <= 0.0001f)
            return;

        playback.Seek(wrapped * length);
    }

    public bool HasFinishedClip(AnimationClip clip)
    {
        if (playback == null || clip == null)
            return true;

        return playback.CurrentClip == clip && playback.HasFinished;
    }

    /// <summary>按逻辑步推进 Playable 时间与淡入；Native RootMotion 的 delta 由此 Evaluate 产生。</summary>
    public void Tick(float deltaTime) => playback?.Tick(deltaTime);

    /// <summary>销毁播放后端（PlayableGraph 等）。</summary>
    public void Dispose() => playback?.Dispose();
}
