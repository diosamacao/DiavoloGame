using NUnit.Framework;

/// <summary>复制位姿写入 MotorSim，不依赖 Unity Transform。</summary>
public sealed class ReplicationPoseApplierTests
{
    /// <summary>快照毫米坐标与朝向原样落到电机。</summary>
    [Test]
    public void ApplyToMotor_CopiesPoseAndFacing()
    {
        var motor = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, radiusMm: 280);
        motor.TeleportMm(0, 0, 0);
        motor.SetFacingMilliDeg(0);

        var snapshot = new ActorReplicationSnapshot(
            new SimActorId(1),
            teamId: 1,
            ReplicationActorKind.Player,
            posXMm: 2500,
            posZMm: -800,
            posYMm: 100,
            facingMilliDeg: 90000,
            moveVxMm: 0,
            moveVzMm: 0,
            locomotionPhase: 0,
            gait: 0,
            cardinal: 0,
            actionId: 0,
            graphNodeKey: 0,
            actionFrame: 0,
            freezeFrames: 0,
            selectedTargetId: SimActorId.Invalid,
            healthMilli: 100000,
            flagsPacked: 0,
            VitalityReplicationEdge.None);

        ReplicationPoseApplier.ApplyToMotor(motor, in snapshot);

        Assert.That(motor.PositionMm.X, Is.EqualTo(2500));
        Assert.That(motor.PositionMm.Z, Is.EqualTo(-800));
        Assert.That(motor.YMm, Is.EqualTo(100));
        Assert.That(motor.FacingMilliDeg, Is.EqualTo(90000));
    }
}
