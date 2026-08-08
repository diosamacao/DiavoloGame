using System.Collections;
using UnityEngine;

/// <summary>
/// 动作音效淡出驱动：挂在 ActionSfx 子物体上，供 ActionSfxPlayer 在打断时渐弱而非硬 Stop。
/// </summary>
[DisallowMultipleComponent]
public sealed class ActionSfxFadeDriver : MonoBehaviour
{
    AudioSource _source;
    float _restVolume = 1f;
    Coroutine _fadeRoutine;

    /// <summary>是否正在淡出。</summary>
    public bool IsFading => _fadeRoutine != null;

    /// <summary>绑定要控制的 AudioSource，并记录恢复音量。</summary>
    public void Initialize(AudioSource source)
    {
        _source = source;
        if (_source != null)
            _restVolume = Mathf.Max(0.0001f, _source.volume <= 0f ? 1f : _source.volume);
    }

    /// <summary>在 durationSeconds 内将音量降到 0，然后 Stop 并恢复音量供下次播放。</summary>
    public void BeginFadeOut(float durationSeconds)
    {
        if (_source == null)
            return;

        CancelFadeKeepVolume();
        // 已无在播内容时无需淡出
        if (!_source.isPlaying)
            return;

        float duration = Mathf.Max(0.0001f, durationSeconds);
        _fadeRoutine = StartCoroutine(FadeOutRoutine(duration));
    }

    /// <summary>新招起手：取消淡出并把音量恢复到静息值，避免 OneShot 被压哑。</summary>
    public void CancelFadeAndRestore()
    {
        CancelFadeKeepVolume();
        if (_source != null)
            _source.volume = _restVolume;
    }

    void CancelFadeKeepVolume()
    {
        if (_fadeRoutine == null)
            return;

        StopCoroutine(_fadeRoutine);
        _fadeRoutine = null;
    }

    /// <summary>用 unscaled 时间淡出，避免逻辑卡肉/timeScale 拉长体感。</summary>
    IEnumerator FadeOutRoutine(float durationSeconds)
    {
        float startVolume = _source.volume;
        float elapsed = 0f;
        while (elapsed < durationSeconds && _source != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / durationSeconds);
            _source.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        if (_source != null)
        {
            _source.Stop();
            _source.volume = _restVolume;
        }

        _fadeRoutine = null;
    }

    void OnDisable()
    {
        CancelFadeKeepVolume();
        if (_source != null)
            _source.volume = _restVolume;
    }
}
