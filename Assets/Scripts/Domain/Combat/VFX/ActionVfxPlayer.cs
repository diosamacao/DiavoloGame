using UnityEngine;

/// <summary>
/// 订阅统一 ActionNotify 时间轴；在 PlayVfxNotify 触发帧生成实例，并按显式倍率驱动粒子。
/// 特效生命周期由池化实例自行管理，招式切换时不强制回收。
/// </summary>
public sealed class ActionVfxPlayer : IActionNotifyConsumer
{
    readonly Transform root;
    readonly CharacterAttachPointResolver attachPoints;

    /// <summary>创建纯 C# VFX 点事件消费者。</summary>
    public ActionVfxPlayer(Transform actorRoot, CharacterAttachPointResolver attachPointResolver)
    {
        root = actorRoot;
        attachPoints = attachPointResolver;
    }

    /// <summary>点事件触发：解析挂点、生成 VFX、应用播放倍率。</summary>
    public void OnActionNotify(in ActionNotifyContext context)
    {
        if (context.Notify is not PlayVfxNotify vfx || vfx.Prefab == null)
            return;

        Transform anchor = attachPoints != null
            ? attachPoints.Resolve(vfx.AttachPointId)
            : root;
        GameObject instance = ActionVfxSpawner.Spawn(vfx.Prefab, root, anchor, vfx);
        if (instance == null)
            return;

        ActionVfxPlayback.ApplyPlaybackSpeed(instance, vfx.PlaybackSpeed);

        // 无 VFXManager 时无自动回池，按时长 Destroy，避免连招后泄漏。
        if (!VFXManager.TryGetInstance(out _))
            ScheduleFallbackDestroy(instance, vfx);
    }

    /// <summary>VFX 已改为点事件，不再消费区间窗口。</summary>
    public void OnActionNotifyState(in ActionNotifyContext context) { }

    /// <summary>招式结束；不强制 Despawn，交由 VfxPooledInstance 按自然生命周期回收。</summary>
    public void OnActionEnded() { }

    /// <summary>无对象池回退路径：按 Prefab 自然时长 / playbackSpeed 延时销毁。</summary>
    static void ScheduleFallbackDestroy(GameObject instance, PlayVfxNotify vfx)
    {
        float naturalSeconds = ActionVfxPlayback.EstimateNaturalDurationSeconds(vfx.Prefab);
        float speed = Mathf.Max(0.0001f, vfx.PlaybackSpeed);
        Object.Destroy(instance, naturalSeconds / speed);
    }
}
