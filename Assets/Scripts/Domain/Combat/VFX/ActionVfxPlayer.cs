using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 订阅统一 ActionNotify 时间轴；在 PlayVfxNotify 触发帧生成实例，并按显式倍率驱动粒子。
/// </summary>
public sealed class ActionVfxPlayer : IActionNotifyConsumer
{
    readonly Transform root;
    readonly CharacterAttachPointResolver attachPoints;
    readonly List<GameObject> _spawnedInstances = new();

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
        _spawnedInstances.Add(instance);
    }

    /// <summary>VFX 已改为点事件，不再消费区间窗口。</summary>
    public void OnActionNotifyState(in ActionNotifyContext context) { }

    /// <summary>招式结束时清理本招生成的实例，避免跨招残留。</summary>
    public void OnActionEnded() => ClearSpawnedInstances();

    /// <summary>销毁/回收全部本消费者生成的实例。</summary>
    public void ClearSpawnedInstances()
    {
        for (int i = 0; i < _spawnedInstances.Count; i++)
            DespawnInstance(_spawnedInstances[i]);

        _spawnedInstances.Clear();
    }

    static void DespawnInstance(GameObject instance)
    {
        if (instance == null)
            return;

        if (VFXManager.TryGetInstance(out VFXManager manager))
            manager.Despawn(instance);
        else
            Object.Destroy(instance);
    }
}
