using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>招式数据：动画、统一时间轴、自动 Transition 与命中反馈默认参数。</summary>
[CreateAssetMenu(fileName = "ActionDefinition", menuName = "ACT/Combat/Action Definition")]
public class ActionDefinition : ScriptableObject
{
    [SerializeField] AnimationClip animationClip = null;
    [SerializeField] float sampleRate = 30f;
    [SerializeField] int totalFrames;
    [SerializeField] CombatActionType actionType = CombatActionType.Attack;
    [SerializeField] float crossFadeDuration = 0.1f;

    [Header("Phases (Editor)")]
    [Tooltip("Startup / Active / Recovery 与无敌、霸体覆盖；编辑器时间轴数据源。")]
    [SerializeField] ActionPhase[] phases = Array.Empty<ActionPhase>();

    [Header("Timeline")]
    [Tooltip("动作帧数据唯一真源：点事件与区间窗口均从此处读取。")]
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

    /// <summary>播放该动作的 AnimationClip。</summary>
    public AnimationClip AnimationClip => animationClip;

    /// <summary>逻辑采样率；所有时间轴帧都按此值换算。</summary>
    public float SampleRate => sampleRate > 0f ? sampleRate : 30f;

    /// <summary>动作总逻辑帧数。</summary>
    public int TotalFrames => totalFrames;

    /// <summary>动作类型，用于反馈默认值和上层分类。</summary>
    public CombatActionType ActionType => actionType;

    /// <summary>切入动作动画时使用的淡入时长。</summary>
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

    /// <summary>VFX 区间窗口列表，来自统一 Timeline。</summary>
    public PlayVfxNotify[] PlayVfxNotifies => Timeline.PlayVfxNotifies;

    /// <summary>SFX 区间窗口列表，来自统一 Timeline。</summary>
    public PlaySfxNotifyState[] PlaySfxStates => Timeline.PlaySfxStates;

    /// <summary>阶段标记列表；暂保留为独立编辑数据。</summary>
    public ActionPhase[] Phases => phases ?? Array.Empty<ActionPhase>();

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

    public float DurationSeconds
    {
        get
        {
            if (totalFrames > 0)
                return totalFrames / SampleRate;

            return animationClip != null ? animationClip.length : 0f;
        }
    }

    /// <summary>按 priority 降序返回 CancelWindow。</summary>
    public IReadOnlyList<ResolvedCancelWindow> GetCancelWindowsSorted()
    {
        return Timeline.GetCancelWindowsSorted();
    }

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
    public IReadOnlyList<ActionPhase> GetActivePhasesAtFrame(int frame)
    {
        if (phases == null || phases.Length == 0)
            return Array.Empty<ActionPhase>();

        var active = new List<ActionPhase>();
        foreach (ActionPhase phase in phases)
        {
            if (phase != null && phase.IsActiveAtFrame(frame))
                active.Add(phase);
        }

        return active;
    }

    /// <summary>指定帧是否落在可被打断的阶段区间内。</summary>
    public bool IsInterruptibleAtFrame(int frame)
    {
        IReadOnlyList<ActionPhase> activePhases = GetActivePhasesAtFrame(frame);
        if (activePhases.Count == 0)
            return false;

        foreach (ActionPhase phase in activePhases)
        {
            if (phase.Interruptible)
                return true;
        }

        return false;
    }

    public bool IsInCancelWindow(ResolvedCancelWindow window, float elapsedSeconds)
    {
        if (totalFrames <= 0)
            return false;

        return window.IsActiveAtFrame(FrameAt(elapsedSeconds));
    }

    /// <summary>当前时刻是否落在 CancelType.Movement 窗口内。</summary>
    public bool IsInMovementCancelWindow(float elapsedSeconds)
    {
        if (totalFrames <= 0)
            return false;

        int frame = FrameAt(elapsedSeconds);
        foreach (CancelWindowNotifyState window in Timeline.CancelWindowStates)
        {
            if (window != null
                && window.CancelType == CancelType.Movement
                && window.IsActiveAtFrame(frame))
            {
                return true;
            }
        }

        return false;
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
        if (animationClip == null)
            return;

        sampleRate = Mathf.Max(1f, sampleRate);
        totalFrames = Mathf.Max(1, Mathf.RoundToInt(animationClip.length * sampleRate));

        timeline ??= new ActionTimeline();
        timeline.ClampToTotalFrames(totalFrames);

        if (phases != null)
        {
            foreach (ActionPhase phase in phases)
                phase?.ClampToTotalFrames(totalFrames);
        }

        hitStopFrames = Mathf.Max(0, hitStopFrames);
    }
}

/// <summary>招式开始时由运行时触发的副作用（朝向、切战斗模式等）。</summary>
public enum ActionStartBehaviorType
{
    FaceBufferedMoveIntent = 0,

    /// <summary>切换 CombatModeType，目标与策略见 ActionDefinition 的 switchCombatMode 字段。</summary>
    SwitchCombatMode = 1,
}
