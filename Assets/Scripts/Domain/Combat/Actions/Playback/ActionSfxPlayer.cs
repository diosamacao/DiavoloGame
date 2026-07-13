using UnityEngine;

/// <summary>
/// 订阅统一 ActionNotify 时间轴；在 PlaySfxNotify 触发帧按音量与显式倍率播放一次音效。
/// </summary>
public sealed class ActionSfxPlayer : IActionNotifyConsumer
{
    readonly AudioSource audioSource;

    /// <summary>使用角色根上的 AudioSource；缺失时自动添加。</summary>
    public ActionSfxPlayer(Transform actorRoot)
    {
        if (actorRoot == null)
            return;

        audioSource = actorRoot.GetComponent<AudioSource>();
        if (audioSource != null)
            return;

        audioSource = actorRoot.gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
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

    /// <summary>招式结束；OneShot 无需额外清理。</summary>
    public void OnActionEnded() { }
}
