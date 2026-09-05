using NUnit.Framework;

/// <summary>他人 Proxy 相位判断：过渡硬切、快照 AnimationKey 解码。</summary>
public sealed class ReplicationPresentationAlignTests
{
    /// <summary>进出 Stop/Start 硬切；Idle↔Run 不硬切。</summary>
    [Test]
    public void ShouldHardCut_OnlyTransitionPhases()
    {
        Assert.That(ReplicationPresentationAlign.ShouldHardCut(AnimationKey.Run, AnimationKey.StopL), Is.True);
        Assert.That(ReplicationPresentationAlign.ShouldHardCut(AnimationKey.Idle, AnimationKey.Run), Is.False);
        Assert.That(ReplicationPresentationAlign.ShouldHardCut(AnimationKey.Run, AnimationKey.Sprint), Is.False);
    }

    /// <summary>快照字节能还原为 AnimationKey。</summary>
    [Test]
    public void TryReadPhase_Sprint_Succeeds()
    {
        ActorReplicationSnapshot snap = PhaseSnapshot(AnimationKey.Sprint);
        Assert.That(ReplicationPresentationAlign.TryReadPhase(in snap, out AnimationKey key), Is.True);
        Assert.That(key, Is.EqualTo(AnimationKey.Sprint));
        Assert.That(ReplicationPresentationAlign.IsTransitionPhase(key), Is.False);
        Assert.That(ReplicationPresentationAlign.IsMovingLocomotionPhase(key), Is.True);
    }

    /// <summary>走跑算位移相位，Idle / Additive 受击不算，供复制 Urgent。</summary>
    [Test]
    public void IsMovingLocomotionPhase_ExcludesIdleAndHitShake()
    {
        Assert.That(ReplicationPresentationAlign.IsMovingLocomotionPhase(AnimationKey.Walk), Is.True);
        Assert.That(ReplicationPresentationAlign.IsMovingLocomotionPhase(AnimationKey.WalkLeft), Is.True);
        Assert.That(ReplicationPresentationAlign.IsMovingLocomotionPhase(AnimationKey.Idle), Is.False);
        Assert.That(ReplicationPresentationAlign.IsMovingLocomotionPhase(AnimationKey.HitShake), Is.False);
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
            0,
            0,
            0,
            SimActorId.Invalid,
            100000,
            0,
            VitalityReplicationEdge.None);
}
