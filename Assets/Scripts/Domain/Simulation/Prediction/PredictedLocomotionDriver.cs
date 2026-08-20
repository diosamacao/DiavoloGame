using System;

/// <summary>
/// ACT 走跑预测门面：电机推进与 2m/宽限策略。
/// 命令/状态历史与 Restore+Replay 交给通用 PredictionCoordinator。
/// Listen 本机与远端客机共用。
/// </summary>
public sealed class PredictedLocomotionDriver
{
    const int MaxPending = 180;
    const int SnapGraceFrames = 8;

    readonly ActCharacterPredictionModel _model;
    readonly PredictionCoordinator<LocomotionPredictCommand, LocomotionPredictState> _coordinator;
    int _snapGraceFrames;

    /// <summary>绑定电机副本与速度/阈值；motor 不得再交给 SimulationWorld 当权威。</summary>
    public PredictedLocomotionDriver(CharacterMotorSim motor, PredictedLocomotionConfig config)
    {
        _model = new ActCharacterPredictionModel(motor, config);
        _coordinator = new PredictionCoordinator<LocomotionPredictCommand, LocomotionPredictState>(
            _model,
            MaxPending);
    }

    /// <summary>预测电机；只允许本驱动与和解写入。</summary>
    public CharacterMotorSim Motor => _model.Motor;

    /// <summary>当前预测参数（走跑冲刺/转向平滑/纠偏阈）。</summary>
    public PredictedLocomotionConfig Config => _model.Config;

    /// <summary>尚未被权威确认的输入条数。</summary>
    public int PendingCount => _coordinator.PendingCount;

    /// <summary>当前纠偏阈值（毫米）。</summary>
    public int ReconcileThresholdMm => _model.Config.ReconcileThresholdMm;

    /// <summary>通用纠偏计数，供 HUD 显示 snap / replay。</summary>
    public ReconcileMetrics Metrics => _coordinator.Metrics;

    /// <summary>
    /// 内层机已写出 MotorSim 后只记账。skipWishReplay：禁止 ApplyInput。
    /// skipRunnerReplay=false：纠偏经 Runner 重放。
    /// </summary>
    public void RecordAutonomous(in InputFrame input)
    {
        RecordPending(in input, skipWishReplay: true, skipRunnerReplay: false);
    }

    /// <summary>用本帧输入按 FollowInput 推进预测电机并缓存 (frame, input, pose)。</summary>
    public void Predict(in InputFrame input, int planarSpeedMm = 0)
    {
        _model.ApplyInput(in input, planarSpeedMm);
        RecordPending(in input, skipWishReplay: false, skipRunnerReplay: true);
    }

    /// <summary>
    /// 出招/受击/起步/折返/急停：直接贴齐权威电机位姿并记账，不跑 wish 预测。
    /// 避免烘焙位移只出现在延迟 Tick 上、纠偏时按 10Hz 吸附。
    /// </summary>
    public void PredictAligned(in InputFrame input, CharacterMotorSim authorityMotor)
    {
        if (authorityMotor == null)
            throw new ArgumentNullException(nameof(authorityMotor));

        CopyMotorPose(authorityMotor);
        RecordPending(in input, skipWishReplay: true, skipRunnerReplay: true);
    }

    /// <summary>只改预测电机，不追加 pending；供纠偏后把出招帧重新贴回权威。</summary>
    public void SnapMotorTo(CharacterMotorSim authorityMotor)
    {
        if (authorityMotor == null)
            throw new ArgumentNullException(nameof(authorityMotor));

        CopyMotorPose(authorityMotor);
    }

    /// <summary>真实客机没有权威 Motor 引用时，用快照毫米位姿贴齐并记账。</summary>
    public void PredictAlignedToSnapshot(in InputFrame input, in ActorReplicationSnapshot authority)
    {
        _model.ApplyPose(LocomotionPredictState.FromSnapshot(in authority));
        RecordPending(in input, skipWishReplay: true, skipRunnerReplay: true);
    }

    /// <summary>只把预测电机吸到快照，不追加 pending；纠偏后烘焙相位仍以权威为准。</summary>
    public void SnapToSnapshot(in ActorReplicationSnapshot authority)
    {
        _model.ApplyPose(LocomotionPredictState.FromSnapshot(in authority));
        _snapGraceFrames = SnapGraceFrames;
    }

    /// <summary>走跑带 Runner 时的硬吸阈；房间禁止再传 50mm。</summary>
    public const int AutonomousHardSnapMm = 2000;

    /// <summary>刚吸附后若干逻辑步内，小于此误差不再连吸，避免 50mm 上下抖。</summary>
    public const int SnapGraceMaxErrorMm = 150;

    /// <summary>无 Runner 时的和解（单测 / 旧 Predict 路径）。</summary>
    public PredictedReconcileResult Reconcile(
        long authorityFrame,
        in ActorReplicationSnapshot authority,
        int snapThresholdMm = -1) =>
        Reconcile(authorityFrame, in authority, replay: null, snapThresholdMm);

    /// <summary>
    /// 用权威帧位姿和解。误差 ≤ 阈值只 Ack。
    /// 走跑且提供 replay：未显式传阈时用 <see cref="AutonomousHardSnapMm"/>（2m）。
    /// 出招/受击：只吸 Pose。无 replay 时非 aligned 仍 ApplyInput（旧单测，默认 50mm）。
    /// </summary>
    public PredictedReconcileResult Reconcile(
        long authorityFrame,
        in ActorReplicationSnapshot authority,
        IPredictedLocomotionReplay replay,
        int snapThresholdMm = -1)
    {
        LocomotionPredictState authorityPose = LocomotionPredictState.FromSnapshot(in authority);
        int errorMm = _coordinator.PeekError(authorityFrame, in authorityPose);
        int threshold = snapThresholdMm >= 0
            ? snapThresholdMm
            : replay != null
                ? AutonomousHardSnapMm
                : _model.Config.ReconcileThresholdMm;
        if (_snapGraceFrames > 0)
            _snapGraceFrames--;

        bool withinGrace = _snapGraceFrames > 0 && errorMm <= SnapGraceMaxErrorMm;
        bool useRunner = replay != null && !ActCharacterPredictionModel.IsActionOrHit(in authority);
        PredictionCorrectionPolicy policy = ActCharacterPredictionModel.ResolvePolicy(
            errorMm,
            threshold,
            withinGrace,
            hasRunnerReplay: useRunner,
            authorityActionOrHit: ActCharacterPredictionModel.IsActionOrHit(in authority));

        if (policy.CorrectionRequired && useRunner)
            _model.BindReplay(replay, in authority);

        PredictionReconcileResult generic = _coordinator.ReceiveAuthority(
            authorityFrame,
            in authorityPose,
            in policy);
        _model.UnbindReplay();

        if (generic.Snapped)
            _snapGraceFrames = SnapGraceFrames;

        return new PredictedReconcileResult(generic.Snapped, generic.Error, generic.ReplayedCommands);
    }

    /// <summary>把权威毫米位姿拷到预测电机，并清空转向阻尼以免回摆。</summary>
    void CopyMotorPose(CharacterMotorSim authorityMotor)
    {
        _model.ApplyPose(new LocomotionPredictState(
            authorityMotor.PositionMm.X,
            authorityMotor.PositionMm.Z,
            authorityMotor.YMm,
            authorityMotor.FacingMilliDeg));
    }

    void RecordPending(in InputFrame input, bool skipWishReplay, bool skipRunnerReplay)
    {
        var command = new LocomotionPredictCommand(in input, skipWishReplay, skipRunnerReplay);
        _coordinator.Record(input.Frame, in command, _model.Capture());
    }
}
