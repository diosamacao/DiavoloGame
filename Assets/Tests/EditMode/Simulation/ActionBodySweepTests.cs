using System;
using NUnit.Framework;

/// <summary>覆盖快速跨体、静态组合、量化与接触逃离；不依赖 Unity Physics。</summary>
public sealed class ActionBodySweepTests
{
    [Test]
    public void EmptySpaceKeepsFullDisplacement_AndStaticOverlapRecovers()
    {
        Assert.That(Sweep(SimVec2.Zero, new SimVec2(123, 2226)), Is.EqualTo(new SimVec2(123, 2226)));
        var world = new SimStaticCollisionWorld(0, new[] { new SimStaticAabb(-100, 100, -100, 100) });
        SimVec2 end = ActionBodySweep.Resolve(world, SimVec2.Zero, new SimVec2(-100, 0), 280, 20,
            Array.Empty<SimBodyObstacle>(), out _, out _);
        Assert.That(end, Is.EqualTo(new SimVec2(-480, 0)));
    }

    static SimBodyObstacle Body(int id, int x, int z, int radius = 280) =>
        new(new SimActorId(id), new SimVec2(x, z), radius);

    static SimVec2 Sweep(SimVec2 from, SimVec2 delta, params SimBodyObstacle[] bodies) =>
        ActionBodySweep.Resolve(OpenFieldSimCollisionWorld.Instance, from, delta, 280, 20,
            bodies, out _, out _);

    [TestCase(2226)] [TestCase(1943)]
    public void CrossesWholeBody_StopsOnApproachSide(int forward)
    {
        SimVec2 end = Sweep(SimVec2.Zero, new SimVec2(0, forward), Body(1, 0, 1000));
        Assert.That(end.Z, Is.InRange(419, 420));
        Assert.That(end.X, Is.Zero);
    }

    [Test]
    public void NearestBodyWins_IndependentOfCollectionOrder()
    {
        var bodies = new[] { Body(2, 0, 1600), Body(3, 0, 1000), Body(1, 0, 1000) };
        SimVec2 a = ActionBodySweep.Resolve(OpenFieldSimCollisionWorld.Instance, SimVec2.Zero,
            new SimVec2(0, 3000), 280, 20, bodies, out bool blocked, out SimActorId id);
        Array.Reverse(bodies);
        SimVec2 b = Sweep(SimVec2.Zero, new SimVec2(0, 3000), bodies);
        Assert.That(blocked, Is.True);
        Assert.That(id.Value, Is.EqualTo(1));
        Assert.That(a, Is.EqualTo(b));
    }

    [TestCase(0, 0)] [TestCase(0, -100)] [TestCase(100, 0)]
    public void InitialOverlap_AllowsStationaryOutwardAndTangent(int x, int z)
    {
        Assert.That(Sweep(SimVec2.Zero, new SimVec2(x, z), Body(1, 0, 100)),
            Is.EqualTo(new SimVec2(x, z)));
    }

    [Test]
    public void InitialOverlap_BlocksDeeperMotion_ButCoincidentCanEscape()
    {
        Assert.That(Sweep(SimVec2.Zero, new SimVec2(0, 2000), Body(1, 0, 100)), Is.EqualTo(SimVec2.Zero));
        Assert.That(Sweep(SimVec2.Zero, new SimVec2(0, 100), Body(1, 0, 0)).Z, Is.EqualTo(100));
    }

    [Test]
    public void TangentDoesNotBlock()
    {
        Assert.That(Sweep(SimVec2.Zero, new SimVec2(0, 2226), Body(1, 580, 1000)).Z, Is.EqualTo(2226));
    }

    [TestCase(900, 2000, 620)] [TestCase(2000, 1000, 420)]
    public void EarliestWallOrBodyWins(int wallZ, int bodyZ, int expected)
    {
        var world = new SimStaticCollisionWorld(0, new[] { new SimStaticAabb(-2000, 2000, wallZ, wallZ + 100) });
        SimVec2 end = ActionBodySweep.Resolve(world, SimVec2.Zero, new SimVec2(0, 4000), 280, 20,
            new[] { Body(1, 0, bodyZ) }, out bool blocked, out _);
        Assert.That(end.Z, Is.InRange(expected - 1, expected));
        Assert.That(blocked, Is.True);
    }

    [Test]
    public void DiagonalWall_StopsBothAxes_AndCanLeaveSurface()
    {
        var world = new SimStaticCollisionWorld(0, new[] { new SimStaticAabb(-5000, 5000, 1000, 1500) });
        var motor = new CharacterMotorSim(world, 280);
        motor.TryMoveActionMm(new SimVec2(2000, 2000), 20, Array.Empty<SimBodyObstacle>(), out _, out _);
        Assert.That(motor.PositionMm, Is.EqualTo(new SimVec2(720, 720)));
        motor.TryMoveActionMm(new SimVec2(100, 0), 20, Array.Empty<SimBodyObstacle>(), out _, out _);
        Assert.That(motor.PositionMm, Is.EqualTo(new SimVec2(820, 720)));
        motor.TryMoveActionMm(new SimVec2(0, -100), 20, Array.Empty<SimBodyObstacle>(), out _, out _);
        Assert.That(motor.PositionMm.Z, Is.EqualTo(620));
    }

    [Test]
    public void NoDeferredDisplacement_AndSuppressionDoesNotDisableStop()
    {
        var motor = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, 280);
        motor.SetSoftBodySuppressFrames(10);
        var bodies = new[] { Body(1, 0, 1000) };
        for (int i = 0; i < 10; i++)
            motor.TryMoveActionMm(new SimVec2(0, 2226), 20, bodies, out _, out _);
        Assert.That(motor.PositionMm.Z, Is.InRange(419, 420));
        int before = motor.PositionMm.Z;
        motor.TryMoveActionMm(new SimVec2(0, 50), 20, Array.Empty<SimBodyObstacle>(), out _, out _);
        Assert.That(motor.PositionMm.Z, Is.EqualTo(before + 50));
    }

    [Test]
    public void TwoSequentialDashes_DoNotExchangeSides()
    {
        var a = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, 280);
        var b = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, 280);
        b.TeleportMm(0, 2000);
        a.TryMoveActionMm(new SimVec2(0, 3000), 20, new[] { Body(2, 0, 2000) }, out _, out _);
        b.TryMoveActionMm(new SimVec2(0, -3000), 20, new[] { Body(1, a.PositionMm.X, a.PositionMm.Z) }, out _, out _);
        Assert.That(b.PositionMm.Z - a.PositionMm.Z, Is.GreaterThanOrEqualTo(580));
    }

    [Test]
    public void LargeWorldCoordinates_DoNotOverflow()
    {
        SimVec2 end = Sweep(new SimVec2(1500000000, 1500000000), new SimVec2(0, 2226),
            Body(1, 1500000000, 1500001000));
        Assert.That(end.Z, Is.InRange(1500000419, 1500000420));
    }

    [Test]
    public void QuantizedDiagonalPaths_RemainOutsideBody()
    {
        var random = new Random(1309);
        for (int i = 0; i < 2000; i++)
        {
            int x = random.Next(-1000, 1001), z = random.Next(800, 2001);
            var body = Body(1, x, z);
            SimVec2 end = Sweep(SimVec2.Zero, new SimVec2(x * 2, z * 2), body);
            double dx = (double)end.X - x, dz = (double)end.Z - z;
            Assert.That(dx * dx + dz * dz, Is.GreaterThanOrEqualTo(580d * 580));
        }
    }
}
