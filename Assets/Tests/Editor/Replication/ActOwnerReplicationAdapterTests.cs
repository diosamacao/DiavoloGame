using System;
using NUnit.Framework;

/// <summary>验证 ACT Owner Adapter 的身份门禁、权威状态保存与重置行为。</summary>
public sealed class ActOwnerReplicationAdapterTests
{
    /// <summary>无本地 PlayerController 时仍应保存合法 Owner HP，但不得声明预测已就绪。</summary>
    [Test]
    public void ApplySnapshot_WithoutLocalActor_StoresHealthWithoutCreatingDriver()
    {
        var adapter = new ActOwnerReplicationAdapter(new ActContentRegistry());
        adapter.BeginSession(new SimActorId(5), new InputFrameBuffer());
        ActorReplicationSnapshot snapshot = CreateSnapshot(
            actorId: 5,
            kind: ReplicationActorKind.Player,
            healthMilli: 90000);

        adapter.ApplySnapshot(localPlayer: null, in snapshot, appliedHint: 0);

        Assert.That(adapter.SelfHealthMilli, Is.EqualTo(90000));
        Assert.That(adapter.CanPredict, Is.False);
        Assert.That(adapter.PendingCount, Is.Zero);
    }

    /// <summary>Snapshot ActorId 不等于 Session 分配实体时必须明确拒绝。</summary>
    [Test]
    public void ApplySnapshot_MismatchedOwnerEntity_Throws()
    {
        var adapter = new ActOwnerReplicationAdapter(new ActContentRegistry());
        adapter.BeginSession(new SimActorId(5), new InputFrameBuffer());
        ActorReplicationSnapshot snapshot = CreateSnapshot(
            actorId: 6,
            kind: ReplicationActorKind.Player,
            healthMilli: 90000);

        Assert.Throws<InvalidOperationException>(
            () => adapter.ApplySnapshot(null, in snapshot, appliedHint: 0));
    }

    /// <summary>Reset 必须清空身份、HP 与预测历史，避免下一个房间继承 Owner 状态。</summary>
    [Test]
    public void Reset_ClearsOwnerState()
    {
        var adapter = new ActOwnerReplicationAdapter(new ActContentRegistry());
        adapter.BeginSession(new SimActorId(5), new InputFrameBuffer());
        ActorReplicationSnapshot snapshot = CreateSnapshot(
            actorId: 5,
            kind: ReplicationActorKind.Player,
            healthMilli: 12345);
        adapter.ApplySnapshot(null, in snapshot, appliedHint: 0);

        adapter.Reset();

        Assert.That(adapter.SelfHealthMilli, Is.EqualTo(-1));
        Assert.That(adapter.CanPredict, Is.False);
        Assert.That(adapter.PendingCount, Is.Zero);
    }

    /// <summary>创建仅填 Owner 门禁测试所需字段的完整角色快照。</summary>
    static ActorReplicationSnapshot CreateSnapshot(
        int actorId,
        ReplicationActorKind kind,
        int healthMilli)
    {
        return new ActorReplicationSnapshot(
            new SimActorId(actorId),
            teamId: 1,
            kind: kind,
            posXMm: 100,
            posZMm: 200,
            posYMm: 0,
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
            healthMilli,
            flagsPacked: 0,
            VitalityReplicationEdge.None,
            locomotionNormalizedMilli: 0);
    }
}
