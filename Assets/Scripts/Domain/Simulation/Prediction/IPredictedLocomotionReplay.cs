/// <summary>
/// 客机走跑纠偏重放口。由 Autonomous CharacterActor 实现。
/// Simulation 不引用内层机类型，避免程序集反向依赖。
/// </summary>
public interface IPredictedLocomotionReplay
{
    /// <summary>
    /// 按权威快照恢复相位/步态/Clip 时间。
    /// 位姿已由 <see cref="PredictedLocomotionDriver"/> 写入 MotorSim。
    /// </summary>
    void RestoreFromAuthority(in ActorReplicationSnapshot authority);

    /// <summary>用未确认 <see cref="InputFrame"/> 推进同一套内层机；禁止再走 ApplyInput。</summary>
    void ReplayTick(in InputFrame input);
}
