using NUnit.Framework;

/// <summary>预测位移自洽、阈值内不吸附、超阈重放等于权威+后续输入、撞墙回拉。</summary>
public sealed class PredictedLocomotionReconcileTests
{
    /// <summary>同一输入脚本跑两条预测路径，水平误差 ≤ 1mm（RTT=0 自洽）。</summary>
    [Test]
    public void RttZero_SameInputs_TwoDrivers_ErrorAtMost1mm()
    {
        var left = CreateDriver(OpenFieldSimCollisionWorld.Instance);
        var right = CreateDriver(OpenFieldSimCollisionWorld.Instance);
        SimActorId id = new SimActorId(1);

        for (int i = 0; i < 60; i++)
            PredictForward(left, id, i);
        for (int i = 0; i < 60; i++)
            PredictForward(right, id, i);

        int error = PredictedLocomotionMath.PlanarErrorMm(
            left.Motor.PositionMm.X,
            left.Motor.PositionMm.Z,
            right.Motor.PositionMm.X,
            right.Motor.PositionMm.Z);
        Assert.That(error, Is.LessThanOrEqualTo(1));
    }

    /// <summary>误差在 50mm 内只 Ack，不吸附、不重放。</summary>
    [Test]
    public void Reconcile_WithinThreshold_DoesNotSnap()
    {
        var driver = CreateDriver(OpenFieldSimCollisionWorld.Instance);
        SimActorId id = new SimActorId(1);
        for (int i = 0; i < 10; i++)
            PredictForward(driver, id, i);

        int x = driver.Motor.PositionMm.X;
        int z = driver.Motor.PositionMm.Z;
        ActorReplicationSnapshot authority = PoseSnapshot(id, x + 30, z, driver.Motor.YMm, driver.Motor.FacingMilliDeg);

        PredictedReconcileResult result = driver.Reconcile(9, in authority);

        Assert.That(result.Snapped, Is.False);
        Assert.That(result.PlanarErrorMm, Is.EqualTo(30));
        Assert.That(result.ReplayedInputs, Is.Zero);
        Assert.That(driver.Motor.PositionMm.X, Is.EqualTo(x));
        Assert.That(driver.PendingCount, Is.Zero);
    }

    /// <summary>超阈吸附后重放未确认输入，最终等于「权威 pose + 后续同一输入」。</summary>
    [Test]
    public void Reconcile_OverThreshold_ReplayEqualsAuthorityPlusLaterInputs()
    {
        var world = OpenFieldSimCollisionWorld.Instance;
        var driver = CreateDriver(world);
        var expected = CreateDriver(world);
        SimActorId id = new SimActorId(1);

        for (int i = 0; i <= 10; i++)
            PredictForward(driver, id, i);

        ActorReplicationSnapshot authority = PoseSnapshot(id, 0, 0, 0, 0);
        PredictedReconcileResult result = driver.Reconcile(5, in authority);

        Assert.That(result.Snapped, Is.True);
        Assert.That(result.PlanarErrorMm, Is.GreaterThan(50));
        Assert.That(result.ReplayedInputs, Is.EqualTo(5));

        ReplicationPoseApplier.ApplyToMotor(expected.Motor, in authority);
        for (int i = 6; i <= 10; i++)
            PredictForward(expected, id, i);

        int error = PredictedLocomotionMath.PlanarErrorMm(
            driver.Motor.PositionMm.X,
            driver.Motor.PositionMm.Z,
            expected.Motor.PositionMm.X,
            expected.Motor.PositionMm.Z);
        Assert.That(error, Is.LessThanOrEqualTo(1));
        Assert.That(driver.Motor.FacingMilliDeg, Is.EqualTo(expected.Motor.FacingMilliDeg));
    }

    /// <summary>W 起步后改 D：首帧朝向不得瞬间到 90°，位移沿朝向而不是瞬时横移。</summary>
    [Test]
    public void ApplyInput_FollowInput_DoesNotSnapFacingOnFirstStrafeFrame()
    {
        var motor = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, radiusMm: 280);
        motor.SetFacingMilliDeg(0);
        float facingVelocity = 0f;
        var input = new InputFrame(
            0,
            new SimActorId(1),
            moveX: 127,
            moveY: 0,
            0ul,
            0ul,
            0ul,
            moveReferenceYawQuantized: 0);

        PredictedLocomotionMath.ApplyInput(
            motor,
            in input,
            PredictedLocomotionConfig.Default,
            ref facingVelocity);

        Assert.That(motor.FacingMilliDeg, Is.GreaterThan(0));
        Assert.That(motor.FacingMilliDeg, Is.LessThan(45 * MotionQuantization.MilliDegPerDeg));
        Assert.That(motor.PositionMm.Z, Is.GreaterThan(motor.PositionMm.X));
    }

    /// <summary>贴齐权威后 pending 与权威同位姿，和解不吸附。</summary>
    [Test]
    public void PredictAligned_ThenReconcileSamePose_DoesNotSnap()
    {
        var world = OpenFieldSimCollisionWorld.Instance;
        var driver = CreateDriver(world);
        var authority = new CharacterMotorSim(world, radiusMm: 280);
        authority.TeleportMm(2500, 0, 1800);
        authority.SetFacingMilliDeg(45000);
        SimActorId id = new SimActorId(1);
        var input = new InputFrame(3, id, 0, 127, 0ul, 0ul, 0ul, 0);

        driver.PredictAligned(in input, authority);

        Assert.That(driver.Motor.PositionMm.X, Is.EqualTo(2500));
        Assert.That(driver.Motor.PositionMm.Z, Is.EqualTo(1800));
        Assert.That(driver.Motor.FacingMilliDeg, Is.EqualTo(45000));

        ActorReplicationSnapshot snapshot = PoseSnapshot(
            id,
            2500,
            1800,
            0,
            45000);
        PredictedReconcileResult result = driver.Reconcile(3, in snapshot);

        Assert.That(result.Snapped, Is.False);
        Assert.That(result.PlanarErrorMm, Is.EqualTo(0));
        Assert.That(driver.Motor.PositionMm.X, Is.EqualTo(2500));
    }

    /// <summary>客机用快照贴齐后，与同位姿和解不得吸附。</summary>
    [Test]
    public void PredictAlignedToSnapshot_ThenReconcileSamePose_DoesNotSnap()
    {
        var driver = CreateDriver(OpenFieldSimCollisionWorld.Instance);
        SimActorId id = new SimActorId(1);
        ActorReplicationSnapshot snapshot = PoseSnapshot(id, 1200, 800, 0, 90000);
        var input = new InputFrame(4, id, 0, 127, 0ul, 0ul, 0ul, 0);

        driver.PredictAlignedToSnapshot(in input, in snapshot);
        PredictedReconcileResult result = driver.Reconcile(4, in snapshot);

        Assert.That(result.Snapped, Is.False);
        Assert.That(driver.Motor.PositionMm.X, Is.EqualTo(1200));
        Assert.That(driver.Motor.PositionMm.Z, Is.EqualTo(800));
        Assert.That(driver.Motor.FacingMilliDeg, Is.EqualTo(90000));
    }

    /// <summary>预测无墙沿 +Z 穿过去，权威有墙停住；和解后拉回权威边缘。</summary>
    [Test]
    public void Reconcile_Wall_AuthorityStops_SnapPullsBack()
    {
        // PredictForward 是 yaw0 + 前向，FollowInput 沿 +Z；墙必须挡 Z 而不是 X
        var wall = new SimStaticAabb(-500, 500, 1000, 2000);
        var blocked = new SimStaticCollisionWorld(0, new[] { wall });
        var open = OpenFieldSimCollisionWorld.Instance;

        var predicted = CreateDriver(open);
        var authorityMotor = new CharacterMotorSim(blocked, radiusMm: 280);
        var authorityDriver = new PredictedLocomotionDriver(
            authorityMotor,
            PredictedLocomotionConfig.Default);
        SimActorId id = new SimActorId(1);

        for (int i = 0; i < 40; i++)
        {
            PredictForward(predicted, id, i);
            PredictForward(authorityDriver, id, i);
        }

        Assert.That(predicted.Motor.PositionMm.Z, Is.GreaterThan(authorityMotor.PositionMm.Z + 50));

        ActorReplicationSnapshot authority = PoseSnapshot(
            id,
            authorityMotor.PositionMm.X,
            authorityMotor.PositionMm.Z,
            authorityMotor.YMm,
            authorityMotor.FacingMilliDeg);
        PredictedReconcileResult result = predicted.Reconcile(39, in authority);

        Assert.That(result.Snapped, Is.True);
        Assert.That(predicted.Motor.PositionMm.X, Is.EqualTo(authorityMotor.PositionMm.X));
        Assert.That(predicted.Motor.PositionMm.Z, Is.EqualTo(authorityMotor.PositionMm.Z));
    }

    static PredictedLocomotionDriver CreateDriver(ISimCollisionWorld world) =>
        new PredictedLocomotionDriver(
            new CharacterMotorSim(world, radiusMm: 280),
            PredictedLocomotionConfig.Default);

    static void PredictForward(PredictedLocomotionDriver driver, SimActorId id, int frame)
    {
        var input = new InputFrame(
            frame,
            id,
            moveX: 0,
            moveY: 127,
            0ul,
            0ul,
            0ul,
            moveReferenceYawQuantized: 0);
        driver.Predict(in input);
    }

    static ActorReplicationSnapshot PoseSnapshot(
        SimActorId id,
        int xMm,
        int zMm,
        int yMm,
        int facingMilliDeg) =>
        new ActorReplicationSnapshot(
            id,
            1,
            ReplicationActorKind.Player,
            xMm,
            zMm,
            yMm,
            facingMilliDeg,
            0,
            0,
            0,
            0,
            0,
            0,
            string.Empty,
            0,
            0,
            SimActorId.Invalid,
            100000,
            0,
            VitalityReplicationEdge.None);
}
