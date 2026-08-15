using UnityEngine;

/// <summary>
/// 客机本机走跑：同一套 <see cref="LocomotionStateMachine"/> 写预测 MotorSim 与 Animation。
/// 不进 SimulationWorld，不 Collect，不写 Numeric。纠偏经 <see cref="IPredictedLocomotionReplay"/>。
/// </summary>
public sealed class AutonomousLocomotionRunner : IPredictedLocomotionReplay
{
    readonly InputManager _input;
    readonly CharacterMotor _motor;
    readonly CharacterAnimationService _animation;
    readonly LocomotionStateMachine _machine;
    readonly float _fixedDeltaSeconds;
    bool _entered;

    /// <summary>
    /// 在已有 Motor/Animation/Profile 上装配内层机；moveIntent 必须是可 Ingest 的同一 <see cref="InputManager"/>。
    /// </summary>
    public AutonomousLocomotionRunner(
        InputManager input,
        CharacterMotor motor,
        CharacterAnimationService animation,
        CharacterLocomotionProfile profile,
        Transform root,
        float fixedDeltaSeconds)
    {
        _input = input ?? throw new System.ArgumentNullException(nameof(input));
        _motor = motor ?? throw new System.ArgumentNullException(nameof(motor));
        _animation = animation ?? throw new System.ArgumentNullException(nameof(animation));
        if (profile == null)
            throw new System.ArgumentNullException(nameof(profile));
        if (root == null)
            throw new System.ArgumentNullException(nameof(root));

        _fixedDeltaSeconds = fixedDeltaSeconds > 0f
            ? fixedDeltaSeconds
            : 1f / SimulationConfig.DefaultLogicHz;
        var footstepPlayer = new LocomotionFootstepPlayer(root, profile);
        _machine = new LocomotionStateMachine(
            root,
            motor,
            animation,
            input,
            profile,
            footstepPlayer);
    }

    /// <summary>与出招 Runner 共用的输入中枢。</summary>
    public InputManager Input => _input;

    /// <summary>内层机；供意图生产器读 Sprint 条件。</summary>
    public LocomotionStateMachine Locomotion => _machine;

    /// <summary>内层机已 Enter、正在推进走跑。</summary>
    public bool IsActive => _entered;

    /// <summary>与预测体共用的电机；纠偏只改这份 Sim。</summary>
    public CharacterMotorSim MotorSim => _motor.Sim;

    /// <summary>逻辑帧 wish，供本机朝向调试箭头。</summary>
    public Vector3 DebugWishWorld => _motor.DebugWishWorldDirection;

    /// <summary>本机 Sprint 倾身；Lean 不进 Snapshot。</summary>
    public float LeanRollDegrees => _entered ? _machine.Context.SprintLeanRollDegrees : 0f;

    /// <summary>摄入本帧输入并推进内层机、重力与 Clip 时间。</summary>
    public void Tick(in InputFrame input) => Tick(in input, default);

    /// <summary>
    /// 摄入本帧输入并推进内层机。尚未 Enter 时消费 resume（闪避后 Sprint）。
    /// </summary>
    public void Tick(in InputFrame input, in LocomotionResumeRequest resumeRequest)
    {
        _input.IngestFrame(input);
        if (!_entered)
            Enter(in resumeRequest);

        StepSimulation();
    }

    /// <inheritdoc />
    public void ReplayTick(in InputFrame input)
    {
        _input.IngestFrame(input);
        if (!_entered)
            Enter(default);

        StepSimulation();
    }

    /// <inheritdoc />
    public void RestoreFromAuthority(in ActorReplicationSnapshot authority)
    {
        // 纠偏一次性对齐：允许 SyncRootPoseFromSim 清转向阻尼
        _motor.SyncRootPoseFromSim();
        LocomotionSavedState state = LocomotionSavedState.FromAuthority(in authority);
        _machine.Restore(in state);
        _entered = true;
    }

    /// <summary>进入顶层 Locomotion；默认从 Idle 起，有输入时同帧可进 Start。</summary>
    public void Enter(in LocomotionResumeRequest resumeRequest)
    {
        _machine.Enter(in resumeRequest);
        _entered = true;
    }

    /// <summary>出招/受击时离开走跑，停止烘焙根位移；下次 Tick 会重新 Enter。</summary>
    public void Exit()
    {
        if (!_entered)
            return;

        _machine.Exit();
        _entered = false;
    }

    /// <summary>与 CharacterActor.Step 同序：先重力，再内层机写水平位移。</summary>
    void StepSimulation()
    {
        _motor.TickGravity(_fixedDeltaSeconds);
        _machine.Tick(_fixedDeltaSeconds);
        _animation.SetSpeed(1f);
        _animation.Tick(_fixedDeltaSeconds);
        // 禁止每帧 SyncRootPoseFromSim：会清零转向阻尼，客机转向硬切。
    }
}
