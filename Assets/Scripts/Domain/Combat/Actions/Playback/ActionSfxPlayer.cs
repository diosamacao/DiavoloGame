using UnityEngine;

/// <summary>
/// 订阅统一 ActionNotify 时间轴；在 PlaySfxNotify 触发帧播放动作音效。
/// 招式结束/连招打断时由多声道驱动在 0.1s 内淡出旧音，不打断新招起手音。
/// </summary>
public sealed class ActionSfxPlayer : IActionNotifyConsumer
{
    const string SourceObjectName = "ActionSfx";
    /// <summary>打断/结束时音量淡出到 0 的时长（秒，unscaled）。</summary>
    const float InterruptFadeOutSeconds = 0.1f;

    readonly ActionSfxFadeDriver fadeDriver;

    /// <summary>在角色根下使用专用 ActionSfx 多声道；缺失时自动创建。</summary>
    public ActionSfxPlayer(Transform actorRoot)
    {
        if (actorRoot == null)
            return;

        AudioSource template = ResolveOrCreateTemplateSource(actorRoot);
        fadeDriver = template != null
            ? template.GetComponent<ActionSfxFadeDriver>()
            : null;
        fadeDriver?.Initialize(template);
    }

    /// <summary>点事件触发：在空闲声道播放，不取消其他声道的淡出。</summary>
    public void OnActionNotify(in ActionNotifyContext context)
    {
        if (fadeDriver == null)
            return;

        if (context.Notify is not PlaySfxNotify sfx || sfx.AudioClip == null)
            return;

        fadeDriver.Play(sfx.AudioClip, sfx.Volume, sfx.PlaybackSpeed);
    }

    /// <summary>SFX 为点事件，不消费区间窗口。</summary>
    public void OnActionNotifyState(in ActionNotifyContext context) { }

    /// <summary>招式结束 / TransitionTo 打断：所有仍在播的声道 0.1s 淡出。</summary>
    public void OnActionEnded()
    {
        fadeDriver?.BeginFadeOutAll(InterruptFadeOutSeconds);
    }

    /// <summary>复用或创建挂在角色根下的模板 AudioSource，并挂上淡出驱动。</summary>
    static AudioSource ResolveOrCreateTemplateSource(Transform actorRoot)
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
