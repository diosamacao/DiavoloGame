using NUnit.Framework;

/// <summary>出招预测：同招不取消、权威未起手取消、Hit 边沿改跟受击招。</summary>
public sealed class PredictedActionReconcileTests
{
    /// <summary>延迟 Tick 仍是同一招时只 Ack，不把本地招 Seek 回旧帧。</summary>
    [Test]
    public void Reconcile_SameAction_DoesNotCancelOrRewind()
    {
        var driver = new PredictedActionDriver();
        SimActorId id = new SimActorId(1);
        driver.Predict(10, actionId: 7, actionFrame: 4);
        driver.Predict(11, actionId: 7, actionFrame: 5);

        ActorReplicationSnapshot delayed = ActionSnapshot(id, actionId: 7, actionFrame: 0);
        PredictedActionReconcileResult result = driver.Reconcile(10, in delayed);

        Assert.That(result.Cancelled, Is.False);
        Assert.That(driver.ActionId, Is.EqualTo(7));
        Assert.That(driver.ActionFrame, Is.EqualTo(5));
    }

    /// <summary>该帧预测起手但权威未起手：取消本地招。</summary>
    [Test]
    public void Reconcile_AuthorityDidNotStart_CancelsPredictedAction()
    {
        var driver = new PredictedActionDriver();
        SimActorId id = new SimActorId(1);
        driver.Predict(3, actionId: 4, actionFrame: 0);

        ActorReplicationSnapshot authority = ActionSnapshot(id, actionId: 0, actionFrame: 0);
        PredictedActionReconcileResult result = driver.Reconcile(3, in authority);

        Assert.That(result.Cancelled, Is.True);
        Assert.That(driver.ActionId, Is.Zero);
        Assert.That(driver.IsActive, Is.False);
    }

    /// <summary>权威 Hit 边沿：取消预测攻击并改跟权威受击招。</summary>
    [Test]
    public void Reconcile_HitEdge_CancelsAttackAndFollowsHitAction()
    {
        var driver = new PredictedActionDriver();
        SimActorId id = new SimActorId(1);
        driver.Predict(8, actionId: 4, actionFrame: 6);
        driver.TickUnconfirmed(9);

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

        PredictedActionReconcileResult result = driver.Reconcile(8, in hit);

        Assert.That(result.Cancelled, Is.True);
        Assert.That(driver.ActionId, Is.EqualTo(20));
        Assert.That(driver.ActionFrame, Is.EqualTo(1));
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
