using System;
using UnityEngine;

/// <summary>Locomotion 相位参数、落脚标记与脚步音配置（与 AnimationProfile 分离）。</summary>
[CreateAssetMenu(fileName = "CharacterLocomotionProfile", menuName = "ACT/Character/Locomotion Profile")]
public class CharacterLocomotionProfile : ScriptableObject
{
    [Header("Thresholds")]
    [SerializeField] float idleInputThreshold = 0.01f;
    [SerializeField] float stopMinSpeedFactor = 0.5f;
    [Tooltip("Sprint 下与输入方向夹角达到该值触发转身（度）；对齐 zzzdemo turnBackAngle。")]
    [SerializeField] float pivotAngleDegrees = 135f;
    [Tooltip("若转身 Clip 本身含 Y 转向：保持 false（全程锁根，由 Clip 表现转向）。仅当 Clip 始终朝前、靠代码转根时才开 true（zzzdemo ReturnRun）。")]
    [SerializeField] bool pivotRootFollowsInput = false;
    [Tooltip("pivotRootFollowsInput 时：前段不转根的归一化时间。")]
    [SerializeField, Range(0f, 1f)] float pivotLockNormalizedTime = 0.08f;
    [Tooltip("pivotRootFollowsInput 时：锁定期后跟输入的 SmoothDamp 时间。")]
    [SerializeField] float pivotRotationSmoothTime = 0.5f;
    [Tooltip("在 Run 步态下连续保持跑输入达到该秒数后进入 Sprint。")]
    [SerializeField] float sprintAfterRunSeconds = 3f;
    [Tooltip("Gait 下松手后仍保持当前步态的宽限秒数；用于键盘换向空窗，避免立刻 Stop 导致无法 Pivot。")]
    [SerializeField] float gaitInputGapGraceSeconds = 0.15f;
    [SerializeField, Range(0f, 1f)] float startToGaitNormalized = 1f;
    [SerializeField, Range(0f, 1f)] float stopCancelNormalized = 0.4f;
    [SerializeField] float interruptFadeDuration = 0.08f;

    [Header("Foot Plants")]
    [SerializeField] FootPlantMarker[] walkFootPlants = Array.Empty<FootPlantMarker>();
    [SerializeField] FootPlantMarker[] runFootPlants = Array.Empty<FootPlantMarker>();
    [SerializeField] FootPlantMarker[] sprintFootPlants = Array.Empty<FootPlantMarker>();
    [SerializeField] FootPlantMarker[] startFootPlants = Array.Empty<FootPlantMarker>();

    [Header("Footstep Audio")]
    [SerializeField] AudioClip footstepLeft;
    [SerializeField] AudioClip footstepRight;
    [SerializeField, Range(0f, 1f)] float footstepVolume = 1f;

    public float IdleInputThreshold => idleInputThreshold;
    public float StopMinSpeedFactor => stopMinSpeedFactor;
    public float PivotAngleDegrees => pivotAngleDegrees;
    public bool PivotRootFollowsInput => pivotRootFollowsInput;
    public float PivotLockNormalizedTime => pivotLockNormalizedTime;
    public float PivotRotationSmoothTime => pivotRotationSmoothTime;
    public float SprintAfterRunSeconds => sprintAfterRunSeconds;
    public float GaitInputGapGraceSeconds => gaitInputGapGraceSeconds;
    public float StartToGaitNormalized => startToGaitNormalized;
    public float StopCancelNormalized => stopCancelNormalized;
    public float InterruptFadeDuration => interruptFadeDuration;
    public float FootstepVolume => footstepVolume;

    public FootPlantMarker[] WalkFootPlants => walkFootPlants ?? Array.Empty<FootPlantMarker>();
    public FootPlantMarker[] RunFootPlants => runFootPlants ?? Array.Empty<FootPlantMarker>();
    public FootPlantMarker[] SprintFootPlants => sprintFootPlants ?? Array.Empty<FootPlantMarker>();
    public FootPlantMarker[] StartFootPlants => startFootPlants ?? Array.Empty<FootPlantMarker>();

    public AudioClip FootstepLeft => footstepLeft;
    public AudioClip FootstepRight => footstepRight;

    /// <summary>按步态取落脚表；Sprint 未配置时回退 Run。</summary>
    public FootPlantMarker[] GetGaitFootPlants(LocomotionGait gait)
    {
        switch (gait)
        {
            case LocomotionGait.Sprint:
                return SprintFootPlants.Length > 0 ? SprintFootPlants : RunFootPlants;
            case LocomotionGait.Run:
                return RunFootPlants;
            default:
                return WalkFootPlants;
        }
    }

    /// <summary>按脚取脚步音；缺省时左右互相回退。</summary>
    public AudioClip GetFootstepClip(FootSide foot)
    {
        if (foot == FootSide.Left)
            return footstepLeft != null ? footstepLeft : footstepRight;
        return footstepRight != null ? footstepRight : footstepLeft;
    }
}

/// <summary>单条落脚标记：相对循环 Clip 一周期的归一化时间。</summary>
[Serializable]
public struct FootPlantMarker
{
    [Range(0f, 1f)] public float normalizedTime;
    public FootSide foot;

    public float NormalizedTime => normalizedTime;
    public FootSide Foot => foot;
}
