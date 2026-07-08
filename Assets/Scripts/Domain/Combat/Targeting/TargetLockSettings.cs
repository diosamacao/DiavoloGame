using System;
using UnityEngine;

/// <summary>单招索敌配置：范围、扇形与选择策略。</summary>
[Serializable]
public class TargetLockSettings
{
    [SerializeField] bool enabled = false;
    [SerializeField] float lockRange = 8f;
    [Tooltip("0 表示全向索敌；>0 时仅 NearestInForwardCone 或作为额外过滤角。")]
    [SerializeField] float forwardConeAngle = 120f;
    [SerializeField] TargetSelectionPolicy policy = TargetSelectionPolicy.NearestDistance;
    [Tooltip("<=0 时使用 RotationNotifyState 的 smoothTime；值越小转向越快。")]
    [SerializeField] float lockRotationSmoothTimeOverride = 0f;

    public bool Enabled => enabled;
    public float LockRange => Mathf.Max(0f, lockRange);
    public float ForwardConeAngle => Mathf.Clamp(forwardConeAngle, 0f, 360f);
    public TargetSelectionPolicy Policy => policy;

    /// <summary>是否启用前方扇形过滤（全向时不过滤）。</summary>
    public bool UsesForwardConeFilter =>
        policy == TargetSelectionPolicy.NearestInForwardCone && ForwardConeAngle > 0f && ForwardConeAngle < 360f;

    /// <summary>索敌转向平滑时间；未覆盖时回退 rotationStateSmoothTime。</summary>
    public float ResolveLockSmoothTime(float rotationStateSmoothTime) =>
        lockRotationSmoothTimeOverride > 0f ? lockRotationSmoothTimeOverride : rotationStateSmoothTime;
}
