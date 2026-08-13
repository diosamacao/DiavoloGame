using System;
using UnityEngine;

/// <summary>动作图节点是否消费角色 SelectedTarget，以及转向平滑覆盖。</summary>
[Serializable]
public class TargetLockSettings
{
    [SerializeField] bool enabled = false;
    [Tooltip("<=0 时使用 RotationNotifyState 的 smoothTime；值越小转向越快。")]
    [SerializeField] float lockRotationSmoothTimeOverride = 0f;

    /// <summary>该动作节点是否使用当前 SelectedTarget。</summary>
    public bool Enabled => enabled;

    /// <summary>节点索敌转向平滑覆盖；非正值表示使用旋转窗口值。</summary>
    public float LockRotationSmoothTimeOverride => lockRotationSmoothTimeOverride;

    /// <summary>索敌转向平滑时间；未覆盖时回退 rotationStateSmoothTime。</summary>
    public float ResolveLockSmoothTime(float rotationStateSmoothTime) =>
        lockRotationSmoothTimeOverride > 0f ? lockRotationSmoothTimeOverride : rotationStateSmoothTime;
}
