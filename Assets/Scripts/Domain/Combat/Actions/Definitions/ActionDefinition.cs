using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>招式数据：多段动画、统一时间轴、自动 Transition 与命中反馈默认参数。</summary>
[CreateAssetMenu(fileName = "ActionDefinition", menuName = "ACT/Combat/Action Definition")]
public class ActionDefinition : ScriptableObject
{
    [HideInInspector]
    [SerializeField] AnimationClip animationClip = null;

    [Header("Trigger")]
    [Tooltip("进入/派生到本招所需的设备无关玩法意图；Cancel 与动作图边按此枚举匹配。")]
    [SerializeField] GameplayIntentType trigger = GameplayIntentType.None;

    [Header("Animation")]
    [Tooltip("按顺序播放的动画段；totalFrames 由各段有效帧累加。")]
    [SerializeField] ActionAnimationSegment[] animationSegments = Array.Empty<ActionAnimationSegment>();

    [SerializeField] float sampleRate = 30f;
    [SerializeField] int totalFrames;
    [SerializeField] CombatActionType actionType = CombatActionType.Attack;
    [SerializeField] float crossFadeDuration = 0.1f;

    [Header("Interrupt")]
    [Tooltip("招式打断优先级；更大则可硬打断更小者。同级不互打断，连招 Cancel 不受此限制。")]
    [SerializeField] int interruptPriority = 0;

    [Header("Damage")]
    [Tooltip("招式基础伤害；最终伤害还会乘当前 Hitbox 的 Damage Weight。")]
    [SerializeField] float baseDamage = 10f;

    [Header("Timeline")]
    [Tooltip("动作帧数据唯一真源：Phase、点事件与其它区间窗口均从此处读取。")]
    [SerializeField] ActionTimeline timeline = new();

    [Header("Transitions")]
    [SerializeField] ActionTransition[] transitions = Array.Empty<ActionTransition>();

    [Header("Start Behaviors")]
    [SerializeField] ActionStartBehaviorType[] startBehaviors = Array.Empty<ActionStartBehaviorType>();

    [Header("Combat Mode Switch")]
    [Tooltip("Start Behaviors 含 SwitchCombatMode 时生效。")]
    [SerializeField] CombatModeType switchCombatModeTarget = CombatModeType.Default;
    [SerializeField] CombatModeSwitchPolicy switchCombatModePolicy = CombatModeSwitchPolicy.Immediate;

    [Header("Camera Shake")]
    [Tooltip("命中时使用的镜头震动预设；为空则按 ActionType 使用 CameraShakeController 默认配置。")]
    [SerializeField] CameraShakeProfile cameraShakeProfile = null;
    [Tooltip("勾选后禁止该招式触发镜头震动。")]
    [SerializeField] bool disableCameraShakeOnHit = false;
    [Tooltip("开启镜头震动；新资产默认 true。旧资产缺此字段时 Attack 仍默认开启。")]
    [SerializeField] bool useCameraShakeOnHit = true;

    [Header("Hit Stop")]
    [Tooltip("命中时触发卡肉（顿帧）；hitStopFrames > 0 时生效。")]
    [SerializeField] bool useHitStopOnHit = true;
    [Tooltip("勾选后禁止该招式触发卡肉。")]
    [SerializeField] bool disableHitStopOnHit = false;
    [Tooltip("卡肉持续逻辑帧数（与 sampleRate 对齐）。")]
    [SerializeField] int hitStopFrames = 3;
    [Tooltip("勾选后每招仅第一次命中触发卡肉。")]
    [SerializeField] bool hitStopOncePerAction = true;

    [Header("Target Lock")]
    [Tooltip("攻击旋转窗口内的自动索敌配置。")]
    [SerializeField] TargetLockSettings targetLockSettings = new();

    [Header("Movement")]
    [Tooltip("开启时由动画 Root Motion 驱动位移，脚本位移（Displacement Distance）将被忽略。")]
    [SerializeField] bool useRootMotion = true;

    /// <summary>本招所需的设备无关玩法意图。</summary>
    public GameplayIntentType Trigger => trigger;

    /// <summary>顺序动画段；运行时与编辑器均只认此列表。</summary>
    public ActionAnimationSegment[] AnimationSegments =>
        animationSegments ?? Array.Empty<ActionAnimationSegment>();

    /// <summary>是否至少有一段绑定了 Clip。</summary>
    public bool HasAnimation
    {
        get
        {
            ActionAnimationSegment[] segments = AnimationSegments;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].clip != null)
                    return true;
            }

            return false;
        }
    }

    /// <summary>首段 AnimationClip；兼容旧调用方的「主 Clip」查询。</summary>
    public AnimationClip AnimationClip
    {
        get
        {
            ActionAnimationSegment[] segments = AnimationSegments;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].clip != null)
                    return segments[i].clip;
            }

            return null;
        }
    }

    /// <summary>逻辑采样率；所有时间轴帧都按此值换算。</summary>
    public float SampleRate => sampleRate > 0f ? sampleRate : 30f;

    /// <summary>动作总逻辑帧数（各段有效帧之和）。</summary>
    public int TotalFrames => totalFrames;

    /// <summary>动作类型，用于反馈默认值和上层分类。</summary>
    public CombatActionType ActionType => actionType;

    /// <summary>招式打断优先级；高优可经 Entry 硬打断低优（严格大于）。</summary>
    public int InterruptPriority => interruptPriority;

    /// <summary>招式基础伤害；非正值表示该招不造成生命值伤害。</summary>
    public float BaseDamage => Mathf.Max(0f, baseDamage);

    /// <summary>切入招式首段时的默认淡入时长；段可自带 crossFadeDuration 覆盖。</summary>
    public float CrossFadeDuration => crossFadeDuration;

    /// <summary>是否由动画 RootMotion 驱动位移。</summary>
    public bool UseRootMotion => useRootMotion;

    /// <summary>动作帧数据唯一真源：点事件与区间窗口均从此处读取。</summary>
    public ActionTimeline Timeline => timeline ?? new ActionTimeline();

    /// <summary>动作开始时执行的副作用列表。</summary>
    public ActionStartBehaviorType[] StartBehaviors => startBehaviors ?? Array.Empty<ActionStartBehaviorType>();

    /// <summary>StartBehavior 含 SwitchCombatMode 时的目标战斗模式。</summary>
    public CombatModeType SwitchCombatModeTarget => switchCombatModeTarget;

    /// <summary>StartBehavior 含 SwitchCombatMode 时的切换策略。</summary>
    public CombatModeSwitchPolicy SwitchCombatModePolicy => switchCombatModePolicy;

    /// <summary>是否存在非 RootMotion 的脚本位移窗口。</summary>
    public bool HasScriptedDisplacement => !useRootMotion && Timeline.HasScriptedMovement;

    /// <summary>索敌配置；未配置时返回默认空配置。</summary>
    public TargetLockSettings TargetLockSettings => targetLockSettings ?? new TargetLockSettings();

    /// <summary>动作是否启用起手索敌。</summary>
    public bool HasTargetLock => targetLockSettings != null && targetLockSettings.Enabled;

    /// <summary>攻击判定框区间列表，来自统一 Timeline。</summary>
    public HitboxNotifyState[] HitboxStates => Timeline.HitboxStates;

    /// <summary>VFX 点事件列表，来自统一 Timeline。</summary>
    public PlayVfxNotify[] PlayVfxNotifies => Timeline.PlayVfxNotifies;

    /// <summary>SFX 点事件列表，来自统一 Timeline。</summary>
    public PlaySfxNotify[] PlaySfxStates => Timeline.PlaySfxStates;

    /// <summary>阶段窗口列表，来自统一 Timeline。</summary>
    public ActionPhaseNotifyState[] Phases => Timeline.PhaseStates;

    /// <summary>通用点事件列表，来自统一 Timeline。</summary>
    public ActionEvent[] ActionEvents => Timeline.ActionEvents;

    /// <summary>命中时镜头震动预设；可为空。</summary>
    public CameraShakeProfile CameraShakeProfile => cameraShakeProfile;

    /// <summary>该招式命中是否触发镜头震动（兼容旧 ActionDefinition 资产）。</summary>
    public bool ShouldShakeOnHit()
    {
        if (disableCameraShakeOnHit)
            return false;

        if (useCameraShakeOnHit)
            return true;

        // 旧资产 YAML 无 useCameraShakeOnHit 时 Unity 反序列化为 false；Attack 仍默认震。
        return actionType == CombatActionType.Attack;
    }

    /// <summary>卡肉持续秒数（由 hitStopFrames / sampleRate 换算）。</summary>
    public float HitStopDurationSeconds => hitStopFrames > 0 ? hitStopFrames / SampleRate : 0f;

    /// <summary>该招式命中是否触发卡肉。</summary>
    public bool ShouldHitStopOnHit()
    {
        if (disableHitStopOnHit || hitStopFrames <= 0)
            return false;

        return useHitStopOnHit;
    }

    /// <summary>每招是否仅第一次命中触发卡肉。</summary>
    public bool HitStopOncePerAction => hitStopOncePerAction;

    /// <summary>招式总时长（秒）；优先 totalFrames，否则回退段累加。</summary>
    public float DurationSeconds
    {
        get
        {
            if (totalFrames > 0)
                return totalFrames / SampleRate;

            return ComputeTotalFramesFromSegments() / SampleRate;
        }
    }

    /// <summary>解析全局逻辑帧落在哪一段，以及段内帧偏移。</summary>
    public bool TryGetSegmentAtFrame(
        int globalFrame,
        out int segmentIndex,
        out ActionAnimationSegment segment,
        out int frameOffsetInSegment)
    {
        segmentIndex = -1;
        segment = default;
        frameOffsetInSegment = 0;

        ActionAnimationSegment[] segments = AnimationSegments;
        if (segments.Length == 0 || globalFrame < 0)
            return false;

        int cursor = 0;
        for (int i = 0; i < segments.Length; i++)
        {
            int count = segments[i].GetFrameCount(SampleRate);
            if (count <= 0 || segments[i].clip == null)
                continue;

            if (globalFrame < cursor + count)
            {
                segmentIndex = i;
                segment = segments[i];
                frameOffsetInSegment = globalFrame - cursor;
                return true;
            }

            cursor += count;
        }

        // 落在末尾之后时钳到最后有效段末帧，便于 scrub / 结束判定采样。
        return TryGetLastValidSegment(out segmentIndex, out segment, out frameOffsetInSegment);
    }

    /// <summary>按已播放秒数解析当前动画段。</summary>
    public bool TryGetSegmentAtElapsed(
        float elapsedSeconds,
        out int segmentIndex,
        out ActionAnimationSegment segment,
        out int frameOffsetInSegment)
    {
        return TryGetSegmentAtFrame(
            FrameAt(elapsedSeconds),
            out segmentIndex,
            out segment,
            out frameOffsetInSegment);
    }

    /// <summary>全局逻辑帧对应的采样 Clip；无则 null。</summary>
    public AnimationClip GetClipAtFrame(int globalFrame)
    {
        return TryGetSegmentAtFrame(globalFrame, out _, out ActionAnimationSegment segment, out _)
            ? segment.clip
            : null;
    }

    /// <summary>全局逻辑帧对应的 Clip 局部采样时间（秒）。</summary>
    public float GetLocalTimeInSegment(int globalFrame)
    {
        if (!TryGetSegmentAtFrame(globalFrame, out _, out ActionAnimationSegment segment, out int offset))
            return 0f;

        return segment.GetLocalTimeSeconds(offset, SampleRate);
    }

    /// <summary>解析切入指定段应使用的淡入时长；首段可回退到招式默认 CrossFade。</summary>
    public float ResolveSegmentCrossFade(int segmentIndex)
    {
        ActionAnimationSegment[] segments = AnimationSegments;
        if (segmentIndex < 0 || segmentIndex >= segments.Length)
            return crossFadeDuration;

        float segmentFade = segments[segmentIndex].crossFadeDuration;
        if (segmentIndex == 0 && segmentFade <= 0f)
            return crossFadeDuration;

        return Mathf.Max(0f, segmentFade);
    }

    /// <summary>返回指定类型的唯一 CancelWindow；缺失或重复配置时返回 null。</summary>
    public CancelWindowNotifyState GetCancelWindow(CancelWindowType windowType) =>
        Timeline.GetCancelWindow(windowType);

    /// <summary>Transition 衔接，按 priority 降序。</summary>
    public IReadOnlyList<ActionTransition> GetTransitionsSorted()
    {
        if (transitions == null || transitions.Length == 0)
            return Array.Empty<ActionTransition>();

        var list = new List<ActionTransition>(transitions);
        list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        return list;
    }

    /// <summary>当前时刻是否满足 Transition 触发条件。</summary>
    /// <param name="hasConfirmedHit">本招是否已命中；OnHitConfirm / OnWhiff 需要。</param>
    public bool IsTransitionEligible(
        ActionTransition transition,
        float elapsedSeconds,
        bool hasConfirmedHit = false)
    {
        if (transition == null || totalFrames <= 0)
            return false;

        switch (transition.Condition)
        {
            case ActionTransitionCondition.AnimationEnd:
                return elapsedSeconds >= DurationSeconds;
            case ActionTransitionCondition.AtFrame:
                return FrameAt(elapsedSeconds) >= transition.StartFrame;
            case ActionTransitionCondition.OnHitConfirm:
                return hasConfirmedHit;
            case ActionTransitionCondition.OnWhiff:
                return elapsedSeconds >= DurationSeconds && !hasConfirmedHit;
            default:
                return false;
        }
    }

    /// <summary>返回指定帧上全部生效的阶段（按数组顺序）。</summary>
    public IReadOnlyList<ActionPhaseNotifyState> GetActivePhasesAtFrame(int frame) =>
        Timeline.GetActivePhaseStatesAtFrame(frame);

    /// <summary>
    /// 指定帧是否允许高优硬打断。
    /// 无三相窗口覆盖时默认可打断；有覆盖时任一 Interruptible 即可；Invincible/SuperArmor 不参与。
    /// </summary>
    public bool IsInterruptibleAtFrame(int frame)
    {
        bool hasControllingPhase = false;
        IReadOnlyList<ActionPhaseNotifyState> activePhases = GetActivePhasesAtFrame(frame);
        foreach (ActionPhaseNotifyState phase in activePhases)
        {
            if (!phase.ControlsInterruptibility)
                continue;

            hasControllingPhase = true;
            if (phase.Interruptible)
                return true;
        }

        return !hasControllingPhase;
    }

    /// <summary>指定帧的 Recovery 窗口是否允许移动输入退出到 Locomotion。</summary>
    public bool AllowsRecoveryMovementCancelAtFrame(int frame)
    {
        foreach (ActionPhaseNotifyState phase in Timeline.PhaseStates)
        {
            if (phase != null && phase.IsActiveAtFrame(frame) && phase.AllowMovementCancel)
                return true;
        }

        return false;
    }

    /// <summary>指定帧的 Recovery 窗口是否允许有效动作输入按 Graph Entry 重开。</summary>
    public bool AllowsRecoveryEntryRestartAtFrame(int frame)
    {
        foreach (ActionPhaseNotifyState phase in Timeline.PhaseStates)
        {
            if (phase != null && phase.IsActiveAtFrame(frame) && phase.AllowEntryRestart)
                return true;
        }

        return false;
    }

    /// <summary>指定类型的唯一 CancelWindow 是否覆盖当前帧。</summary>
    public bool IsCancelWindowActiveAtFrame(CancelWindowType windowType, int frame)
    {
        CancelWindowNotifyState window = GetCancelWindow(windowType);
        return window != null && window.IsActiveAtFrame(frame);
    }

    public bool IsInDisplacementWindow(float elapsedSeconds)
    {
        if (!HasScriptedDisplacement || totalFrames <= 0)
            return false;

        return GetActiveMovementState(elapsedSeconds) != null;
    }

    /// <summary>当前时刻是否落在输入旋转修正窗口内。</summary>
    public bool IsInRotationWindow(float elapsedSeconds)
    {
        if (totalFrames <= 0)
            return false;

        return GetActiveRotationState(elapsedSeconds) != null;
    }

    /// <summary>返回当前时刻最高优先级脚本位移窗口。</summary>
    public MovementNotifyState GetActiveMovementState(float elapsedSeconds) =>
        Timeline.GetActiveMovementStateAtFrame(FrameAt(elapsedSeconds));

    /// <summary>返回当前时刻最高优先级旋转修正窗口。</summary>
    public RotationNotifyState GetActiveRotationState(float elapsedSeconds) =>
        Timeline.GetActiveRotationStateAtFrame(FrameAt(elapsedSeconds));

    /// <summary>将 elapsed 秒换算为逻辑帧索引。</summary>
    public int FrameAt(float elapsedSeconds) => Mathf.FloorToInt(elapsedSeconds * SampleRate);

    /// <summary>返回指定帧上全部生效的 Hitbox（按数组顺序）。</summary>
    public IReadOnlyList<HitboxNotifyState> GetActiveHitboxesAtFrame(int frame)
    {
        return Timeline.GetActiveHitboxesAtFrame(frame);
    }

    void OnValidate()
    {
        sampleRate = Mathf.Max(1f, sampleRate);
        MigrateLegacyAnimationClipIfNeeded();
        totalFrames = Mathf.Max(1, ComputeTotalFramesFromSegments());

        timeline ??= new ActionTimeline();
        timeline.ClampToTotalFrames(totalFrames);

        hitStopFrames = Mathf.Max(0, hitStopFrames);
        baseDamage = Mathf.Max(0f, baseDamage);
    }

    /// <summary>旧单字段 animationClip 迁入 animationSegments[0]；之后只认 segments。</summary>
    void MigrateLegacyAnimationClipIfNeeded()
    {
        if (animationClip == null)
            return;

        bool hasSegmentClip = false;
        if (animationSegments != null)
        {
            for (int i = 0; i < animationSegments.Length; i++)
            {
                if (animationSegments[i].clip != null)
                {
                    hasSegmentClip = true;
                    break;
                }
            }
        }

        if (!hasSegmentClip)
        {
            animationSegments = new[]
            {
                new ActionAnimationSegment
                {
                    clip = animationClip,
                    startFrame = 0,
                    endFrame = -1,
                    crossFadeDuration = 0f,
                },
            };
        }

        animationClip = null;
    }

    int ComputeTotalFramesFromSegments()
    {
        ActionAnimationSegment[] segments = AnimationSegments;
        int sum = 0;
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i].clip == null)
                continue;

            sum += segments[i].GetFrameCount(SampleRate);
        }

        return sum;
    }

    bool TryGetLastValidSegment(
        out int segmentIndex,
        out ActionAnimationSegment segment,
        out int frameOffsetInSegment)
    {
        segmentIndex = -1;
        segment = default;
        frameOffsetInSegment = 0;

        ActionAnimationSegment[] segments = AnimationSegments;
        for (int i = segments.Length - 1; i >= 0; i--)
        {
            int count = segments[i].GetFrameCount(SampleRate);
            if (count <= 0 || segments[i].clip == null)
                continue;

            segmentIndex = i;
            segment = segments[i];
            frameOffsetInSegment = count - 1;
            return true;
        }

        return false;
    }
}

/// <summary>招式开始时由运行时触发的副作用（朝向、切战斗模式等）。</summary>
public enum ActionStartBehaviorType
{
    FaceBufferedMoveIntent = 0,

    /// <summary>切换 CombatModeType，目标与策略见 ActionDefinition 的 switchCombatMode 字段。</summary>
    SwitchCombatMode = 1,
}
