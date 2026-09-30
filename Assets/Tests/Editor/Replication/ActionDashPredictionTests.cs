using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>只读体积提供者的预测一致性与生命周期；陈旧位置差异不伪装为双端完全一致。</summary>
public sealed class ActionDashPredictionTests
{
    [Test]
    public void ListenSharedRoster_FilterSelectsOneWorldBeforeDuplicateCheck()
    {
        var authority = new BodyTarget(2, 1000);
        var proxy = new BodyTarget(2, 1200);
        var targets = new List<IHurtboxTarget> { authority, proxy };
        var authorityQuery = new CharacterBodyObstacleQuery(() => targets, body => ReferenceEquals(body, authority));
        var proxyQuery = new CharacterBodyObstacleQuery(() => targets, body => ReferenceEquals(body, proxy));
        var bodies = new List<SimBodyObstacle>();
        authorityQuery.Collect(new SimActorId(1), bodies);
        Assert.That(bodies.Count, Is.EqualTo(1));
        Assert.That(bodies[0].PositionMm.Z, Is.EqualTo(1000));
        proxyQuery.Collect(new SimActorId(1), bodies);
        Assert.That(bodies[0].PositionMm.Z, Is.EqualTo(1200));
    }

    [Test]
    public void Query_ExcludesSelfDisabledAndRemovedBodies_WithoutCaching()
    {
        var self = new BodyTarget(1, 0);
        var other = new BodyTarget(2, 1000);
        var targets = new List<IHurtboxTarget> { other, self };
        var query = new CharacterBodyObstacleQuery(() => targets);
        var bodies = new List<SimBodyObstacle>();
        query.Collect(new SimActorId(1), bodies);
        Assert.That(bodies.Count, Is.EqualTo(1));
        other.Enabled = false;
        query.Collect(new SimActorId(1), bodies);
        Assert.That(bodies, Is.Empty);
        other.Enabled = true;
        targets.Remove(other);
        query.Collect(new SimActorId(1), bodies);
        Assert.That(bodies, Is.Empty);
        targets.Add(other);
        query.Collect(new SimActorId(2), bodies);
        Assert.That(bodies[0].ActorId.Value, Is.EqualTo(1));
    }

    [Test]
    public void AuthorityAndPrediction_WithSameBodiesMatch_AndNeverWriteTarget()
    {
        var target = new BodyTarget(2, 1000);
        var targets = new List<IHurtboxTarget> { target };
        var query = new CharacterBodyObstacleQuery(() => targets);
        var bodies = new List<SimBodyObstacle>();
        query.Collect(new SimActorId(1), bodies);
        var a = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, 280);
        var b = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, 280);
        a.TryMoveActionMm(new SimVec2(0, 2226), 20, bodies, out _, out _);
        b.TryMoveActionMm(new SimVec2(0, 2226), 20, bodies, out _, out _);
        Assert.That(a.PositionMm, Is.EqualTo(b.PositionMm));
        Assert.That(target.Position.Z, Is.EqualTo(1000));
    }

    [Test]
    public void DuplicateIds_AreRejected()
    {
        var query = new CharacterBodyObstacleQuery(() => new IHurtboxTarget[] { new BodyTarget(2, 1000), new BodyTarget(2, 1500) });
        Assert.Throws<System.InvalidOperationException>(() => query.Collect(new SimActorId(1), new List<SimBodyObstacle>()));
    }

    [Test]
    public void Query_FreezesOneSubmission_ThenReadsLatestProxyPosition()
    {
        var target = new BodyTarget(2, 1000);
        var query = new CharacterBodyObstacleQuery(() => new IHurtboxTarget[] { target });
        var bodies = new List<SimBodyObstacle>();
        query.Collect(new SimActorId(1), bodies);
        target.Position = new SimVec2(0, 1500);
        Assert.That(bodies[0].PositionMm.Z, Is.EqualTo(1000));
        query.Collect(new SimActorId(1), bodies);
        Assert.That(bodies[0].PositionMm.Z, Is.EqualTo(1500));
    }

    sealed class BodyTarget : IHurtboxTarget, ISimBodyObstacleSource
    {
        public bool Enabled = true;
        public SimVec2 Position;
        public BodyTarget(int id, int z) { SimulationId = new SimActorId(id); Position = new SimVec2(0, z); }
        public SimActorId SimulationId { get; }
        public Transform TargetTransform => null;
        public HitboxOrientedBox GetLogicalHurtbox() => default;
        public SimCombatPose GetLogicalCombatPose() => default;
        public void OnHit(in ActionHitContext context) { }
        public bool TryGetBodyObstacle(out SimBodyObstacle obstacle)
        { obstacle = new SimBodyObstacle(SimulationId, Position, 280); return Enabled; }
    }
}
