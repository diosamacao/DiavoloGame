using System;

/// <summary>
/// ACT 走跑预测模型：电机、2m Gate、出招/受击禁止走跑 Replay。
/// 连招超前与 Action Cancel 仍在 PredictedActionAckQueue。
/// </summary>
public sealed class ActCharacterPredictionModel : IPredictionModel<LocomotionPredictCommand, LocomotionPredictState>
{
    readonly CharacterMotorSim _motor;
    readonly PredictedLocomotionConfig _config;
    float _facingVelocityDeg;
    IPredictedLocomotionReplay _replay;
    ActorReplicationSnapshot _authoritySnapshot;
    bool _hasAuthoritySnapshot;

    /// <summary>绑定预测电机与速度参数。</summary>
    public ActCharacterPredictionModel(CharacterMotorSim motor, PredictedLocomotionConfig config)
    {
        _motor = motor ?? throw new ArgumentNullException(nameof(motor));
        _config = config;
    }

    /// <summary>当前预测电机。</summary>
    public CharacterMotorSim Motor => _motor;

    /// <summary>走跑配置。</summary>
    public PredictedLocomotionConfig Config => _config;

    /// <inheritdoc />
    public LocomotionPredictState Capture() =>
        new LocomotionPredictState(
            _motor.PositionMm.X,
            _motor.PositionMm.Z,
            _motor.YMm,
            _motor.FacingMilliDeg);

    /// <inheritdoc />
    public void Restore(in LocomotionPredictState authorityState)
    {
        ApplyPose(in authorityState);
        if (_replay != null && _hasAuthoritySnapshot)
            _replay.RestoreFromAuthority(in _authoritySnapshot);
    }

    /// <inheritdoc />
    public bool TrySimulate(in LocomotionPredictCommand command, in PredictionCorrectionPolicy policy)
    {
        // Input 是属性，不能直接 in 传参（CS8156），必须先落到局部。
        InputFrame input = command.Input;
        if (policy.ReplayKind == ActPredictionReplayKind.Runner)
        {
            if (command.SkipRunnerReplay || _replay == null)
                return false;
            _replay.ReplayTick(in input);
            return true;
        }

        if (command.SkipWishReplay)
            return false;

        PredictedLocomotionMath.ApplyInput(
            _motor,
            in input,
            in _config,
            ref _facingVelocityDeg);
        return true;
    }

    /// <inheritdoc />
    public int MeasureError(in LocomotionPredictState authority, in LocomotionPredictState predicted) =>
        PredictedLocomotionMath.PlanarErrorMm(
            predicted.PosXMm,
            predicted.PosZMm,
            authority.PosXMm,
            authority.PosZMm);

    /// <summary>纠偏前绑定 Runner 重放口；仅 Runner 策略需要。</summary>
    public void BindReplay(IPredictedLocomotionReplay replay, in ActorReplicationSnapshot authority)
    {
        _replay = replay;
        _authoritySnapshot = authority;
        _hasAuthoritySnapshot = replay != null;
    }

    /// <summary>一次对照结束后解除 Runner 绑定，避免下一次误 Restore 动作相位。</summary>
    public void UnbindReplay()
    {
        _replay = null;
        _hasAuthoritySnapshot = false;
    }

    /// <summary>把电机吸到权威位姿并清转向阻尼。</summary>
    public void ApplyPose(in LocomotionPredictState state)
    {
        _motor.TeleportMm(state.PosXMm, state.PosYMm, state.PosZMm);
        _motor.SetFacingMilliDeg(state.FacingMilliDeg);
        _facingVelocityDeg = 0f;
    }

    /// <summary>用本帧输入推进预测电机。</summary>
    public void ApplyInput(in InputFrame input, int planarSpeedMm = 0)
    {
        PredictedLocomotionMath.ApplyInput(
            _motor,
            in input,
            in _config,
            ref _facingVelocityDeg,
            planarSpeedMm);
    }

    /// <summary>2m Gate、宽限与出招/受击策略；Coordinator 只执行结果。</summary>
    public static PredictionCorrectionPolicy ResolvePolicy(
        int errorMm,
        int thresholdMm,
        bool withinGrace,
        bool hasRunnerReplay,
        bool authorityActionOrHit)
    {
        if (errorMm <= thresholdMm || withinGrace)
            return PredictionCorrectionPolicy.AcknowledgeOnly;

        if (hasRunnerReplay && !authorityActionOrHit)
        {
            return new PredictionCorrectionPolicy(
                correctionRequired: true,
                allowReplay: true,
                ActPredictionReplayKind.Runner);
        }

        return new PredictionCorrectionPolicy(
            correctionRequired: true,
            allowReplay: true,
            ActPredictionReplayKind.Wish);
    }

    /// <summary>权威正在出招或本 Tick 受击/死亡，走跑不得 Restore+Replay。</summary>
    public static bool IsActionOrHit(in ActorReplicationSnapshot snapshot) =>
        snapshot.ActionId != 0
        || snapshot.VitalityEdge == VitalityReplicationEdge.Hit
        || snapshot.VitalityEdge == VitalityReplicationEdge.Death;
}
