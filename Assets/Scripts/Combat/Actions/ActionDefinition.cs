using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>招式数据：动画、CancelWindow 衔接、结束 Transition 与位移窗口。</summary>
[CreateAssetMenu(fileName = "ActionDefinition", menuName = "ACT/Combat/Action Definition")]
public class ActionDefinition : ScriptableObject
{
    [SerializeField] string id = "player_attack_1";
    [SerializeField] string displayName = "Attack 1";
    [SerializeField] AnimationClip animationClip;
    [SerializeField] float sampleRate = 30f;
    [SerializeField] int totalFrames;
    [SerializeField] CombatActionType actionType = CombatActionType.Attack;
    [SerializeField] float crossFadeDuration = 0.1f;

    [Header("Cancel Windows")]
    [SerializeField] CancelWindow[] cancelWindows = Array.Empty<CancelWindow>();

    [Header("Transitions")]
    [SerializeField] ActionTransition[] transitions = Array.Empty<ActionTransition>();

    [Header("Start Behaviors")]
    [SerializeField] ActionStartBehaviorType[] startBehaviors = Array.Empty<ActionStartBehaviorType>();

    [Header("Combat Mode Switch")]
    [Tooltip("Start Behaviors 含 SwitchCombatMode 时生效。")]
    [SerializeField] CombatModeType switchCombatModeTarget = CombatModeType.Default;
    [SerializeField] CombatModeSwitchPolicy switchCombatModePolicy = CombatModeSwitchPolicy.Immediate;

    [Header("Hitboxes")]
    [SerializeField] HitboxKeyframe[] hitboxes = Array.Empty<HitboxKeyframe>();

    [Header("VFX")]
    [Tooltip("刀光等特效帧事件：在 triggerFrame 生成一次 Prefab。")]
    [SerializeField] ActionVfxKeyframe[] vfxEvents = Array.Empty<ActionVfxKeyframe>();

    [Header("Camera Shake")]
    [Tooltip("命中时使用的镜头震动预设；为空则按 ActionType 使用 CameraShakeController 默认配置。")]
    [SerializeField] CameraShakeProfile cameraShakeProfile;
    [Tooltip("勾选后禁止该招式触发镜头震动。")]
    [SerializeField] bool disableCameraShakeOnHit;
    [Tooltip("开启镜头震动；新资产默认 true。旧资产缺此字段时 Attack 仍默认开启。")]
    [SerializeField] bool useCameraShakeOnHit = true;

    [Header("Rotation")]
    [Tooltip("帧窗口内允许玩家用移动输入修正朝向，常用于攻击前摇瞄准。")]
    [SerializeField] RotationWindow rotationWindow = new();

    [Header("Target Lock")]
    [Tooltip("攻击旋转窗口内的自动索敌配置。")]
    [SerializeField] TargetLockSettings targetLockSettings = new();

    [Header("Movement")]
    [Tooltip("开启时由动画 Root Motion 驱动位移，脚本位移（Displacement Distance）将被忽略。")]
    [SerializeField] bool useRootMotion = true;
    [SerializeField] float displacementDistance;
    [SerializeField] int displacementStartFrame;
    [SerializeField] int displacementEndFrame;

    public string Id => id;
    public string DisplayName => displayName;
    public AnimationClip AnimationClip => animationClip;
    public float SampleRate => sampleRate > 0f ? sampleRate : 30f;
    public int TotalFrames => totalFrames;
    public CombatActionType ActionType => actionType;
    public float CrossFadeDuration => crossFadeDuration;
    public bool UseRootMotion => useRootMotion;
    public float DisplacementDistance => displacementDistance;
    public int DisplacementStartFrame => displacementStartFrame;
    public int DisplacementEndFrame => displacementEndFrame;
    public ActionStartBehaviorType[] StartBehaviors => startBehaviors ?? Array.Empty<ActionStartBehaviorType>();
    public CombatModeType SwitchCombatModeTarget => switchCombatModeTarget;
    public CombatModeSwitchPolicy SwitchCombatModePolicy => switchCombatModePolicy;
    public bool HasScriptedDisplacement => !useRootMotion && Mathf.Abs(displacementDistance) > 0.001f;
    public RotationWindow RotationWindow => rotationWindow;
    public bool HasRotationWindow => rotationWindow != null && rotationWindow.IsConfigured;
    public TargetLockSettings TargetLockSettings => targetLockSettings ?? new TargetLockSettings();
    public bool HasTargetLock => targetLockSettings != null && targetLockSettings.Enabled;
    public HitboxKeyframe[] Hitboxes => hitboxes ?? Array.Empty<HitboxKeyframe>();
    public ActionVfxKeyframe[] VfxEvents => vfxEvents ?? Array.Empty<ActionVfxKeyframe>();

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
        if (cancelWindows == null || cancelWindows.Length == 0)
            return Array.Empty<ResolvedCancelWindow>();

        var list = new List<ResolvedCancelWindow>(cancelWindows.Length);
        foreach (CancelWindow window in cancelWindows)
        {
            if (window != null)
                list.Add(window.ToResolved());
        }

        list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        return list;
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
    public bool IsTransitionEligible(ActionTransition transition, float elapsedSeconds)
    {
        if (transition == null || totalFrames <= 0)
            return false;

        switch (transition.Condition)
        {
            case ActionTransitionCondition.AnimationEnd:
                return elapsedSeconds >= DurationSeconds;
            case ActionTransitionCondition.AtFrame:
                return FrameAt(elapsedSeconds) >= transition.StartFrame;
            default:
                return false;
        }
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
        if (totalFrames <= 0 || cancelWindows == null)
            return false;

        int frame = FrameAt(elapsedSeconds);
        foreach (CancelWindow window in cancelWindows)
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

        int frame = FrameAt(elapsedSeconds);
        return frame >= displacementStartFrame && frame <= displacementEndFrame;
    }

    /// <summary>当前时刻是否落在输入旋转修正窗口内。</summary>
    public bool IsInRotationWindow(float elapsedSeconds)
    {
        if (!HasRotationWindow || totalFrames <= 0)
            return false;

        return rotationWindow.IsActiveAtFrame(FrameAt(elapsedSeconds));
    }

    public float DisplacementSpeed
    {
        get
        {
            if (!HasScriptedDisplacement)
                return 0f;

            int frameCount = displacementEndFrame - displacementStartFrame + 1;
            if (frameCount <= 0)
                return 0f;

            return displacementDistance / (frameCount / SampleRate);
        }
    }

    /// <summary>将 elapsed 秒换算为逻辑帧索引。</summary>
    public int FrameAt(float elapsedSeconds) => Mathf.FloorToInt(elapsedSeconds * SampleRate);

    /// <summary>返回指定帧上全部生效的 Hitbox（按数组顺序）。</summary>
    public IReadOnlyList<HitboxKeyframe> GetActiveHitboxesAtFrame(int frame)
    {
        if (hitboxes == null || hitboxes.Length == 0)
            return Array.Empty<HitboxKeyframe>();

        var active = new List<HitboxKeyframe>();
        foreach (HitboxKeyframe hitbox in hitboxes)
        {
            if (hitbox != null && hitbox.IsActiveAtFrame(frame))
                active.Add(hitbox);
        }

        return active;
    }

    void OnValidate()
    {
        if (animationClip == null)
            return;

        if (string.IsNullOrEmpty(id))
            id = name;

        sampleRate = Mathf.Max(1f, sampleRate);
        totalFrames = Mathf.Max(1, Mathf.RoundToInt(animationClip.length * sampleRate));

        if (Mathf.Abs(displacementDistance) > 0.001f)
        {
            if (displacementEndFrame <= 0)
                displacementEndFrame = totalFrames - 1;

            if (displacementStartFrame <= 0 && displacementEndFrame > 0)
                displacementStartFrame = 0;
        }

        displacementStartFrame = Mathf.Clamp(displacementStartFrame, 0, Mathf.Max(0, totalFrames - 1));
        displacementEndFrame = Mathf.Clamp(
            displacementEndFrame,
            displacementStartFrame,
            Mathf.Max(0, totalFrames - 1));

        if (rotationWindow != null && rotationWindow.IsConfigured)
            rotationWindow.ClampToTotalFrames(totalFrames);

        if (vfxEvents != null)
        {
            foreach (ActionVfxKeyframe vfxEvent in vfxEvents)
                vfxEvent?.ClampToTotalFrames(totalFrames);
        }
    }
}

/// <summary>招式开始时由运行时触发的副作用（朝向、切战斗模式等）。</summary>
public enum ActionStartBehaviorType
{
    FaceBufferedMoveIntent = 0,

    /// <summary>切换 CombatModeType，目标与策略见 ActionDefinition 的 switchCombatMode 字段。</summary>
    SwitchCombatMode = 1,
}
