using System;
using NUnit.Framework;

/// <summary>验证软弹开质量比与不可推动体行为。</summary>
public sealed class SoftBodySeparationTests
{
    static readonly int[] EqualMass = { 100, 100 };

    /// <summary>等质量重叠圆盘应沿连线分开。</summary>
    [Test]
    public void Resolve_SoftPushesOverlappingDisksApart()
    {
        var positions = new[]
        {
            new SimVec2(0, 0),
            new SimVec2(100, 0),
        };
        var radii = new[] { 280, 280 };

        SoftBodySeparation.Resolve(
            positions,
            radii,
            EqualMass,
            count: 2,
            factorMilli: 500,
            iterations: 1);

        int dist = SoftBodySeparation.LengthMm(
            positions[1].X - positions[0].X,
            positions[1].Z - positions[0].Z);
        Assert.That(dist, Is.GreaterThan(100));
        Assert.That(dist, Is.LessThan(560));
    }

    /// <summary>不可推动体位置不变，轻物体承担全部推力。</summary>
    [Test]
    public void Resolve_ImmovableActsLikeWall()
    {
        var positions = new[]
        {
            new SimVec2(0, 0),
            new SimVec2(100, 0),
        };
        var radii = new[] { 280, 280 };
        var masses = new[] { 100, SoftBodySeparation.ImmovableMass };

        SoftBodySeparation.Resolve(positions, radii, masses, 2, factorMilli: 1000, iterations: 3);

        Assert.That(positions[1].X, Is.EqualTo(100));
        Assert.That(positions[1].Z, Is.EqualTo(0));
        Assert.That(positions[0].X, Is.LessThan(0));
    }

    /// <summary>质量更大的一侧位移更小。</summary>
    [Test]
    public void Resolve_HeavierDiskMovesLess()
    {
        var positions = new[]
        {
            new SimVec2(0, 0),
            new SimVec2(100, 0),
        };
        var radii = new[] { 280, 280 };
        var masses = new[] { 100, 900 };

        SoftBodySeparation.Resolve(positions, radii, masses, 2, factorMilli: 1000, iterations: 1);

        int lightMove = Math.Abs(positions[0].X - 0);
        int heavyMove = Math.Abs(positions[1].X - 100);
        Assert.That(lightMove, Is.GreaterThan(heavyMove));
    }

    /// <summary>同输入两次 Resolve 结果必须一致。</summary>
    [Test]
    public void Resolve_IsDeterministic()
    {
        var a = new[] { new SimVec2(10, 20), new SimVec2(50, 40) };
        var b = new[] { new SimVec2(10, 20), new SimVec2(50, 40) };
        var radii = new[] { 100, 100 };

        SoftBodySeparation.Resolve(a, radii, EqualMass, 2, 500, 3);
        SoftBodySeparation.Resolve(b, radii, EqualMass, 2, 500, 3);

        Assert.That(a[0].X, Is.EqualTo(b[0].X));
        Assert.That(a[0].Z, Is.EqualTo(b[0].Z));
        Assert.That(a[1].X, Is.EqualTo(b[1].X));
        Assert.That(a[1].Z, Is.EqualTo(b[1].Z));
    }

    /// <summary>World 帧末应对两个 SoftBody 参与者执行弹开。</summary>
    [Test]
    public void SimulationWorld_Step_AppliesSoftBodySeparation()
    {
        var world = new SimulationWorld(new SimulationConfig(60, 8, true, 1000, 4));

        var a = new SoftBodyActor(new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, 200));
        var b = new SoftBodyActor(new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, 200));
        a.MotorSim.TeleportMm(0, 0);
        b.MotorSim.TeleportMm(50, 0);
        world.Register(a);
        world.Register(b);

        world.Step();

        int dist = SoftBodySeparation.LengthMm(
            b.MotorSim.PositionMm.X - a.MotorSim.PositionMm.X,
            b.MotorSim.PositionMm.Z - a.MotorSim.PositionMm.Z);
        Assert.That(dist, Is.GreaterThan(50));
        Assert.That(a.SoftApplyCount, Is.EqualTo(1));
        Assert.That(b.SoftApplyCount, Is.EqualTo(1));
    }

    /// <summary>抑制参与者不进软体分离，重叠距离保持。</summary>
    [Test]
    public void SimulationWorld_Step_SkipsSuppressedParticipant()
    {
        var world = new SimulationWorld(new SimulationConfig(60, 8, true, 1000, 4));

        var a = new SoftBodyActor(
            new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, 200),
            participates: false);
        var b = new SoftBodyActor(new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, 200));
        a.MotorSim.TeleportMm(0, 0);
        b.MotorSim.TeleportMm(50, 0);
        world.Register(a);
        world.Register(b);

        world.Step();

        Assert.That(a.MotorSim.PositionMm.X, Is.EqualTo(0));
        Assert.That(b.MotorSim.PositionMm.X, Is.EqualTo(50));
        Assert.That(a.SoftApplyCount, Is.EqualTo(0));
        Assert.That(b.SoftApplyCount, Is.EqualTo(0));
    }

    /// <summary>测试用软弹开参与者。</summary>
    sealed class SoftBodyActor : ISimulationActor, ISimSoftBodyParticipant
    {
        readonly bool _participates;

        public SoftBodyActor(CharacterMotorSim motorSim, bool participates = true)
        {
            MotorSim = motorSim;
            _participates = participates;
        }

        public CharacterMotorSim MotorSim { get; }
        public bool ParticipatesInSoftBodySeparation => _participates;
        public int SoftApplyCount { get; private set; }

        public void OnSoftBodySeparationApplied() => SoftApplyCount++;

        public void Step(long frameIndex, float fixedDeltaSeconds, in InputFrame inputFrame) { }
    }
}
