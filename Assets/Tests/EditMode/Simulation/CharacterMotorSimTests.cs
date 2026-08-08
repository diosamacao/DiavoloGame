using NUnit.Framework;

/// <summary>验证 MotorSim 毫米位移与本地→世界旋转，不依赖 Unity Physics。</summary>
public sealed class CharacterMotorSimTests
{
    /// <summary>世界位移直接累加毫米坐标。</summary>
    [Test]
    public void TryMoveWorldMm_AccumulatesPosition()
    {
        var motor = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, radiusMm: 280);
        motor.TeleportMm(0, 0);
        Assert.That(motor.TryMoveWorldMm(1000, 500), Is.True);
        Assert.That(motor.PositionMm.X, Is.EqualTo(1000));
        Assert.That(motor.PositionMm.Z, Is.EqualTo(500));
    }

    /// <summary>朝向 0° 时本地前向等于世界 +Z。</summary>
    [Test]
    public void TryMoveLocalMm_FacingZero_MovesAlongPlusZ()
    {
        var motor = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, radiusMm: 280);
        motor.TeleportMm(0, 0);
        motor.SetFacingDegrees(0f);
        Assert.That(motor.TryMoveLocalMm(0, 1000), Is.True);
        Assert.That(motor.PositionMm.X, Is.EqualTo(0));
        Assert.That(motor.PositionMm.Z, Is.EqualTo(1000));
    }

    /// <summary>朝向 +90° 时本地前向转到世界 +X。</summary>
    [Test]
    public void TryMoveLocalMm_FacingPlus90_MovesAlongPlusX()
    {
        var motor = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, radiusMm: 280);
        motor.TeleportMm(0, 0);
        motor.SetFacingDegrees(90f);
        Assert.That(motor.TryMoveLocalMm(0, 1000), Is.True);
        Assert.That(motor.PositionMm.X, Is.EqualTo(1000));
        Assert.That(motor.PositionMm.Z, Is.EqualTo(0));
    }

    /// <summary>零位移返回 false 且坐标不变。</summary>
    [Test]
    public void TryMoveWorldMm_ZeroDelta_ReturnsFalse()
    {
        var motor = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, radiusMm: 280);
        motor.TeleportMm(10, 20);
        Assert.That(motor.TryMoveWorldMm(0, 0), Is.False);
        Assert.That(motor.PositionMm.X, Is.EqualTo(10));
        Assert.That(motor.PositionMm.Z, Is.EqualTo(20));
    }

    /// <summary>RotateLocalToWorld 与 Motor 内部公式一致。</summary>
    [Test]
    public void RotateLocalToWorld_MatchesExpectedBasis()
    {
        int facing = MotionQuantization.DegreesToMilliDeg(90f);
        CharacterMotorSim.RotateLocalToWorld(facing, 0, 1000, out int wx, out int wz);
        Assert.That(wx, Is.EqualTo(1000));
        Assert.That(wz, Is.EqualTo(0));
    }

    /// <summary>软体抑制：Set 后 IsSoftBodySuppressed，Tick 递减，Clear 立即清零。</summary>
    [Test]
    public void SoftBodySuppress_TicksAndClears()
    {
        var motor = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, radiusMm: 280);
        Assert.That(motor.IsSoftBodySuppressed, Is.False);

        motor.SetSoftBodySuppressFrames(2);
        Assert.That(motor.IsSoftBodySuppressed, Is.True);
        Assert.That(motor.SoftBodySuppressFrames, Is.EqualTo(2));

        motor.TickSoftBodySuppress();
        Assert.That(motor.SoftBodySuppressFrames, Is.EqualTo(1));

        motor.ClearSoftBodySuppress();
        Assert.That(motor.IsSoftBodySuppressed, Is.False);
    }
}
