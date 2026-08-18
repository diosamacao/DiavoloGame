using UnityEngine;

/// <summary>消费 FootCycle 落脚事件并 PlayOneShot；无 AudioClip 时静默跳过。</summary>
public sealed class LocomotionFootstepPlayer
{
    readonly AudioSource _audioSource;
    readonly CharacterLocomotionProfile _profile;

    /// <summary>使用角色根 AudioSource；缺失时自动添加。</summary>
    public LocomotionFootstepPlayer(Transform actorRoot, CharacterLocomotionProfile profile)
    {
        _profile = profile;
        _audioSource = actorRoot.GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = actorRoot.gameObject.AddComponent<AudioSource>();
    }

    /// <summary>无 AudioSource 的静默脚步；Headless Authority 使用。</summary>
    public static LocomotionFootstepPlayer CreateSilent() => new();

    LocomotionFootstepPlayer()
    {
        _profile = null;
        _audioSource = null;
    }

    /// <summary>若本帧有落脚则播放对应脚步音。</summary>
    public void PlayIfPlanted(FootSide? planted)
    {
        if (planted == null || _profile == null || _audioSource == null)
            return;

        AudioClip clip = _profile.GetFootstepClip(planted.Value);
        if (clip == null)
            return;

        _audioSource.PlayOneShot(clip, _profile.FootstepVolume);
    }
}
