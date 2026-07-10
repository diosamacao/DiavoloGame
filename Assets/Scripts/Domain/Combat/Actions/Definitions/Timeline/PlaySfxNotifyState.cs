using System;
using UnityEngine;

/// <summary>
/// 播放 SFX 的区间窗口；窗口时长可拖拽，播放倍率 = 自然时长 / 窗口时长。
/// </summary>
[Serializable]
public class PlaySfxNotifyState : ActionNotifyState
{
    [SerializeField] AudioClip audioClip = null;
    [SerializeField, Range(0f, 1f)] float volume = 1f;
    [Tooltip("AudioClip 自然时长（秒）；对应倍率 1.0。为 0 时按 Clip.length 或倍率 1。")]
    [SerializeField] float naturalDurationSeconds;

    /// <summary>进入窗口时播放的音效。</summary>
    public AudioClip AudioClip => audioClip;

    /// <summary>播放音量。</summary>
    public float Volume => Mathf.Clamp01(volume);

    /// <summary>资源自然时长（秒）；优先用缓存，否则回退 Clip.length。</summary>
    public float NaturalDurationSeconds
    {
        get
        {
            if (naturalDurationSeconds > 0f)
                return naturalDurationSeconds;

            return audioClip != null ? Mathf.Max(0f, audioClip.length) : 0f;
        }
    }

    /// <summary>按采样率换算当前窗口占用秒数。</summary>
    public float GetWindowDurationSeconds(float sampleRate)
    {
        float rate = sampleRate > 0f ? sampleRate : 30f;
        int frameCount = Mathf.Max(1, EndFrame - StartFrame + 1);
        return frameCount / rate;
    }

    /// <summary>
    /// 播放倍率：自然时长 / 窗口时长；未配置自然时长时返回 1。
    /// </summary>
    public float GetPlaybackSpeed(float sampleRate)
    {
        float natural = NaturalDurationSeconds;
        if (natural <= 0f)
            return 1f;

        return natural / Mathf.Max(GetWindowDurationSeconds(sampleRate), 0.0001f);
    }

    /// <summary>写入缓存的自然时长；编辑器从 AudioClip 解析后调用。</summary>
    public void SetNaturalDurationSeconds(float seconds) =>
        naturalDurationSeconds = Mathf.Max(0f, seconds);
}
