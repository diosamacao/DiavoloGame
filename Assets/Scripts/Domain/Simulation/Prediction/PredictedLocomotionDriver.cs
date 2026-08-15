using System;
using System.Collections.Generic;

/// <summary>
/// 远端客户端预测位移：本地 InputFrame 推进 MotorSim 副本，权威超阈则吸附并重放未确认输入。
/// Listen Host 本地玩家不得使用本驱动。
/// </summary>
public sealed class PredictedLocomotionDriver
{
    const int MaxPending = 180;

    readonly CharacterMotorSim _motor;
    readonly PredictedLocomotionConfig _config;
    readonly List<PendingCommand> _pending = new(32);
    float _facingVelocityDeg;
    int _snapGraceFrames;

    /// <summary>绑定电机副本与速度/阈值；motor 不得再交给 SimulationWorld 当权威。</summary>
    public PredictedLocomotionDriver(CharacterMotorSim motor, PredictedLocomotionConfig config)
    {
        _motor = motor ?? throw new ArgumentNullException(nameof(motor));
        _config = config;
    }

    /// <summary>预测电机；只允许本驱动与和解写入。</summary>
    public CharacterMotorSim Motor => _motor;

    /// <summary>当前预测参数（走跑冲刺/转向平滑/纠偏阈）。</summary>
    public PredictedLocomotionConfig Config => _config;

    /// <summary>尚未被权威确认的输入条数。</summary>
    public int PendingCount => _pending.Count;

    /// <summary>当前纠偏阈值（毫米）。</summary>
    public int ReconcileThresholdMm => _config.ReconcileThresholdMm;

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
        PredictedLocomotionMath.ApplyInput(
            _motor,
            in input,
            in _config,
            ref _facingVelocityDeg,
            planarSpeedMm);
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
        ReplicationPoseApplier.ApplyToMotor(_motor, in authority);
        _facingVelocityDeg = 0f;
        RecordPending(in input, skipWishReplay: true, skipRunnerReplay: true);
    }

    /// <summary>只把预测电机吸到快照，不追加 pending；纠偏后烘焙相位仍以权威为准。</summary>
    public void SnapToSnapshot(in ActorReplicationSnapshot authority)
    {
        ReplicationPoseApplier.ApplyToMotor(_motor, in authority);
        _facingVelocityDeg = 0f;
        // 硬贴后同样给宽限，避免下一包 50mm 上下立刻再吸。
        _snapGraceFrames = SnapGraceFrames;
    }

    /// <summary>UE1 过渡硬吸阈；UE2 房间改走 Runner 重放，不再使用。</summary>
    public const int AutonomousHardSnapMm = 2000;

    /// <summary>刚吸附后若干逻辑步内，小于此误差不再连吸，避免 50mm 上下抖。</summary>
    public const int SnapGraceMaxErrorMm = 150;

    const int SnapGraceFrames = 8;

    /// <summary>无 Runner 时的和解（单测 / 旧 Predict 路径）。</summary>
    public PredictedReconcileResult Reconcile(
        long authorityFrame,
        in ActorReplicationSnapshot authority,
        int snapThresholdMm = -1) =>
        Reconcile(authorityFrame, in authority, replay: null, snapThresholdMm);

    /// <summary>
    /// 用权威帧位姿和解。误差 ≤ 阈值只 Ack。
    /// 走跑且提供 replay：未显式传阈时用 <see cref="AutonomousHardSnapMm"/>（2m），
    /// 禁止再用 50mm——内层机与 Host 常态偏差就会每包 Restore+Replay，表现为卡顿。
    /// 出招/受击：只吸 Pose。无 replay 时非 aligned 仍 ApplyInput（旧单测，默认 50mm）。
    /// </summary>
    public PredictedReconcileResult Reconcile(
        long authorityFrame,
        in ActorReplicationSnapshot authority,
        IPredictedLocomotionReplay replay,
        int snapThresholdMm = -1)
    {
        int errorMm = ResolveErrorAgainstPredictedFrame(authorityFrame, in authority);
        DropAcked(authorityFrame);

        int threshold = snapThresholdMm >= 0
            ? snapThresholdMm
            : replay != null
                ? AutonomousHardSnapMm
                : _config.ReconcileThresholdMm;
        // 先消耗本包宽限，再判断：刚吸附的若干权威包不再因 50～150mm 连吸。
        if (_snapGraceFrames > 0)
            _snapGraceFrames--;

        bool withinThreshold = errorMm <= threshold;
        bool withinGrace = _snapGraceFrames > 0 && errorMm <= SnapGraceMaxErrorMm;
        if (withinThreshold || withinGrace)
            return new PredictedReconcileResult(snapped: false, errorMm, replayedInputs: 0);

        ReplicationPoseApplier.ApplyToMotor(_motor, in authority);
        _facingVelocityDeg = 0f;

        bool useRunner = replay != null && !IsActionOrHit(in authority);
        if (useRunner)
            replay.RestoreFromAuthority(in authority);

        int replayed = 0;
        for (int i = 0; i < _pending.Count; i++)
        {
            PendingCommand pending = _pending[i];
            InputFrame replayInput = pending.Input;
            if (useRunner)
            {
                if (pending.SkipRunnerReplay)
                    continue;

                replay.ReplayTick(in replayInput);
                _pending[i] = new PendingCommand(
                    pending.Frame,
                    pending.Input,
                    _motor.PositionMm.X,
                    _motor.PositionMm.Z,
                    _motor.YMm,
                    _motor.FacingMilliDeg,
                    skipWishReplay: true,
                    skipRunnerReplay: false);
                replayed++;
                continue;
            }

            if (pending.SkipWishReplay)
                continue;

            PredictedLocomotionMath.ApplyInput(
                _motor,
                in replayInput,
                in _config,
                ref _facingVelocityDeg);
            _pending[i] = new PendingCommand(
                pending.Frame,
                pending.Input,
                _motor.PositionMm.X,
                _motor.PositionMm.Z,
                _motor.YMm,
                _motor.FacingMilliDeg,
                skipWishReplay: false,
                skipRunnerReplay: true);
            replayed++;
        }

        _snapGraceFrames = SnapGraceFrames;
        return new PredictedReconcileResult(snapped: true, errorMm, replayed);
    }

    /// <summary>权威正在出招或本 Tick 受击/死亡，走跑不得 Restore+Replay。</summary>
    static bool IsActionOrHit(in ActorReplicationSnapshot snapshot) =>
        snapshot.ActionId != 0
        || snapshot.VitalityEdge == VitalityReplicationEdge.Hit
        || snapshot.VitalityEdge == VitalityReplicationEdge.Death;

    /// <summary>把权威毫米位姿拷到预测电机，并清空转向阻尼以免回摆。</summary>
    void CopyMotorPose(CharacterMotorSim authorityMotor)
    {
        _motor.TeleportMm(authorityMotor.PositionMm.X, authorityMotor.YMm, authorityMotor.PositionMm.Z);
        _motor.SetFacingMilliDeg(authorityMotor.FacingMilliDeg);
        _facingVelocityDeg = 0f;
    }

    void RecordPending(in InputFrame input, bool skipWishReplay, bool skipRunnerReplay)
    {
        _pending.Add(new PendingCommand(
            input.Frame,
            input,
            _motor.PositionMm.X,
            _motor.PositionMm.Z,
            _motor.YMm,
            _motor.FacingMilliDeg,
            skipWishReplay,
            skipRunnerReplay));

        if (_pending.Count > MaxPending)
            _pending.RemoveRange(0, _pending.Count - MaxPending);
    }

    int ResolveErrorAgainstPredictedFrame(long authorityFrame, in ActorReplicationSnapshot authority)
    {
        for (int i = 0; i < _pending.Count; i++)
        {
            if (_pending[i].Frame != authorityFrame)
                continue;

            return PredictedLocomotionMath.PlanarErrorMm(
                _pending[i].PosXMm,
                _pending[i].PosZMm,
                authority.PosXMm,
                authority.PosZMm);
        }

        return PredictedLocomotionMath.PlanarErrorMm(
            _motor.PositionMm.X,
            _motor.PositionMm.Z,
            authority.PosXMm,
            authority.PosZMm);
    }

    void DropAcked(long authorityFrame)
    {
        int keepFrom = 0;
        while (keepFrom < _pending.Count && _pending[keepFrom].Frame <= authorityFrame)
            keepFrom++;

        if (keepFrom > 0)
            _pending.RemoveRange(0, keepFrom);
    }

    readonly struct PendingCommand
    {
        public PendingCommand(
            long frame,
            in InputFrame input,
            int posXMm,
            int posZMm,
            int posYMm,
            int facingMilliDeg,
            bool skipWishReplay,
            bool skipRunnerReplay)
        {
            Frame = frame;
            Input = input;
            PosXMm = posXMm;
            PosZMm = posZMm;
            PosYMm = posYMm;
            FacingMilliDeg = facingMilliDeg;
            SkipWishReplay = skipWishReplay;
            SkipRunnerReplay = skipRunnerReplay;
        }

        public long Frame { get; }
        public InputFrame Input { get; }
        public int PosXMm { get; }
        public int PosZMm { get; }
        public int PosYMm { get; }
        public int FacingMilliDeg { get; }
        public bool SkipWishReplay { get; }
        public bool SkipRunnerReplay { get; }
    }
}
