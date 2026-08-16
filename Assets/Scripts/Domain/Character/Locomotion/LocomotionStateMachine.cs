using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Locomotion 内层纯状态机宿主：注册 Idle/Start/Gait/PivotTurn/Stop，
/// 先推进转换再执行当前相位帧逻辑，保证同帧切态后 Motor/动画立即生效。
/// </summary>
public sealed class LocomotionStateMachine
{
    readonly StateMachine<LocomotionPhase, LocomotionContext> _machine = new();
    readonly Dictionary<LocomotionPhase, LocomotionPhaseState> _phases = new();

    /// <summary>共享上下文；供外部只读 Phase/Gait 与意图条件查询。</summary>
    public LocomotionContext Context { get; }

    /// <summary>当前内嵌相位。</summary>
    public LocomotionPhase Phase => _machine.CurrentStateId;

    /// <summary>当前稳态步态。</summary>
    public LocomotionGait Gait => Context.Gait;

    /// <summary>装配内层机并注入依赖；不在此处 Initialize（由 Enter 驱动）。</summary>
    public LocomotionStateMachine(
        Transform root,
        CharacterMotor motor,
        CharacterAnimationService animation,
        IMoveIntentSource moveIntent,
        CharacterLocomotionProfile profile,
        LocomotionFootstepPlayer footstepPlayer)
    {
        var footCycle = new LocomotionFootCycle();
        var rootMotionPlayer = new LocomotionRootMotionPlayer(profile);
        Context = new LocomotionContext(
            root,
            motor,
            animation,
            moveIntent,
            profile,
            footCycle,
            footstepPlayer,
            rootMotionPlayer);
        Context.BindMachine(this);

        Register(new IdleLocomotionState());
        Register(new StartLocomotionState());
        Register(new GaitLocomotionState());
        Register(new PivotTurnLocomotionState());
        Register(new StopLocomotionState());
    }

    /// <summary>进入顶层 Locomotion；可消费 Action 边界传入的一次性步态恢复请求。</summary>
    public void Enter(in LocomotionResumeRequest resumeRequest)
    {
        Context.RunHoldSeconds = 0f;
        Context.GaitInputGapSeconds = 0f;
        Context.PivotMoveLatched = false;
        Context.PendingGaitHardCutPlay = false;
        Context.PendingGaitFaceDirection = Vector3.zero;
        Context.StopPlayHardCut = false;
        Context.RootMotionPlayer.End();
        Context.FootCycle.Unfreeze();
        Context.FootCycle.SetMarkers(System.Array.Empty<FootPlantMarker>());
        Context.SprintLean.Reset();

        bool canResume = resumeRequest.IsValid
            && (!resumeRequest.RequireMoveIntent || Context.Input.HasMoveIntent);
        if (canResume && resumeRequest.SkipStart)
        {
            // Dodge 恢复：直接进目标步态，不走 Idle→Start→Run 计时。
            LocomotionGaitPolicy policy = Context.Profile != null
                ? Context.Profile.GaitPolicy
                : new LocomotionGaitPolicy();
            Context.PendingGait = policy.ClampGait(resumeRequest.InitialGait);
            Context.PendingGaitHardCutPlay = true;
            _machine.Initialize(Context, LocomotionPhase.Gait);
            return;
        }

        _machine.Initialize(Context, LocomotionPhase.Idle);
    }

    /// <summary>离开顶层 Locomotion；停止落脚采样与烘焙根位移。下次 Enter 会重新 Initialize。</summary>
    public void Exit()
    {
        Context.FootCycle.Freeze();
        Context.RootMotionPlayer.End();
        Context.RunHoldSeconds = 0f;
        Context.GaitInputGapSeconds = 0f;
        Context.PivotMoveLatched = false;
        Context.SprintLean.Reset();
    }

    /// <summary>捕获当前内层机可恢复字段，供 SavedMove。</summary>
    public LocomotionSavedState Capture()
    {
        Context.RootMotionPlayer.Capture(
            out bool rmActive,
            out AnimationKey rmKey,
            out int rmFrame,
            out float rmYaw);
        AnimationKey animKey = Context.Animation != null && Context.Animation.CurrentKey.HasValue
            ? Context.Animation.CurrentKey.Value
            : AnimationKey.Idle;
        float normalized = Context.Animation != null ? Context.Animation.NormalizedTime : 0f;
        return new LocomotionSavedState(
            Phase,
            Context.Gait,
            animKey,
            normalized,
            Context.RunHoldSeconds,
            Context.GaitInputGapSeconds,
            Context.GaitCardinal,
            Context.GaitCardinalDwellFrames,
            Context.ActiveStartKey,
            Context.ActiveStartGait,
            Context.ActiveStartCardinal,
            Context.StopKey,
            Context.StopFromStart,
            Context.StopEnterFacing,
            Context.PivotTargetDirection,
            Context.PivotEnterFacing,
            Context.PivotMoveLatched,
            rmActive,
            rmKey,
            rmFrame,
            rmYaw,
            Context.FootCycle.LastPlanted,
            Context.FootCycle.HasPlantRecord,
            footFrozen: Phase != LocomotionPhase.Gait && Phase != LocomotionPhase.Start);
    }

    /// <summary>
    /// 纠偏恢复：写 Context、RestoreCurrent 不走 Enter，再硬切 Play/Seek。
    /// 禁止 Initialize→Idle.Enter，否则会清掉 Sprint。
    /// </summary>
    public void Restore(in LocomotionSavedState state)
    {
        Context.RunHoldSeconds = state.RunHoldSeconds;
        Context.GaitInputGapSeconds = state.GaitInputGapSeconds;
        Context.Gait = state.Gait;
        Context.PendingGait = state.Gait;
        Context.PendingGaitHardCutPlay = false;
        Context.PendingGaitFaceDirection = Vector3.zero;
        Context.GaitCardinal = state.GaitCardinal;
        Context.GaitCardinalDwellFrames = state.GaitCardinalDwellFrames;
        Context.ActiveStartKey = state.ActiveStartKey;
        Context.ActiveStartGait = state.ActiveStartGait;
        Context.ActiveStartCardinal = state.ActiveStartCardinal;
        Context.StopKey = state.StopKey;
        Context.StopFromStart = state.StopFromStart;
        Context.StopPlayHardCut = false;
        Context.StopEnterFacing = state.StopEnterFacing.sqrMagnitude > 0.0001f
            ? state.StopEnterFacing
            : Vector3.forward;
        Context.PivotTargetDirection = state.PivotTarget.sqrMagnitude > 0.0001f
            ? state.PivotTarget
            : Vector3.forward;
        Context.PivotEnterFacing = state.PivotEnterFacing.sqrMagnitude > 0.0001f
            ? state.PivotEnterFacing
            : Vector3.forward;
        Context.PivotMoveLatched = state.PivotMoveLatched;
        Context.SprintLean.Reset();
        Context.FootCycle.SetMarkers(Context.GetMarkersForPhase(state.Phase));
        Context.FootCycle.Restore(state.LastPlanted, state.HasPlantRecord, state.FootFrozen);
        Context.RootMotionPlayer.Restore(
            state.RootMotionActive,
            state.RootMotionKey,
            state.RootMotionFrame,
            state.RootMotionBasisYaw);

        _machine.RestoreCurrent(Context, state.Phase);

        if (Context.Animation != null)
        {
            Context.Animation.ResetPlaybackState();
            Context.Animation.Play(state.AnimationKey, 0f);
            if (state.NormalizedTime > 0f)
                Context.Animation.SeekLocomotionNormalized(state.NormalizedTime);
        }
    }

    /// <summary>推进转换，再执行当前相位的位移/动画/脚步，并刷新 Sprint 倾身。</summary>
    public void Tick(float deltaTime)
    {
        Context.DeltaTime = Mathf.Max(0f, deltaTime);
        // 本步输入/速度快照，供各相位 Tick 共用
        Context.FrameSnapshot = Context.BuildSnapshot();
        // FaceTarget 朝向须在 ApplyLocomotion 前写入 Motor
        Context.PublishFacingTargetToMotor();
        // 先做相位转换（Idle/Start/Gait/Stop/Pivot）
        _machine.Tick(Context.DeltaTime);
        // 再执行当前相位的位移/动画/脚步
        if (_phases.TryGetValue(_machine.CurrentStateId, out LocomotionPhaseState phase))
            phase.ExecuteFrame(Context.DeltaTime);
        // 冲刺倾身只写视觉 Roll
        UpdateSprintLean(Context.DeltaTime);
    }

    /// <summary>
    /// L-DIR4：仅 Gait+Sprint+FollowInput 时按 facing↔wish 产 lean；其余相位强制 0。
    /// </summary>
    void UpdateSprintLean(float deltaTime)
    {
        SprintLeanSettings settings = Context.Profile != null
            ? Context.Profile.SprintLean
            : null;

        bool allowLean = Phase == LocomotionPhase.Gait
            && Context.Gait == LocomotionGait.Sprint
            && Context.ResolveFacingMode() == LocomotionFacingMode.FollowMove
            && Context.HasMeaningfulMove(Context.FrameSnapshot);

        Vector3 facing = Context.Root.forward;
        Vector3 wish = Context.FrameSnapshot.WorldMoveDirection;
        Context.SprintLean.Tick(settings, facing, wish, allowLean, deltaTime);
    }

    /// <summary>供 Context / 各态请求相位切换。</summary>
    public bool TryChangePhase(LocomotionPhase next, bool force = false) =>
        _machine.TryChangeState(next, force);

    void Register(LocomotionPhaseState state)
    {
        _phases[state.Id] = state;
        _machine.RegisterState(state);
    }
}
