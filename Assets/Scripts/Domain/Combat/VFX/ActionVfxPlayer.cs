using UnityEngine;

/// <summary>订阅统一 ActionNotify 时间轴，在 PlayVfxNotify 触发时实例化 VFX Prefab。</summary>
public sealed class ActionVfxPlayer : IActionNotifyConsumer
{
    readonly Transform root;
    readonly Transform attachPoint;

    /// <summary>VFX 局部变换挂点；为空时使用本物体 Transform。</summary>
    public Transform AttachPoint => attachPoint;

    /// <summary>创建纯 C# VFX 帧消费者。</summary>
    public ActionVfxPlayer(Transform actorRoot, Transform vfxAttachPoint)
    {
        root = actorRoot;
        attachPoint = vfxAttachPoint != null ? vfxAttachPoint : actorRoot;
    }

    /// <summary>点事件触发：仅处理 PlayVfxNotify，其他点事件交给对应 Consumer。</summary>
    public void OnActionNotify(in ActionNotifyContext context)
    {
        if (context.Notify is not PlayVfxNotify vfx || vfx.Prefab == null)
            return;

        ActionVfxSpawner.Spawn(vfx.Prefab, root, attachPoint, vfx);
    }

    /// <summary>VFX 当前不消费区间窗口事件。</summary>
    public void OnActionNotifyState(in ActionNotifyContext context) { }
}
