using UnityEngine;

/// <summary>
/// 订阅统一 ActionNotify 时间轴；在 PlaySfxNotify 触发帧按音量与显式倍率播放音效。
/// 招式结束或被打断时对动作音效做短时淡出（与脚步声 AudioSource 隔离）。
/// </summary>
public sealed class ActionSfxPlayer : IActionNotifyConsumer
{
    const string SourceObjectName = "ActionSfx";
    /// <summary>打断/结束时音量淡出到 0 的时长（秒，unscaled）。</summary>
    const float InterruptFadeOutSeconds = 0.1f;

    readonly AudioSource audioSource;
    readonly ActionSfxFadeDriver fadeDriver;

    /// <summary>在角色根下使用专用 ActionSfx AudioSource；缺失时自动创建。</summary>
    public ActionSfxPlayer(Transform actorRoot)
    {
        if (actorRoot == null)
            return;

        audioSource = ResolveOrCreateSource(actorRoot);
        fadeDriver = audioSource != null
            ? audioSource.GetComponent<ActionSfxFadeDriver>()
            : null;
        fadeDriver?.Initialize(audioSource);
    }

    /// <summary>点事件触发：取消进行中的淡出，按 playbackSpeed 映射 pitch 后 PlayOneShot。</summary>
    public void OnActionNotify(in ActionNotifyContext context)
    {
        if (audioSource == null)
            return;

        if (context.Notify is not PlaySfxNotify sfx || sfx.AudioClip == null)
            return;

        // 新 OneShot 前恢复音量，避免承接打断淡出的低音量
        fadeDriver?.CancelFadeAndRestore();

        float previousPitch = audioSource.pitch;
        audioSource.pitch = sfx.PlaybackSpeed;
        audioSource.PlayOneShot(sfx.AudioClip, sfx.Volume);
        audioSource.pitch = previousPitch;
    }

    /// <summary>SFX 为点事件，不消费区间窗口。</summary>
    public void OnActionNotifyState(in ActionNotifyContext context) { }

    /// <summary>招式结束 / TransitionTo 打断：0.1s 内将动作音效音量淡出到 0 再 Stop。</summary>
    public void OnActionEnded()
    {
        if (audioSource == null)
            return;

        if (fadeDriver != null)
        {
            fadeDriver.BeginFadeOut(InterruptFadeOutSeconds);
            return;
        }

        audioSource.Stop();
    }

    /// <summary>复用或创建挂在角色根下的专用 AudioSource，避免 Stop 误杀脚步声。</summary>
    static AudioSource ResolveOrCreateSource(Transform actorRoot)
    {
        Transform existing = actorRoot.Find(SourceObjectName);
        if (existing != null)
        {
            AudioSource source = existing.GetComponent<AudioSource>();
            if (source != null)
            {
                EnsureFadeDriver(existing.gameObject);
                return source;
            }
        }

        var go = new GameObject(SourceObjectName);
        go.transform.SetParent(actorRoot, false);
        AudioSource created = go.AddComponent<AudioSource>();
        created.playOnAwake = false;
        created.spatialBlend = 0f;
        EnsureFadeDriver(go);
        return created;
    }

    static void EnsureFadeDriver(GameObject host)
    {
        if (host.GetComponent<ActionSfxFadeDriver>() == null)
            host.AddComponent<ActionSfxFadeDriver>();
    }
}
