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

    /// <summary>用本帧输入按 FollowInput 推进预测电机并缓存 (frame, input, pose)。</summary>
    public void Predict(in InputFrame input, int planarSpeedMm = 0)
    {
        PredictedLocomotionMath.ApplyInput(
            _motor,
            in input,
            in _config,
            ref _facingVelocityDeg,
            planarSpeedMm);
        RecordPending(in input, aligned: false);
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
        RecordPending(in input, aligned: true);
    }

    /// <summary>只改预测电机，不追加 pending；供纠偏后把出招帧重新贴回权威。</summary>
    public void SnapMotorTo(CharacterMotorSim authorityMotor)
    {
        if (authorityMotor == null)
            throw new ArgumentNullException(nameof(authorityMotor));

        CopyMotorPose(authorityMotor);
    }

    /// <summary>
    /// 用权威帧位姿和解。误差 ≤ 阈值只丢弃该帧及更旧缓存；超阈吸附后重放更新的输入。
    /// 禁止把表现 Pose 写回本电机。
    /// </summary>
    public PredictedReconcileResult Reconcile(
        long authorityFrame,
        in ActorReplicationSnapshot authority)
    {
        int errorMm = ResolveErrorAgainstPredictedFrame(authorityFrame, in authority);
        DropAcked(authorityFrame);

        if (errorMm <= _config.ReconcileThresholdMm)
            return new PredictedReconcileResult(snapped: false, errorMm, replayedInputs: 0);

        ReplicationPoseApplier.ApplyToMotor(_motor, in authority);
        _facingVelocityDeg = 0f;
        int replayed = 0;
        for (int i = 0; i < _pending.Count; i++)
        {
            PendingCommand pending = _pending[i];
            // 贴齐帧没有 wish 重放式；强行 ApplyInput 会把攻击/转身烘焙位移冲掉
            if (pending.Aligned)
                continue;

            // in 参数必须是可寻址变量，不能直接传属性
            InputFrame replayInput = pending.Input;
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
                aligned: false);
            replayed++;
        }

        return new PredictedReconcileResult(snapped: true, errorMm, replayed);
    }

    /// <summary>把权威毫米位姿拷到预测电机，并清空转向阻尼以免回摆。</summary>
    void CopyMotorPose(CharacterMotorSim authorityMotor)
    {
        _motor.TeleportMm(authorityMotor.PositionMm.X, authorityMotor.YMm, authorityMotor.PositionMm.Z);
        _motor.SetFacingMilliDeg(authorityMotor.FacingMilliDeg);
        _facingVelocityDeg = 0f;
    }

    void RecordPending(in InputFrame input, bool aligned)
    {
        _pending.Add(new PendingCommand(
            input.Frame,
            input,
            _motor.PositionMm.X,
            _motor.PositionMm.Z,
            _motor.YMm,
            _motor.FacingMilliDeg,
            aligned));

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
            bool aligned)
        {
            Frame = frame;
            Input = input;
            PosXMm = posXMm;
            PosZMm = posZMm;
            PosYMm = posYMm;
            FacingMilliDeg = facingMilliDeg;
            Aligned = aligned;
        }

        public long Frame { get; }
        public InputFrame Input { get; }
        public int PosXMm { get; }
        public int PosZMm { get; }
        public int PosYMm { get; }
        public int FacingMilliDeg { get; }
        public bool Aligned { get; }
    }
}
