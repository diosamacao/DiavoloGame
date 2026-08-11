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
        Context.PivotElapsedSeconds = 0f;
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

    /// <summary>推进转换，再执行当前相位的位移/动画/脚步，并刷新 Sprint 倾身。</summary>
    public void Tick(float deltaTime)
    {
        Context.DeltaTime = Mathf.Max(0f, deltaTime);
        Context.FrameSnapshot = Context.BuildSnapshot();
        _machine.Tick(Context.DeltaTime);
        if (_phases.TryGetValue(_machine.CurrentStateId, out LocomotionPhaseState phase))
            phase.ExecuteFrame(Context.DeltaTime);
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
            && Context.ResolveGaitRotationMode() == LocomotionRotationMode.FollowInput
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
