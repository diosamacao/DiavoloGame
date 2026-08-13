using System;
using UnityEngine;

/// <summary>
/// 位移修正区间窗口：SoftBody 抑制或 TargetAdhesion。
/// Adhesion：desired = enemy + normalize(enemy−player)*horizontalOffset；按剩余帧均摊。
/// </summary>
[Serializable]
public class MotionModifierNotifyState : ActionNotifyState
{
    [SerializeField] MotionModifierMode mode = MotionModifierMode.TargetAdhesion;
    [SerializeField] MotionTargetSource targetSource = MotionTargetSource.SelectedTarget;

    [Tooltip("沿玩家→敌人连线、相对敌人中心的水平偏移（毫米）。>0 穿到敌后侧，=0 敌心，<0 敌前。")]
    [SerializeField] int horizontalOffsetMm = 1000;

    [Tooltip("沿连线法线的侧向偏移（毫米）。")]
    [SerializeField] int lateralOffsetMm = 0;

    [Tooltip("单帧修正上限（毫米）；主节奏由窗口剩余帧均摊决定。")]
    [SerializeField] int maxCorrectionMmPerFrame = 250;

    [Tooltip("玩家到敌人平面距离超过此值（毫米）则本帧不吸。")]
    [SerializeField] int maxAcquireDistanceMm = 4500;

    [Tooltip("连线与角色朝向夹角上限（毫度）；0 表示不限制。")]
    [SerializeField] int maxAngleMilliDeg = 0;

    [SerializeField] bool stopOnTargetLost = true;

    /// <summary>修正模式。</summary>
    public MotionModifierMode Mode => mode;

    /// <summary>目标来源。</summary>
    public MotionTargetSource TargetSource => targetSource;

    /// <summary>连线水平偏移（毫米）。</summary>
    public int HorizontalOffsetMm => horizontalOffsetMm;

    /// <summary>连线侧向偏移（毫米）。</summary>
    public int LateralOffsetMm => lateralOffsetMm;

    /// <summary>单帧修正上限（毫米，至少 1）。</summary>
    public int MaxCorrectionMmPerFrame => Mathf.Max(1, maxCorrectionMmPerFrame);

    /// <summary>最大捕获距离（毫米）。</summary>
    public int MaxAcquireDistanceMm => Mathf.Max(0, maxAcquireDistanceMm);

    /// <summary>最大夹角（毫度）；0=不限制。</summary>
    public int MaxAngleMilliDeg => Mathf.Max(0, maxAngleMilliDeg);

    /// <summary>目标丢失时是否停止修正。</summary>
    public bool StopOnTargetLost => stopOnTargetLost;
}
