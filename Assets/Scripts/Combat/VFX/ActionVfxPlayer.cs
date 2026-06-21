using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 订阅 ActionRuntimeController Logic Tick，在 ActionDefinition 配置的 triggerFrame 实例化 VFX Prefab。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ActionRuntimeController))]
public class ActionVfxPlayer : MonoBehaviour, ICombatFrameConsumer
{
    [SerializeField] ActionRuntimeController actionRuntime = null!;
    [Tooltip("VFX 局部变换挂点；为空时使用本物体 Transform。可与 HitBoxSystem.attachPoint 相同。")]
    [SerializeField] Transform attachPoint = null;

    /// <summary>VFX 局部变换挂点；为空时使用本物体 Transform。</summary>
    public Transform AttachPoint => attachPoint;

    readonly HashSet<int> _firedEventIndices = new();
    ActionDefinition _trackedAction;
    int _lastSampledFrame = -1;

    void Awake()
    {
        if (actionRuntime == null)
            actionRuntime = GetComponent<ActionRuntimeController>();
    }

    /// <summary>绑定运行时与默认挂点，供 CharacterConfig 统一装配。</summary>
    public void Bind(ActionRuntimeController runtime, Transform vfxAttachPoint)
    {
        actionRuntime = runtime;
        attachPoint = vfxAttachPoint;
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

        Transform root = transform;
        Transform anchor = attachPoint != null ? attachPoint : root;

        for (int i = 0; i < events.Length; i++)
        {
            ActionVfxKeyframe vfxEvent = events[i];
            if (vfxEvent == null || vfxEvent.Prefab == null)
                continue;

            if (_firedEventIndices.Contains(i))
                continue;

            if (!vfxEvent.ShouldFireBetweenFrames(_lastSampledFrame, context.FrameIndex))
                continue;

            ActionVfxSpawner.Spawn(vfxEvent.Prefab, root, anchor, vfxEvent);
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
