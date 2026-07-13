using System;
using UnityEngine;

/// <summary>播放 SFX 的点事件；在触发帧播放一次，pitch 由显式播放倍率控制。</summary>
[Serializable]
public class PlaySfxNotify : ActionNotify
{
    [SerializeField] AudioClip audioClip = null;
    [SerializeField, Range(0f, 1f)] float volume = 1f;
    [Tooltip("AudioSource.pitch 倍率；1 = 原速。")]
    [SerializeField] float playbackSpeed = 1f;

    /// <summary>触发时播放的音效。</summary>
    public AudioClip AudioClip => audioClip;

    /// <summary>播放音量。</summary>
    public float Volume => Mathf.Clamp01(volume);

    /// <summary>播放倍率（映射为 pitch）。</summary>
    public float PlaybackSpeed => Mathf.Max(0.0001f, playbackSpeed);
}
