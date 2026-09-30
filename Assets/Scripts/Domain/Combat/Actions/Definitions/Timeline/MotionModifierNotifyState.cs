using System;
using UnityEngine;

/// <summary>
/// 位移修正区间窗口：SoftBody 抑制或 TargetAdhesion。
/// Adhesion：捕获时固定偏移方向，窗内重映射基础位移节奏，末帧收敛到目标落点。
/// </summary>
[Serializable]
public class MotionModifierNotifyState : ActionNotifyState
{
    [SerializeField] MotionModifierMode mode = MotionModifierMode.TargetAdhesion;
    [SerializeField] MotionTargetSource targetSource = MotionTargetSource.SelectedTarget;

    [Tooltip("沿捕获时玩家→敌人连线的偏移（毫米）；方向固定，>0 连线远侧，=0 敌心，<0 连线近侧。")]
    [SerializeField] int horizontalOffsetMm = 1000;

    [Tooltip("沿连线法线的侧向偏移（毫米）。")]
    [SerializeField] int lateralOffsetMm = 0;

    [Tooltip("中间帧相对基础位移的修正上限（毫米）；末帧为保证落点不受此上限限制。过小可能导致末帧突变。")]
    [SerializeField] int maxCorrectionMmPerFrame = 250;

    [Tooltip("首次捕获允许的最大平面距离（毫米）；捕获后穿过敌人不会重新检查。0=不限制。")]
    [SerializeField] int maxAcquireDistanceMm = 4500;

    [Tooltip("连线与角色朝向夹角上限（毫度）；0 表示不限制。")]
    [SerializeField] int maxAngleMilliDeg = 0;

    [Tooltip("目标丢失时停止修正；关闭后继续向最后已知落点收敛。")]
    [SerializeField] bool stopOnTargetLost = true;

    /// <summary>修正模式。</summary>
    public MotionModifierMode Mode => mode;

    /// <summary>目标来源。</summary>
    public MotionTargetSource TargetSource => targetSource;

    /// <summary>连线水平偏移（毫米）。</summary>
    public int HorizontalOffsetMm => horizontalOffsetMm;

    /// <summary>连线侧向偏移（毫米）。</summary>
    public int LateralOffsetMm => lateralOffsetMm;

    /// <summary>中间帧修正上限（毫米，至少 1）；末帧精确收敛优先。</summary>
    public int MaxCorrectionMmPerFrame => Mathf.Max(1, maxCorrectionMmPerFrame);

    /// <summary>最大捕获距离（毫米）。</summary>
    public int MaxAcquireDistanceMm => Mathf.Max(0, maxAcquireDistanceMm);

    /// <summary>最大夹角（毫度）；0=不限制。</summary>
    public int MaxAngleMilliDeg => Mathf.Max(0, maxAngleMilliDeg);

    /// <summary>目标丢失时是否停止修正。</summary>
    public bool StopOnTargetLost => stopOnTargetLost;
}
