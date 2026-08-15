using NUnit.Framework;

/// <summary>客机选片：松手不抢 Idle、冲刺用 Sprint、权威过渡相位优先。</summary>
public sealed class PredictedLocomotionVisualTests
{
    /// <summary>松手时权威仍是 Run，不得先切 Idle。</summary>
    [Test]
    public void ResolveSelfKey_ReleaseWhileAuthorityRun_KeepsRun()
    {
        ActorReplicationSnapshot snap = PhaseSnapshot(AnimationKey.Run);
        AnimationKey key = PredictedLocomotionVisual.ResolveSelfKey(
            in snap,
            hasMoveIntent: false,
            LocomotionGait.Run);
        Assert.That(key, Is.EqualTo(AnimationKey.Run));
    }

    /// <summary>权威已 Sprint 或本地升档到 Sprint 时必须播 Sprint，不能用 Run。</summary>
    [Test]
    public void ResolveSelfKey_Sprint_UsesSprintKey()
    {
        ActorReplicationSnapshot run = PhaseSnapshot(AnimationKey.Run);
        Assert.That(
            PredictedLocomotionVisual.ResolveSelfKey(in run, true, LocomotionGait.Sprint),
            Is.EqualTo(AnimationKey.Sprint));

        ActorReplicationSnapshot sprint = PhaseSnapshot(AnimationKey.Sprint);
        Assert.That(
            PredictedLocomotionVisual.ResolveSelfKey(in sprint, true, LocomotionGait.Run),
            Is.EqualTo(AnimationKey.Sprint));
    }

    /// <summary>权威 Idle 且有输入时播起步，避免 Idle 直接切 Run。</summary>
    [Test]
    public void ResolveSelfKey_IdleWithMove_UsesStart()
    {
        ActorReplicationSnapshot snap = PhaseSnapshot(AnimationKey.Idle);
        Assert.That(
            PredictedLocomotionVisual.ResolveSelfKey(in snap, true, LocomotionGait.Run),
            Is.EqualTo(AnimationKey.Start));
        Assert.That(
            PredictedLocomotionVisual.ResolveSelfKey(in snap, true, LocomotionGait.Walk),
            Is.EqualTo(AnimationKey.WalkStart));
    }

    /// <summary>权威急停相位始终赢过本地预测。</summary>
    [Test]
    public void ResolveSelfKey_AuthorityStop_Wins()
    {
        ActorReplicationSnapshot snap = PhaseSnapshot(AnimationKey.StopL);
        Assert.That(
            PredictedLocomotionVisual.ResolveSelfKey(in snap, false, LocomotionGait.Run),
            Is.EqualTo(AnimationKey.StopL));
    }

    /// <summary>进出 Stop/Start 硬切；Idle↔Run 不硬切。</summary>
    [Test]
    public void ShouldHardCut_OnlyTransitionPhases()
    {
        Assert.That(PredictedLocomotionVisual.ShouldHardCut(AnimationKey.Run, AnimationKey.StopL), Is.True);
        Assert.That(PredictedLocomotionVisual.ShouldHardCut(AnimationKey.Idle, AnimationKey.Run), Is.False);
        Assert.That(PredictedLocomotionVisual.ShouldHardCut(AnimationKey.Run, AnimationKey.Sprint), Is.False);
    }

    static ActorReplicationSnapshot PhaseSnapshot(AnimationKey phase) =>
        new ActorReplicationSnapshot(
            new SimActorId(1),
            1,
            ReplicationActorKind.Player,
            0,
            0,
            0,
            0,
            0,
            0,
            (byte)phase,
            0,
            0,
            0,
            string.Empty,
            0,
            0,
            SimActorId.Invalid,
            100000,
            0,
            VitalityReplicationEdge.None);
}
