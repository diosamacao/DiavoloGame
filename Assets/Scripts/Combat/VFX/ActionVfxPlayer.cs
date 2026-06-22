using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 订阅 ActionExecutor Logic Tick，在 ActionDefinition 配置的 triggerFrame 实例化 VFX Prefab。
/// </summary>
public sealed class ActionVfxPlayer : ICombatFrameConsumer
{
    readonly Transform root;
    readonly Transform attachPoint;

    /// <summary>VFX 局部变换挂点；为空时使用本物体 Transform。</summary>
    public Transform AttachPoint => attachPoint;

    readonly HashSet<int> _firedEventIndices = new();
    ActionDefinition _trackedAction;
    int _lastSampledFrame = -1;

    /// <summary>创建纯 C# VFX 帧消费者。</summary>
    public ActionVfxPlayer(Transform actorRoot, Transform vfxAttachPoint)
    {
        root = actorRoot;
        attachPoint = vfxAttachPoint != null ? vfxAttachPoint : actorRoot;
    }

    /// <summary>新招式开始：重置 VFX 触发记录。</summary>
    public void OnActionBegan(ActionDefinition action)
    {
        ResetTracking(action);
    }

    /// <summary>Logic Tick 帧推进：检测应触发的 VFX 帧事件。</summary>
    public void OnCombatFrameAdvanced(in CombatFrameContext context)
    {
        if (context.Action == null)
            return;

        ResetTrackingIfActionChanged(context.Action);

        ActionVfxKeyframe[] events = context.Action.VfxEvents;
        if (events.Length == 0)
        {
            _lastSampledFrame = context.FrameIndex;
            return;
        }

        for (int i = 0; i < events.Length; i++)
        {
            ActionVfxKeyframe vfxEvent = events[i];
            if (vfxEvent == null || vfxEvent.Prefab == null)
                continue;

            if (_firedEventIndices.Contains(i))
                continue;

            if (!vfxEvent.ShouldFireBetweenFrames(_lastSampledFrame, context.FrameIndex))
                continue;

            ActionVfxSpawner.Spawn(vfxEvent.Prefab, root, attachPoint, vfxEvent);
            _firedEventIndices.Add(i);
        }

        _lastSampledFrame = context.FrameIndex;
    }

    /// <summary>招式结束：清空触发记录。</summary>
    public void OnActionEnded()
    {
        ResetTracking(null);
    }

    void ResetTrackingIfActionChanged(ActionDefinition action)
    {
        if (_trackedAction == action)
            return;

        ResetTracking(action);
    }

    void ResetTracking(ActionDefinition action)
    {
        _trackedAction = action;
        _firedEventIndices.Clear();
        _lastSampledFrame = -1;
    }
}
