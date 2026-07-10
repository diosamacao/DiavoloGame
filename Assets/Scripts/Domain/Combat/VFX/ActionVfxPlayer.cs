using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 订阅统一 ActionNotify 时间轴；在 PlayVfxNotify 窗口 Enter 时生成、Exit 时回收，并按倍率驱动粒子。
/// </summary>
public sealed class ActionVfxPlayer : IActionNotifyConsumer
{
    readonly Transform root;
    readonly Transform attachPoint;
    readonly Dictionary<PlayVfxNotify, GameObject> _activeInstances = new();

    /// <summary>VFX 局部变换挂点；为空时使用本物体 Transform。</summary>
    public Transform AttachPoint => attachPoint;

    /// <summary>创建纯 C# VFX 帧消费者。</summary>
    public ActionVfxPlayer(Transform actorRoot, Transform vfxAttachPoint)
    {
        root = actorRoot;
        attachPoint = vfxAttachPoint != null ? vfxAttachPoint : actorRoot;
    }

    /// <summary>VFX 已改为区间窗口，不再消费点事件。</summary>
    public void OnActionNotify(in ActionNotifyContext context) { }

    /// <summary>区间 Enter 生成、Tick 对齐变换、Exit 回收。</summary>
    public void OnActionNotifyState(in ActionNotifyContext context)
    {
        if (context.State is not PlayVfxNotify vfx || vfx.Prefab == null)
            return;

        switch (context.StatePhase)
        {
            case ActionNotifyStatePhase.Enter:
                EnterVfx(vfx, in context);
                break;
            case ActionNotifyStatePhase.Tick:
                TickVfx(vfx, in context);
                break;
            case ActionNotifyStatePhase.Exit:
                ExitVfx(vfx);
                break;
        }
    }

    /// <summary>招式结束时清理仍存活的窗口实例，避免跨招残留。</summary>
    public void OnActionEnded() => ClearActiveInstances();

    /// <summary>清理全部活跃 VFX 实例。</summary>
    public void ClearActiveInstances()
    {
        foreach (KeyValuePair<PlayVfxNotify, GameObject> pair in _activeInstances)
            DespawnInstance(pair.Value);

        _activeInstances.Clear();
    }

    void EnterVfx(PlayVfxNotify vfx, in ActionNotifyContext context)
    {
        if (_activeInstances.TryGetValue(vfx, out GameObject existing) && existing != null)
            DespawnInstance(existing);

        GameObject instance = ActionVfxSpawner.Spawn(vfx.Prefab, root, attachPoint, vfx);
        if (instance == null)
            return;

        float sampleRate = context.Action != null ? context.Action.SampleRate : 30f;
        ActionVfxPlayback.ApplyPlaybackSpeed(instance, vfx.GetPlaybackSpeed(sampleRate));
        _activeInstances[vfx] = instance;
    }

    void TickVfx(PlayVfxNotify vfx, in ActionNotifyContext context)
    {
        if (!_activeInstances.TryGetValue(vfx, out GameObject instance) || instance == null)
            return;

        // 挂点子物体随 Pose 更新；世界空间生成则保持进入时姿态。
        if (vfx.ParentToAttachPoint)
            ActionVfxSpawner.ApplyTransform(instance.transform, attachPoint, vfx);

        float sampleRate = context.Action != null ? context.Action.SampleRate : 30f;
        ActionVfxPlayback.ApplyPlaybackSpeed(instance, vfx.GetPlaybackSpeed(sampleRate));
    }

    void ExitVfx(PlayVfxNotify vfx)
    {
        if (!_activeInstances.TryGetValue(vfx, out GameObject instance))
            return;

        _activeInstances.Remove(vfx);
        DespawnInstance(instance);
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
