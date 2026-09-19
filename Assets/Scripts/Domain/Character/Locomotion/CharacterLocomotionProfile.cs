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
    static readonly AnimationKey[] RootMotionKeys =
    {
        AnimationKey.StartEnd,
        AnimationKey.StopL,
        AnimationKey.StopR,
        AnimationKey.PivotTurn,
    };

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
    [Tooltip("Gait 下松手后仍保持当前步态的宽限逻辑帧；用于键盘换向空窗。")]
    [SerializeField, Min(0)] int gaitInputGapGraceFrames = 9;
    [Tooltip("Cardinal 死区；与 DirectionModel 共用。")]
    [SerializeField, Min(0.01f)] float cardinalEpsilon = LocomotionDirectionModel.DefaultEpsilon;
    [Tooltip("Gait 循环 Cardinal 最短驻留逻辑帧；防对角线微抖换片。")]
    [SerializeField, Min(0)] int cardinalMinDwellFrames = 3;
    [Header("Gait Policy")]
    [Tooltip("步态升档 / Pivot / Sprint 计时；敌我挂不同配置，勿在代码里按身份分支。")]
    [SerializeField] LocomotionGaitPolicy gaitPolicy = new LocomotionGaitPolicy();
    [Tooltip("朝向策略：玩家 FollowMove；对峙敌 FaceCamera。值对齐旧 GaitRotationMode 以迁移资产。")]
    [FormerlySerializedAs("gaitRotationMode")]
    [SerializeField] LocomotionFacingMode facingMode = LocomotionFacingMode.FollowMove;
    [SerializeField] float interruptFadeDuration = 0.08f;

    [Header("Integer Clip Timing (60Hz)")]
    [Tooltip("每个实际使用的 AnimationKey 必须有且仅有一条时序；由人工 Baker 写入。")]
    [SerializeField] LocomotionClipTiming[] clipTimings = Array.Empty<LocomotionClipTiming>();

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

    /// <summary>Cardinal 死区。</summary>
    public float CardinalEpsilon => Mathf.Max(0.01f, cardinalEpsilon);

    /// <summary>Gait Cardinal 滞回最短驻留帧。</summary>
    public int CardinalMinDwellFrames => Mathf.Max(0, cardinalMinDwellFrames);

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

    /// <summary>Run→Sprint 逻辑帧数（真源在 GaitPolicy）。</summary>
    public int SprintAfterRunFrames => GaitPolicy.SprintAfterRunFrames;

    /// <summary>Gait 无输入宽限逻辑帧。</summary>
    public int GaitInputGapGraceFrames => Mathf.Max(0, gaitInputGapGraceFrames);

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

    /// <summary>严格查找 AnimationKey 的整数帧时序；缺失或重复均返回 false。</summary>
    public bool TryGetClipTiming(AnimationKey key, out LocomotionClipTiming timing)
    {
        timing = default;
        int matches = 0;
        LocomotionClipTiming[] entries = clipTimings ?? Array.Empty<LocomotionClipTiming>();
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].Key != key)
                continue;
            timing = entries[i];
            matches++;
        }

        return matches == 1 && timing.IsValid;
    }

    /// <summary>取得必需时序；资产未迁移或配置非法时立即失败，禁止运行时默认值。</summary>
    public LocomotionClipTiming RequireClipTiming(AnimationKey key)
    {
        if (TryGetClipTiming(key, out LocomotionClipTiming timing))
            return timing;

        throw new InvalidOperationException(
            $"CharacterLocomotionProfile「{name}」缺少唯一有效的 {key} LocomotionClipTiming；请运行人工 Timing Baker。");
    }

#if UNITY_EDITOR
    /// <summary>仅供人工 Editor Baker 整体写入时序；运行时不得调用。</summary>
    public void SetClipTimings(LocomotionClipTiming[] timings) =>
        clipTimings = timings ?? Array.Empty<LocomotionClipTiming>();
#endif

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

    /// <summary>严格校验动画映射与整数帧时序；缺失数据不得使用默认值启动。</summary>
    public bool Validate(UnityEngine.Object context)
    {
        string requestedBy = context != null ? context.name : "(unknown)";
        if (animationProfile == null)
        {
            Debug.LogError(
                $"CharacterLocomotionProfile: '{name}' 未配置 AnimationProfile（引用方：'{requestedBy}'）。",
                this);
            return false;
        }

        gaitPolicy ??= new LocomotionGaitPolicy();
        bool valid = animationProfile.ValidateClips(this);
        foreach (AnimationKey key in Enum.GetValues(typeof(AnimationKey)))
        {
            if (key == AnimationKey.HitShake
                || !animationProfile.TryGetClip(key, out AnimationClip clip)
                || clip == null)
            {
                continue;
            }
            valid &= ValidateRequiredTiming(key, requestedBy);
        }

        for (int i = 0; i < RootMotionKeys.Length; i++)
        {
            AnimationKey key = RootMotionKeys[i];
            if (!IsRootMotionEnabled(key)
                || !animationProfile.TryGetClip(key, out AnimationClip clip)
                || clip == null
                || GetRootMotionTrack(key).IsValid)
            {
                continue;
            }

            Debug.LogError(
                $"CharacterLocomotionProfile: '{name}' 的 {key} 已启用根运动，"
                + $"但 Clip '{clip.name}' 对应烘焙轨无效（frameCount={GetRootMotionTrack(key).FrameCount}，"
                + $"AnimationProfile='{animationProfile.name}'，引用方='{requestedBy}'）。",
                this);
            valid = false;
        }
        return valid;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        gaitPolicy ??= new LocomotionGaitPolicy();
        sprintLean ??= new SprintLeanSettings();
        animSet ??= LocomotionAnimSet.CreateDefault();
    }
#endif

    /// <summary>校验必需键恰有一条有效整数帧时序。</summary>
    bool ValidateRequiredTiming(AnimationKey key, string requestedBy)
    {
        if (TryGetClipTiming(key, out _))
            return true;

        animationProfile.TryGetClip(key, out AnimationClip clip);
        Debug.LogError(
            $"CharacterLocomotionProfile: '{name}' 的 {key} Clip '{(clip != null ? clip.name : "(missing)")}' "
            + $"缺少唯一有效 LocomotionClipTiming（AnimationProfile='{animationProfile.name}'，"
            + $"引用方='{requestedBy}'），请先运行 Timing Baker。",
            this);
        return false;
    }
}

/// <summary>单条落脚标记：当前 AnimationKey 周期内的整数逻辑帧。</summary>
[Serializable]
public struct FootPlantMarker
{
    [Min(0)] public int frame;
    public FootSide foot;

    /// <summary>周期内触发帧。</summary>
    public int Frame => frame;

    /// <summary>触发脚。</summary>
    public FootSide Foot => foot;

}
