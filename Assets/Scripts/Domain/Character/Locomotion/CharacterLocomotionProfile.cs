using System;
using UnityEngine;

/// <summary>
/// 一套 Locomotion 完整配置：Clip 映射（AnimationProfile）+ 相位/落脚/脚步/烘焙轨。
/// 由 CombatMode 挂载；不再在 CharacterConfig 上单独配置。
/// </summary>
[CreateAssetMenu(fileName = "CharacterLocomotionProfile", menuName = "ACT/Character/Locomotion Profile")]
public class CharacterLocomotionProfile : ScriptableObject
{
    [Header("Animation")]
    [Tooltip("Idle/Walk/Run 等 Clip 映射；必填。")]
    [SerializeField] CharacterAnimationProfile animationProfile = null;

    [Header("Thresholds")]
    [SerializeField] float idleInputThreshold = 0.01f;
    [SerializeField] float stopMinSpeedFactor = 0.5f;
    [Tooltip("Sprint 下与输入方向夹角达到该值触发转身（度）；对齐 zzzdemo turnBackAngle。")]
    [SerializeField] float pivotAngleDegrees = 135f;
    [Tooltip("TurnBack 起手锁定角色根朝向的秒数；到时后由实时玩家输入接管朝向。")]
    [SerializeField, Min(0f)] float pivotInputUnlockSeconds = 0.08f;
    [Tooltip("TurnBack 解锁后跟随实时输入方向的 SmoothDamp 时间。")]
    [SerializeField] float pivotRotationSmoothTime = 0.5f;
    [Tooltip("Gait 下松手后仍保持当前步态的宽限秒数；用于键盘换向空窗，避免立刻 Stop 导致无法 Pivot。")]
    [SerializeField] float gaitInputGapGraceSeconds = 0.15f;

    [Header("Gait Policy")]
    [Tooltip("步态升档 / Pivot / Sprint 计时；敌我挂不同配置，勿在代码里按身份分支。")]
    [SerializeField] LocomotionGaitPolicy gaitPolicy = new LocomotionGaitPolicy();
    [Tooltip("Start/Gait 移动时的朝向：玩家 FollowInput；八向对峙敌用 FaceCamera（锁假相机前向）。")]
    [SerializeField] LocomotionRotationMode gaitRotationMode = LocomotionRotationMode.FollowInput;
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
    [Tooltip("转身是否使用烘焙根位移。")]
    [SerializeField] bool pivotUseRootMotion = true;
    [Tooltip("转身是否应用烘焙偏航；Clip 已含骨骼转向时保持 false。")]
    [SerializeField] bool pivotApplyRootYaw = false;
    [SerializeField] float rootMotionPositionScale = 1f;
    [SerializeField] LocomotionRootMotionTrack startEndRootMotion;
    [SerializeField] LocomotionRootMotionTrack stopLRootMotion;
    [SerializeField] LocomotionRootMotionTrack stopRRootMotion;
    [SerializeField] LocomotionRootMotionTrack pivotTurnRootMotion;

    /// <summary>本套 Locomotion 的 Clip 映射。</summary>
    public CharacterAnimationProfile AnimationProfile => animationProfile;

    public float IdleInputThreshold => idleInputThreshold;
    public float StopMinSpeedFactor => stopMinSpeedFactor;
    public float PivotAngleDegrees => pivotAngleDegrees;
    /// <summary>TurnBack 起手锁根秒数；到时后实时玩家输入接管朝向。</summary>
    public float PivotInputUnlockSeconds => Mathf.Max(0f, pivotInputUnlockSeconds);
    public float PivotRotationSmoothTime => pivotRotationSmoothTime;

    /// <summary>步态策略（MaxGait / Pivot / Sprint 秒）；空则回退默认玩家策略。</summary>
    public LocomotionGaitPolicy GaitPolicy => gaitPolicy ??= new LocomotionGaitPolicy();

    /// <summary>Start/Gait 移动朝向模式（Hold/PivotTarget 无效时回退 FollowInput）。</summary>
    public LocomotionRotationMode GaitRotationMode
    {
        get
        {
            // 仅允许移动态合法模式，避免误配 Hold 导致永不转面
            if (gaitRotationMode == LocomotionRotationMode.FaceCamera)
                return LocomotionRotationMode.FaceCamera;
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
    public bool PivotApplyRootYaw => pivotApplyRootYaw;
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
