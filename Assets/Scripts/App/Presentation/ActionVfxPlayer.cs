using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 订阅统一 ActionNotify 时间轴；在 PlayVfxNotify 触发帧生成实例，并按显式倍率驱动粒子与 Animator。
/// 普通切招保持自然生命周期；角色隐藏前强制回收其租约，禁止随父节点冻结后再次显形。
/// </summary>
public sealed class ActionVfxPlayer : IActionNotifyConsumer, IActionVisibilityResetConsumer
{
    readonly Transform root;
    readonly CharacterAttachPointResolver attachPoints;
    readonly List<GameObject> spawnedInstances = new();

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

        spawnedInstances.Add(instance);
        ActionVfxPlayback.ApplyPlaybackSpeed(instance, vfx.PlaybackSpeed);

        // 无 VFXManager 时无自动回池，按时长 Destroy，避免连招后泄漏。
        if (!VFXManager.TryGetInstance(out _))
            ScheduleFallbackDestroy(instance, vfx);
    }

    /// <summary>VFX 已改为点事件，不再消费区间窗口。</summary>
    public void OnActionNotifyState(in ActionNotifyContext context) { }

    /// <summary>招式结束；不强制 Despawn，交由 VfxPooledInstance 按自然生命周期回收。</summary>
    public void OnActionEnded() { }

    /// <summary>角色隐藏前只回收仍属于该角色的实例，防止父节点停用冻结后在下次登场复活。</summary>
    public void ResetForVisibilityLoss()
    {
        bool hasManager = VFXManager.TryGetInstance(out VFXManager manager);
        for (int i = spawnedInstances.Count - 1; i >= 0; i--)
        {
            GameObject instance = spawnedInstances[i];
            if (instance == null)
                continue;

            VfxPooledInstance pooled = instance.GetComponent<VfxPooledInstance>();
            if (pooled != null && !pooled.IsOwnedBy(root))
                continue;

            if (hasManager && pooled != null)
                manager.Despawn(instance);
            else
                Object.Destroy(instance);
        }
        spawnedInstances.Clear();
    }

    /// <summary>无对象池回退路径：按 Prefab 自然时长（粒子/Animator）/ playbackSpeed 延时销毁。</summary>
    static void ScheduleFallbackDestroy(GameObject instance, PlayVfxNotify vfx)
    {
        float naturalSeconds = ActionVfxPlayback.EstimateNaturalDurationSeconds(vfx.Prefab);
        float speed = Mathf.Max(0.0001f, vfx.PlaybackSpeed);
        Object.Destroy(instance, naturalSeconds / speed);
    }
}
