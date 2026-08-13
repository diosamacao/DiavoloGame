using System.Collections.Generic;
using NUnit.Framework;

/// <summary>验证唯一目标自动选择、保持与左右切换的确定性规则。</summary>
public sealed class DeterministicTargetResolverTests
{
    /// <summary>自动 Acquire 的等距平局只由稳定 ActorId 决定。</summary>
    [Test]
    public void Resolve_AcquiresNearestWithStableActorIdTieBreak()
    {
        var request = CreateRequest(SimActorId.Invalid, TargetSwitchDirection.None);
        var candidates = new List<SimTargetCandidate>
        {
            Candidate(3, 1000, 0),
            Candidate(2, -1000, 0),
        };

        SimActorId first = DeterministicTargetResolver.Resolve(in request, candidates);
        candidates.Reverse();
        SimActorId reversed = DeterministicTargetResolver.Resolve(in request, candidates);

        Assert.That(first, Is.EqualTo(new SimActorId(2)));
        Assert.That(reversed, Is.EqualTo(first));
    }

    /// <summary>有效当前目标必须保持，不能被后来出现的更近目标抢走。</summary>
    [Test]
    public void Resolve_RetainsCurrentWhenCloserTargetAppears()
    {
        var request = CreateRequest(new SimActorId(3), TargetSwitchDirection.None);
        var candidates = new[]
        {
            Candidate(2, 500, 0),
            Candidate(3, 1500, 0),
        };

        Assert.That(
            DeterministicTargetResolver.Resolve(in request, candidates),
            Is.EqualTo(new SimActorId(3)));
    }

    /// <summary>左右切换按 MoveReferenceYaw 相对方位选择下一目标。</summary>
    [Test]
    public void Resolve_SwitchesAroundReferenceYawInRequestedDirection()
    {
        var current = new SimActorId(2);
        var candidates = new[]
        {
            Candidate(2, 0, 1000),
            Candidate(3, 1000, 1000),
            Candidate(4, -1000, 1000),
        };
        var right = CreateRequest(current, TargetSwitchDirection.Right);
        var left = CreateRequest(current, TargetSwitchDirection.Left);

        Assert.That(
            DeterministicTargetResolver.Resolve(in right, candidates),
            Is.EqualTo(new SimActorId(3)));
        Assert.That(
            DeterministicTargetResolver.Resolve(in left, candidates),
            Is.EqualTo(new SimActorId(4)));
    }

    /// <summary>当前目标失效帧先自动 Acquire，不在同帧重复执行切换。</summary>
    [Test]
    public void Resolve_InvalidCurrentAutoAcquiresAndDoesNotDoubleSwitch()
    {
        var request = CreateRequest(new SimActorId(9), TargetSwitchDirection.Right);
        var candidates = new[]
        {
            Candidate(2, 0, 1000),
            Candidate(3, 1000, 1000),
        };

        Assert.That(
            DeterministicTargetResolver.Resolve(in request, candidates),
            Is.EqualTo(new SimActorId(2)));
    }

    static SimTargetResolveRequest CreateRequest(
        SimActorId current,
        TargetSwitchDirection switchDirection) =>
        new(
            new SimActorId(1),
            requesterTeamId: 0,
            originXMm: 0,
            originZMm: 0,
            moveReferenceYawQuantized: InputQuantizer.QuantizeYaw(0f),
            acquireRangeMm: 5000,
            retainRangeMm: 6000,
            current,
            switchDirection);

    static SimTargetCandidate Candidate(int id, int xMm, int zMm) =>
        new(new SimActorId(id), teamId: 1, xMm, zMm, isAlive: true);
}
