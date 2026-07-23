using UnityEngine;

/// <summary>
/// 订阅统一 ActionNotify 时间轴；在 PlaySfxNotify 触发帧按音量与显式倍率播放音效。
/// 招式结束或被打断时停止本源上的动作音效（与脚步声 AudioSource 隔离）。
/// </summary>
public sealed class ActionSfxPlayer : IActionNotifyConsumer
{
    const string SourceObjectName = "ActionSfx";

    readonly AudioSource audioSource;

    /// <summary>在角色根下使用专用 ActionSfx AudioSource；缺失时自动创建。</summary>
    public ActionSfxPlayer(Transform actorRoot)
    {
        if (actorRoot == null)
            return;

        audioSource = ResolveOrCreateSource(actorRoot);
    }

    /// <summary>点事件触发：按 playbackSpeed 映射 pitch 后 PlayOneShot。</summary>
    public void OnActionNotify(in ActionNotifyContext context)
    {
        if (audioSource == null)
            return;

        if (context.Notify is not PlaySfxNotify sfx || sfx.AudioClip == null)
            return;

        float previousPitch = audioSource.pitch;
        audioSource.pitch = sfx.PlaybackSpeed;
        audioSource.PlayOneShot(sfx.AudioClip, sfx.Volume);
        audioSource.pitch = previousPitch;
    }

    /// <summary>SFX 为点事件，不消费区间窗口。</summary>
    public void OnActionNotifyState(in ActionNotifyContext context) { }

    /// <summary>招式结束 / TransitionTo 打断：停止本源上未播完的动作音效。</summary>
    public void OnActionEnded()
    {
        if (audioSource == null)
            return;

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
                return source;
        }

        var go = new GameObject(SourceObjectName);
        go.transform.SetParent(actorRoot, false);
        AudioSource created = go.AddComponent<AudioSource>();
        created.playOnAwake = false;
        created.spatialBlend = 0f;
        return created;
    }
}
