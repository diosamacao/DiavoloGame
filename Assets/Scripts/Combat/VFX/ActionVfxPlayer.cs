using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 读取 ActionRuntimeController 当前帧，在 ActionDefinition 配置的 triggerFrame 实例化 VFX Prefab。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ActionRuntimeController))]
public class ActionVfxPlayer : MonoBehaviour
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

    void LateUpdate()
    {
        // 在 ActionRuntimeController.Tick 之后采样，保证帧索引与招式逻辑同步。
        if (actionRuntime == null || !actionRuntime.IsPlaying || actionRuntime.CurrentAction == null)
        {
            ResetTracking(null);
            return;
        }

        ActionDefinition action = actionRuntime.CurrentAction;
        ResetTrackingIfActionChanged(action);

        int currentFrame = actionRuntime.CurrentFrame;
        ActionVfxKeyframe[] events = action.VfxEvents;
        if (events.Length == 0)
        {
            _lastSampledFrame = currentFrame;
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

            if (!vfxEvent.ShouldFireBetweenFrames(_lastSampledFrame, currentFrame))
                continue;

            ActionVfxSpawner.Spawn(vfxEvent.Prefab, root, anchor, vfxEvent);
            _firedEventIndices.Add(i);
        }

        _lastSampledFrame = currentFrame;
    }

    /// <summary>切换或停止招式时清空触发记录，避免跨招误判。</summary>
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
