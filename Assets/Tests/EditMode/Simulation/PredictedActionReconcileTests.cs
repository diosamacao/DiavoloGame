using NUnit.Framework;

/// <summary>出招 Ack：同招不取消、连招超前不取消、变体分叉取消、自然结束不重播延迟招。</summary>
public sealed class PredictedActionReconcileTests
{
    /// <summary>延迟 Tick 仍是同一招时只 Ack，不把本地招 Seek 回旧帧。</summary>
    [Test]
    public void Reconcile_SameAction_DoesNotCancelOrRewind()
    {
        var ack = new PredictedActionAckQueue();
        SimActorId id = new SimActorId(1);
        ack.Record(10, actionId: 7);
        ack.Record(11, actionId: 7);

        ActorReplicationSnapshot delayed = ActionSnapshot(id, actionId: 7, actionFrame: 0);
        PredictedActionReconcileResult result = ack.Reconcile(10, in delayed);

        Assert.That(result.Cancelled, Is.False);
        Assert.That(result.ActionId, Is.EqualTo(7));
        Assert.That(ack.PendingCount, Is.EqualTo(1));
    }

    /// <summary>该帧预测起手但权威未起手：取消本地招。</summary>
    [Test]
    public void Reconcile_AuthorityDidNotStart_CancelsPredictedAction()
    {
        var ack = new PredictedActionAckQueue();
        SimActorId id = new SimActorId(1);
        ack.Record(3, actionId: 4);

        ActorReplicationSnapshot authority = ActionSnapshot(id, actionId: 0, actionFrame: 0);
        PredictedActionReconcileResult result = ack.Reconcile(3, in authority);

        Assert.That(result.Cancelled, Is.True);
        Assert.That(result.ActionId, Is.Zero);
    }

    /// <summary>本机已连到下一招、权威还停在上一招：只 Ack，不取消本地下一段。</summary>
    [Test]
    public void Reconcile_AuthorityStillOnPreviousComboStep_DoesNotCancel()
    {
        var ack = new PredictedActionAckQueue();
        SimActorId id = new SimActorId(1);
        ack.Record(10, actionId: 1);
        ack.Record(11, actionId: 1);
        ack.Record(12, actionId: 2);

        ActorReplicationSnapshot delayed = ActionSnapshot(id, actionId: 1, actionFrame: 8);
        PredictedActionReconcileResult result = ack.Reconcile(12, in delayed);

        Assert.That(result.Cancelled, Is.False);
        Assert.That(result.ActionId, Is.EqualTo(2));
        Assert.That(ack.PendingCount, Is.Zero);
    }

    /// <summary>该帧预测招与权威招不同，且权威招从未在本机出现过：变体分叉，取消。</summary>
    [Test]
    public void Reconcile_DifferentVariantNeverRecorded_Cancels()
    {
        var ack = new PredictedActionAckQueue();
        SimActorId id = new SimActorId(1);
        ack.Record(10, actionId: 7);

        ActorReplicationSnapshot authority = ActionSnapshot(id, actionId: 8, actionFrame: 0);
        PredictedActionReconcileResult result = ack.Reconcile(10, in authority);

        Assert.That(result.Cancelled, Is.True);
        Assert.That(result.ActionId, Is.EqualTo(8));
    }

    /// <summary>本机招自然结束后，延迟快照仍带着同一招：不得再 Seek/派特效。</summary>
    [Test]
    public void ShouldPresentAuthorityAction_AfterLocalEnded_IgnoresStaleSnapshot()
    {
        Assert.That(
            PredictedActionAckQueue.ShouldPresentAuthorityAction(
                localActionActive: false,
                suppressStaleAuthorityAction: true,
                authorityHitOrDeath: false,
                authorityActionId: 4),
            Is.False);
    }

    /// <summary>受击边沿即使本机刚结束也要跟权威受击招。</summary>
    [Test]
    public void ShouldPresentAuthorityAction_Hit_FollowsAuthority()
    {
        Assert.That(
            PredictedActionAckQueue.ShouldPresentAuthorityAction(
                localActionActive: false,
                suppressStaleAuthorityAction: true,
                authorityHitOrDeath: true,
                authorityActionId: 20),
            Is.True);
    }

    /// <summary>本机从未起手、权威有招：跟快照（只读权威招）。</summary>
    [Test]
    public void ShouldPresentAuthorityAction_NeverPredicted_FollowsAuthority()
    {
        Assert.That(
            PredictedActionAckQueue.ShouldPresentAuthorityAction(
                localActionActive: false,
                suppressStaleAuthorityAction: false,
                authorityHitOrDeath: false,
                authorityActionId: 4),
            Is.True);
    }

    /// <summary>权威 Hit 边沿：取消预测攻击，表现改跟权威受击招。</summary>
    [Test]
    public void Reconcile_HitEdge_CancelsAttackAndFollowsHitAction()
    {
        var ack = new PredictedActionAckQueue();
        SimActorId id = new SimActorId(1);
        ack.Record(8, actionId: 4);
        ack.Record(9, actionId: 4);

        ActorReplicationSnapshot hit = new ActorReplicationSnapshot(
            id,
            1,
            ReplicationActorKind.Player,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            actionId: 20,
            string.Empty,
            actionFrame: 1,
            0,
            SimActorId.Invalid,
            80000,
            0,
            VitalityReplicationEdge.Hit);

        PredictedActionReconcileResult result = ack.Reconcile(8, in hit);

        Assert.That(result.Cancelled, Is.True);
        Assert.That(result.ActionId, Is.EqualTo(20));
        Assert.That(result.ActionFrame, Is.EqualTo(1));
    }

    static ActorReplicationSnapshot ActionSnapshot(SimActorId id, int actionId, int actionFrame) =>
        new ActorReplicationSnapshot(
            id,
            1,
            ReplicationActorKind.Player,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            actionId,
            string.Empty,
            actionFrame,
            0,
            SimActorId.Invalid,
            100000,
            0,
            VitalityReplicationEdge.None);
}
