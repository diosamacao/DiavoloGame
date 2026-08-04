using NUnit.Framework;

/// <summary>验证 MotorSim 整数重力与着地，不经 CharacterController。</summary>
public sealed class CharacterMotorSimVerticalTests
{
    /// <summary>出生贴地时 IsGrounded，Tick 后仍贴地。</summary>
    [Test]
    public void TickVertical_Grounded_StaysOnGround()
    {
        var motor = new CharacterMotorSim(
            OpenFieldSimCollisionWorld.Instance,
            radiusMm: 280,
            gravityMmPerSec2: -20000,
            groundedGravityMmPerSec2: -2000);
        motor.TeleportMm(0, 0, 0);

        Assert.That(motor.IsGrounded, Is.True);
        for (int i = 0; i < 10; i++)
            motor.TickVertical();

        Assert.That(motor.IsGrounded, Is.True);
        Assert.That(motor.YMm, Is.EqualTo(0));
    }

    /// <summary>空中下落最终贴地。</summary>
    [Test]
    public void TickVertical_FromAir_LandsOnGround()
    {
        var motor = new CharacterMotorSim(
            OpenFieldSimCollisionWorld.Instance,
            radiusMm: 280,
            gravityMmPerSec2: -20000,
            groundedGravityMmPerSec2: -2000);
        motor.TeleportMm(0, 2000, 0);
        Assert.That(motor.IsGrounded, Is.False);

        for (int i = 0; i < 600; i++)
            motor.TickVertical();

        Assert.That(motor.IsGrounded, Is.True);
        Assert.That(motor.YMm, Is.EqualTo(0));
    }

    /// <summary>自定义地面高度被尊重。</summary>
    [Test]
    public void TickVertical_CustomGroundY()
    {
        var world = new SimStaticCollisionWorld(groundYMm: 1000, aabbs: System.Array.Empty<SimStaticAabb>());
        var motor = new CharacterMotorSim(world, radiusMm: 280);
        motor.TeleportMm(0, 1500, 0);

        for (int i = 0; i < 600; i++)
            motor.TickVertical();

        Assert.That(motor.YMm, Is.EqualTo(1000));
        Assert.That(motor.IsGrounded, Is.True);
    }
}
