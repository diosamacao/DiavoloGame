using System;

/// <summary>把复制快照的毫米位姿写入 MotorSim；不含动画、命中或 Transform。</summary>
public static class ReplicationPoseApplier
{
    /// <summary>瞬移电机到快照位姿；不经碰撞，供幽灵与纠偏共用。</summary>
    public static void ApplyToMotor(CharacterMotorSim motor, in ActorReplicationSnapshot snapshot)
    {
        if (motor == null)
            throw new ArgumentNullException(nameof(motor));

        motor.TeleportMm(snapshot.PosXMm, snapshot.PosYMm, snapshot.PosZMm);
        motor.SetFacingMilliDeg(snapshot.FacingMilliDeg);
    }
}
