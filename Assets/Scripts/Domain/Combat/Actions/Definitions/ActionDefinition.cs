using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>单个动作的播放内容：动画、统一帧时间轴与纯执行策略。</summary>
[CreateAssetMenu(fileName = "ActionDefinition", menuName = "ACT/Combat/Action Definition")]
public class ActionDefinition : ScriptableObject
{
    [Header("Animation")]
    [Tooltip("按顺序播放的动画段；totalFrames 由各段有效帧累加。")]
    [SerializeField] ActionAnimationSegment[] animationSegments = Array.Empty<ActionAnimationSegment>();

    [Tooltip("整数动作采样率；L1 阶段不得高于 SimulationWorld 60Hz。")]
    [SerializeField] int sampleRate = 30;
    [SerializeField] int totalFrames;
    [SerializeField] CombatActionType actionType = CombatActionType.Attack;
    [SerializeField] float crossFadeDuration = 0.1f;

    [Header("Execution")]
    [Tooltip("动作自身固定的执行方式；输入、索敌与流程选择由 ActionGraph 节点负责。")]
    [SerializeField] ActionExecutionPolicy executionPolicy = new();

    [Header("Timeline")]
    [Tooltip("动作帧数据唯一真源：Phase、点事件与其它区间窗口均从此处读取。")]
    [SerializeField] ActionTimeline timeline = new();

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

    /// <summary>整数逻辑采样率；所有时间轴帧都按此值换算。</summary>
    public int SampleRate =>
        Mathf.Clamp(sampleRate, 1, SimulationConfig.DefaultLogicHz);

    /// <summary>动作总逻辑帧数（各段有效帧之和）。</summary>
    public int TotalFrames => totalFrames;

    /// <summary>动作类型，用于反馈默认值和上层分类。</summary>
    public CombatActionType ActionType => actionType;

    /// <summary>切入招式首段时的默认淡入时长；段可自带 crossFadeDuration 覆盖。</summary>
    public float CrossFadeDuration => crossFadeDuration;

    /// <summary>动作自身固定的执行方式。</summary>
    public ActionExecutionPolicy ExecutionPolicy => executionPolicy ?? new ActionExecutionPolicy();

    /// <summary>动作帧数据唯一真源：点事件与区间窗口均从此处读取。</summary>
    public ActionTimeline Timeline => timeline ?? new ActionTimeline();

    /// <summary>是否存在非 RootMotion 的脚本位移窗口。</summary>
    public bool HasScriptedDisplacement =>
        !ExecutionPolicy.UseRootMotion && Timeline.HasScriptedMovement;

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

    /// <summary>供动画与编辑器显示的派生总时长；Runtime 结束判定只使用 TotalFrames。</summary>
    public float DurationSeconds
    {
        get
        {
            if (totalFrames > 0)
                return totalFrames / (float)SampleRate;

            return ComputeTotalFramesFromSegments() / (float)SampleRate;
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

    /// <summary>指定整数动作帧是否落在脚本位移窗口内。</summary>
    public bool IsInDisplacementWindow(int frame)
    {
        if (!HasScriptedDisplacement || totalFrames <= 0)
            return false;

        return GetActiveMovementStateAtFrame(frame) != null;
    }

    /// <summary>指定整数动作帧是否落在输入旋转修正窗口内。</summary>
    public bool IsInRotationWindow(int frame)
    {
        if (totalFrames <= 0)
            return false;

        return GetActiveRotationStateAtFrame(frame) != null;
    }

    /// <summary>返回指定整数动作帧最高优先级的脚本位移窗口。</summary>
    public MovementNotifyState GetActiveMovementStateAtFrame(int frame) =>
        Timeline.GetActiveMovementStateAtFrame(frame);

    /// <summary>返回指定整数动作帧最高优先级的旋转修正窗口。</summary>
    public RotationNotifyState GetActiveRotationStateAtFrame(int frame) =>
        Timeline.GetActiveRotationStateAtFrame(frame);

    /// <summary>返回指定帧上全部生效的 Hitbox（按数组顺序）。</summary>
    public IReadOnlyList<HitboxNotifyState> GetActiveHitboxesAtFrame(int frame)
    {
        return Timeline.GetActiveHitboxesAtFrame(frame);
    }

    void OnValidate()
    {
        sampleRate = Mathf.Clamp(sampleRate, 1, SimulationConfig.DefaultLogicHz);
        totalFrames = Mathf.Max(1, ComputeTotalFramesFromSegments());

        timeline ??= new ActionTimeline();
        timeline.ClampToTotalFrames(totalFrames);
        executionPolicy ??= new ActionExecutionPolicy();
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
