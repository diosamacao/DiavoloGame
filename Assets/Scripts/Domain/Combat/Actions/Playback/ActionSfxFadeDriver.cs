using System.Collections;
using UnityEngine;

/// <summary>
/// 动作音效多声道播放与打断淡出。
/// 连招切招时旧声道独立 0.1s 淡出，新 OneShot 走空闲声道，互不 Cancel。
/// </summary>
[DisallowMultipleComponent]
public sealed class ActionSfxFadeDriver : MonoBehaviour
{
    const int VoiceCount = 3;

    AudioSource[] _voices;
    Coroutine[] _fadeRoutines;
    float _restVolume = 1f;

    /// <summary>在 ActionSfx 根下创建多个 AudioSource 声道。</summary>
    public void Initialize(AudioSource template)
    {
        if (template == null)
            return;

        _restVolume = Mathf.Max(0.0001f, template.volume <= 0f ? 1f : template.volume);
        _voices = new AudioSource[VoiceCount];
        _fadeRoutines = new Coroutine[VoiceCount];

        // 声道 0 复用已有 ActionSfx 上的 AudioSource
        _voices[0] = template;
        ConfigureVoice(template);

        for (int i = 1; i < VoiceCount; i++)
        {
            var go = new GameObject($"ActionSfxVoice_{i}");
            go.transform.SetParent(transform, false);
            AudioSource voice = go.AddComponent<AudioSource>();
            ConfigureVoice(voice);
            voice.volume = _restVolume;
            _voices[i] = voice;
        }
    }

    /// <summary>在空闲（或非淡出）声道上播放；不打断正在淡出的旧声道。</summary>
    public void Play(AudioClip clip, float volumeScale, float pitch)
    {
        if (clip == null || _voices == null)
            return;

        int index = FindPlayableVoiceIndex();
        AudioSource voice = _voices[index];
        // 若选中的声道仍在淡出，先硬停再播（只发生在声道耗尽时）
        StopFade(index);
        voice.Stop();
        voice.volume = _restVolume;
        voice.pitch = Mathf.Max(0.0001f, pitch);
        // 用 Play() 而非 PlayOneShot：音量可按声道淡出，isPlaying 可靠
        voice.clip = clip;
        voice.loop = false;
        voice.Play();
        // volumeScale 映射为相对静息音量
        voice.volume = _restVolume * Mathf.Clamp01(volumeScale);
    }

    /// <summary>对所有正在播放且未在淡出的声道启动淡出。</summary>
    public void BeginFadeOutAll(float durationSeconds)
    {
        if (_voices == null)
            return;

        float duration = Mathf.Max(0.0001f, durationSeconds);
        for (int i = 0; i < _voices.Length; i++)
        {
            AudioSource voice = _voices[i];
            if (voice == null)
                continue;
            // 已在淡出则保持
            if (_fadeRoutines[i] != null)
                continue;
            if (!voice.isPlaying)
                continue;

            _fadeRoutines[i] = StartCoroutine(FadeOutVoice(i, duration));
        }
    }

    int FindPlayableVoiceIndex()
    {
        // 优先：空闲且未淡出
        for (int i = 0; i < _voices.Length; i++)
        {
            if (_fadeRoutines[i] != null)
                continue;
            if (_voices[i] != null && !_voices[i].isPlaying)
                return i;
        }

        // 其次：未淡出但可抢占（同帧叠音）
        for (int i = 0; i < _voices.Length; i++)
        {
            if (_fadeRoutines[i] == null)
                return i;
        }

        // 全部在淡出：抢占 0
        return 0;
    }

    IEnumerator FadeOutVoice(int index, float durationSeconds)
    {
        AudioSource voice = _voices[index];
        float startVolume = voice.volume;
        float elapsed = 0f;
        while (elapsed < durationSeconds && voice != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / durationSeconds);
            voice.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        if (voice != null)
        {
            voice.Stop();
            voice.clip = null;
            voice.volume = _restVolume;
        }

        _fadeRoutines[index] = null;
    }

    void StopFade(int index)
    {
        if (_fadeRoutines == null || index < 0 || index >= _fadeRoutines.Length)
            return;
        if (_fadeRoutines[index] == null)
            return;

        StopCoroutine(_fadeRoutines[index]);
        _fadeRoutines[index] = null;
    }

    static void ConfigureVoice(AudioSource voice)
    {
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        voice.loop = false;
    }

    void OnDisable()
    {
        if (_voices == null)
            return;

        for (int i = 0; i < _voices.Length; i++)
        {
            StopFade(i);
            if (_voices[i] != null)
            {
                _voices[i].Stop();
                _voices[i].volume = _restVolume;
            }
        }
    }
}
