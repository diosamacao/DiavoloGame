using NUnit.Framework;

/// <summary>验证静态 AABB 硬挡与滑墙，不依赖 Unity Physics。</summary>
public sealed class SimStaticCollisionWorldTests
{
    /// <summary>正面撞墙停在膨胀盒边缘。</summary>
    [Test]
    public void ResolveMove_HitsWall_StopsAtExpandedEdge()
    {
        // 墙：x[1000,2000] z[-500,500]；半径 280 → 左缘可到 1000-280=720
        var wall = new SimStaticAabb(1000, 2000, -500, 500);
        var world = new SimStaticCollisionWorld(0, new[] { wall });
        var motor = new CharacterMotorSim(world, radiusMm: 280);
        motor.TeleportMm(0, 0);

        Assert.That(motor.TryMoveWorldMm(2000, 0), Is.True);
        Assert.That(motor.PositionMm.X, Is.EqualTo(720));
        Assert.That(motor.PositionMm.Z, Is.EqualTo(0));
    }

    /// <summary>沿墙滑动：X 被挡时 Z 仍可前进。</summary>
    [Test]
    public void ResolveMove_SlidesAlongWall()
    {
        var wall = new SimStaticAabb(1000, 2000, -500, 500);
        var world = new SimStaticCollisionWorld(0, new[] { wall });
        var motor = new CharacterMotorSim(world, radiusMm: 280);
        motor.TeleportMm(720, 0);

        Assert.That(motor.TryMoveWorldMm(100, 1000), Is.True);
        Assert.That(motor.PositionMm.X, Is.EqualTo(720));
        Assert.That(motor.PositionMm.Z, Is.EqualTo(1000));
    }

    /// <summary>空障碍列表不阻挡，地面高度可读。</summary>
    [Test]
    public void EmptyWorld_AllowsMove_AndReportsGround()
    {
        var world = new SimStaticCollisionWorld(groundYMm: 500, aabbs: System.Array.Empty<SimStaticAabb>());
        Assert.That(world.GroundYMm, Is.EqualTo(500));
        Assert.That(world.ResolveMove(new SimVec2(0, 0), new SimVec2(10, 20), 100).X, Is.EqualTo(10));
    }
}
