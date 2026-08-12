using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 一套 Locomotion 完整配置：Clip 映射（AnimationProfile）+ AnimSet + 相位/落脚/脚步/烘焙轨。
/// 由 CombatMode 挂载；不再在 CharacterConfig 上单独配置。
/// </summary>
[CreateAssetMenu(fileName = "CharacterLocomotionProfile", menuName = "ACT/Character/Locomotion Profile")]
public class CharacterLocomotionProfile : ScriptableObject
{
    [Header("Animation")]
    [Tooltip("Idle/Walk/Run 等 Clip 映射；必填。")]
    [SerializeField] CharacterAnimationProfile animationProfile = null;
    [Tooltip("选片表（gait×cardinal→Key）；默认左右槽=WalkLeft/Right，沿用现有 AnimationProfile。")]
    [SerializeField] LocomotionAnimSet animSet = new LocomotionAnimSet();

    [Header("Thresholds")]
    [SerializeField] float idleInputThreshold = 0.01f;
    [SerializeField] float stopMinSpeedFactor = 0.5f;
    [Tooltip("Sprint 下与输入方向夹角达到该值触发转身（度）；对齐 zzzdemo turnBackAngle。")]
    [SerializeField] float pivotAngleDegrees = 135f;
    [Tooltip("PivotTurn AnimAuth 占比：NormalizedTime 达此值后切 InputAuth（FollowInput）。")]
    [SerializeField, Range(0f, 1f)] float pivotAnimAuthNormalized = 0.5f;
    [Tooltip("Gait 下松手后仍保持当前步态的宽限秒数；用于键盘换向空窗，避免立刻 Stop 导致无法 Pivot。")]
    [SerializeField] float gaitInputGapGraceSeconds = 0.15f;
    [Tooltip("Cardinal 死区；与 DirectionModel 共用。")]
    [SerializeField, Min(0.01f)] float cardinalEpsilon = LocomotionDirectionModel.DefaultEpsilon;
    [Tooltip("Gait 循环 Cardinal 最短驻留逻辑帧；防对角线微抖换片。")]
    [SerializeField, Min(0)] int cardinalMinDwellFrames = 3;
    [Tooltip("L-DIR3 软锁半径（米）；>0 时 FollowMove 靠近敌人可升格 FaceTarget。0=仅动作索敌锁。")]
    [SerializeField, Min(0f)] float softFocusRadiusMeters = 12f;

    [Header("Gait Policy")]
    [Tooltip("步态升档 / Pivot / Sprint 计时；敌我挂不同配置，勿在代码里按身份分支。")]
    [SerializeField] LocomotionGaitPolicy gaitPolicy = new LocomotionGaitPolicy();
    [Tooltip("朝向策略：玩家 FollowMove；对峙敌 FaceCamera。值对齐旧 GaitRotationMode 以迁移资产。")]
    [FormerlySerializedAs("gaitRotationMode")]
    [SerializeField] LocomotionFacingMode facingMode = LocomotionFacingMode.FollowMove;
    [SerializeField, Range(0f, 1f)] float startToGaitNormalized = 1f;
    [SerializeField] float interruptFadeDuration = 0.08f;

    [Header("Sprint Lean (L-DIR4)")]
    [Tooltip("疾跑转弯视觉倾身；敌人对峙建议 MaxLeanDeg=0。")]
    [SerializeField] SprintLeanSettings sprintLean = new SprintLeanSettings();

    [Header("Foot Plants")]
    [SerializeField] FootPlantMarker[] walkFootPlants = Array.Empty<FootPlantMarker>();
    [SerializeField] FootPlantMarker[] runFootPlants = Array.Empty<FootPlantMarker>();
    [SerializeField] FootPlantMarker[] sprintFootPlants = Array.Empty<FootPlantMarker>();
    [SerializeField] FootPlantMarker[] startFootPlants = Array.Empty<FootPlantMarker>();

    [Header("Footstep Audio")]
    [SerializeField] AudioClip footstepLeft = null;
    [SerializeField] AudioClip footstepRight = null;
    [SerializeField, Range(0f, 1f)] float footstepVolume = 1f;

    [Header("Root Motion Bake (Stop / Pivot)")]
    [Tooltip("急停（含 StartEnd）是否使用烘焙根位移。")]
    [SerializeField] bool stopUseRootMotion = true;
    [Tooltip("转身是否使用烘焙根位移（AnimAuth 段强制吃烘焙偏航）。")]
    [SerializeField] bool pivotUseRootMotion = true;
    [SerializeField] float rootMotionPositionScale = 1f;
    [SerializeField] LocomotionRootMotionTrack startEndRootMotion;
    [SerializeField] LocomotionRootMotionTrack stopLRootMotion;
    [SerializeField] LocomotionRootMotionTrack stopRRootMotion;
    [SerializeField] LocomotionRootMotionTrack pivotTurnRootMotion;

    /// <summary>本套 Locomotion 的 Clip 映射。</summary>
    public CharacterAnimationProfile AnimationProfile => animationProfile;

    /// <summary>选片真源；空则默认表。</summary>
    public LocomotionAnimSet AnimSet => animSet ??= LocomotionAnimSet.CreateDefault();

    public float IdleInputThreshold => idleInputThreshold;
    public float StopMinSpeedFactor => stopMinSpeedFactor;
    public float PivotAngleDegrees => pivotAngleDegrees;

    /// <summary>PivotTurn 前半 AnimAuth 归一化门槛；之后 InputAuth。</summary>
    public float PivotAnimAuthNormalized => Mathf.Clamp01(pivotAnimAuthNormalized);

    /// <summary>Cardinal 死区。</summary>
    public float CardinalEpsilon => Mathf.Max(0.01f, cardinalEpsilon);

    /// <summary>Gait Cardinal 滞回最短驻留帧。</summary>
    public int CardinalMinDwellFrames => Mathf.Max(0, cardinalMinDwellFrames);

    /// <summary>软锁最近敌对半径；≤0 关闭软锁。</summary>
    public float SoftFocusRadiusMeters => Mathf.Max(0f, softFocusRadiusMeters);

    /// <summary>步态策略（MaxGait / Pivot / Sprint 秒）；空则回退默认玩家策略。</summary>
    public LocomotionGaitPolicy GaitPolicy => gaitPolicy ??= new LocomotionGaitPolicy();

    /// <summary>配置朝向策略（L-DIR1）；运行时有效模式见 Context.ResolveFacingMode。</summary>
    public LocomotionFacingMode FacingMode
    {
        get
        {
            if (facingMode == LocomotionFacingMode.FaceCamera)
                return LocomotionFacingMode.FaceCamera;
            if (facingMode == LocomotionFacingMode.FaceTarget)
                return LocomotionFacingMode.FaceTarget;
            return LocomotionFacingMode.FollowMove;
        }
    }

    /// <summary>将有效 FacingMode 映射为 Motor 旋转模式。</summary>
    public static LocomotionRotationMode ToMotorRotationMode(LocomotionFacingMode mode)
    {
        switch (mode)
        {
            case LocomotionFacingMode.FaceCamera:
                return LocomotionRotationMode.FaceCamera;
            case LocomotionFacingMode.FaceTarget:
                return LocomotionRotationMode.FaceTarget;
            default:
                return LocomotionRotationMode.FollowInput;
        }
    }

    /// <summary>Run→Sprint 秒数（真源在 GaitPolicy）。</summary>
    public float SprintAfterRunSeconds => GaitPolicy.SprintAfterRunSeconds;

    public float GaitInputGapGraceSeconds => gaitInputGapGraceSeconds;
    public float StartToGaitNormalized => startToGaitNormalized;
    public float InterruptFadeDuration => interruptFadeDuration;
    public float FootstepVolume => footstepVolume;

    /// <summary>Sprint 倾身设置；空则回退默认（启用小倾角）。</summary>
    public SprintLeanSettings SprintLean => sprintLean ??= new SprintLeanSettings();

    public FootPlantMarker[] WalkFootPlants => walkFootPlants ?? Array.Empty<FootPlantMarker>();
    public FootPlantMarker[] RunFootPlants => runFootPlants ?? Array.Empty<FootPlantMarker>();
    public FootPlantMarker[] SprintFootPlants => sprintFootPlants ?? Array.Empty<FootPlantMarker>();
    public FootPlantMarker[] StartFootPlants => startFootPlants ?? Array.Empty<FootPlantMarker>();

    public AudioClip FootstepLeft => footstepLeft;
    public AudioClip FootstepRight => footstepRight;

    public bool StopUseRootMotion => stopUseRootMotion;
    public bool PivotUseRootMotion => pivotUseRootMotion;
    public float RootMotionPositionScale => rootMotionPositionScale;

    /// <summary>按 AnimationKey 取烘焙根位移轨。</summary>
    public LocomotionRootMotionTrack GetRootMotionTrack(AnimationKey key)
    {
        switch (key)
        {
            case AnimationKey.StartEnd:
                return startEndRootMotion;
            case AnimationKey.StopL:
                return stopLRootMotion;
            case AnimationKey.StopR:
                return stopRRootMotion;
            case AnimationKey.PivotTurn:
                return pivotTurnRootMotion;
            default:
                return LocomotionRootMotionTrack.Empty;
        }
    }

    /// <summary>该键是否启用根位移驱动。</summary>
    public bool IsRootMotionEnabled(AnimationKey key)
    {
        switch (key)
        {
            case AnimationKey.StartEnd:
            case AnimationKey.StopL:
            case AnimationKey.StopR:
                return stopUseRootMotion;
            case AnimationKey.PivotTurn:
                return pivotUseRootMotion;
            default:
                return false;
        }
    }

    /// <summary>写入烘焙轨（仅 Editor 烘焙工具调用）。</summary>
    public void SetRootMotionTrack(AnimationKey key, LocomotionRootMotionTrack track)
    {
        switch (key)
        {
            case AnimationKey.StartEnd:
                startEndRootMotion = track;
                break;
            case AnimationKey.StopL:
                stopLRootMotion = track;
                break;
            case AnimationKey.StopR:
                stopRRootMotion = track;
                break;
            case AnimationKey.PivotTurn:
                pivotTurnRootMotion = track;
                break;
        }
    }

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

    /// <summary>校验已挂 AnimationProfile 且含 Idle/Walk/Run。</summary>
    public bool Validate(UnityEngine.Object context)
    {
        if (animationProfile == null)
        {
            Debug.LogError("CharacterLocomotionProfile: AnimationProfile 未配置。", context != null ? context : this);
            return false;
        }

        gaitPolicy ??= new LocomotionGaitPolicy();
        return animationProfile.ValidateClips(context != null ? context : this);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        gaitPolicy ??= new LocomotionGaitPolicy();
        sprintLean ??= new SprintLeanSettings();
        animSet ??= LocomotionAnimSet.CreateDefault();
    }
#endif
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
