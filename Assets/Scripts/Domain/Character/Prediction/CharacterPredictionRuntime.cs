/// <summary>独占 Autonomous 角色的动作取消、权威 Locomotion 恢复与输入重放。</summary>
public sealed class CharacterPredictionRuntime : IPredictedLocomotionReplay
{
    readonly InputManager _inputManager;
    readonly CharacterMotor _motor;
    readonly CharacterStateMachine _stateMachine;
    readonly ActionSim _actionSim;
    readonly GameplayIntentBuffer _intentBuffer;
    readonly float _fixedDeltaSeconds;

    /// <summary>创建角色预测运行时；只复用模拟状态，禁止推进真实动画。</summary>
    internal CharacterPredictionRuntime(
        InputManager inputManager,
        CharacterMotor motor,
        CharacterStateMachine stateMachine,
        ActionSim actionSim,
        GameplayIntentBuffer intentBuffer,
        float fixedDeltaSeconds)
    {
        _inputManager = inputManager;
        _motor = motor;
        _stateMachine = stateMachine;
        _actionSim = actionSim;
        _intentBuffer = intentBuffer;
        _fixedDeltaSeconds = fixedDeltaSeconds;
    }

    /// <summary>Ack 否决本地招时终止动作、清空缓冲并返回 Locomotion。</summary>
    public void StopAutonomousAction()
    {
        _actionSim?.Stop();
        _intentBuffer?.ClearAllBuffers();
        if (_stateMachine.CurrentStateId == CharacterStateType.Action)
            _stateMachine.TryChangeState(CharacterStateType.Locomotion, force: true);
    }

    /// <inheritdoc />
    public void RestoreFromAuthority(in ActorReplicationSnapshot authority)
    {
        _motor.Sim.TeleportMm(authority.PosXMm, authority.PosYMm, authority.PosZMm);
        _motor.Sim.SetFacingMilliDeg(authority.FacingMilliDeg);
        LocomotionSavedState locomotion = LocomotionSavedState.FromSnapshot(in authority);
        RestoreLocomotion(in locomotion);
    }

    /// <inheritdoc />
    public void ReplayTick(in InputFrame input)
    {
        // 纠偏 Replay 只重放走跑，不重跑 Targeting、Action 或 Numeric。
        _inputManager.IngestFrame(input);
        _motor.TickGravity(_fixedDeltaSeconds);
        _stateMachine.Locomotion?.Tick(_fixedDeltaSeconds);
    }

    /// <summary>用完整整数帧状态恢复 Locomotion，并确保顶层状态已回到走跑。</summary>
    void RestoreLocomotion(in LocomotionSavedState state)
    {
        _motor.SyncRootPoseFromSim();
        LocomotionStateMachine locomotion = _stateMachine.Locomotion;
        if (locomotion == null)
            return;

        if (_stateMachine.CurrentStateId != CharacterStateType.Locomotion)
            _stateMachine.TryChangeState(CharacterStateType.Locomotion, force: true);
        locomotion.Restore(in state);
    }
}
