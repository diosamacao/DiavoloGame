using NUnit.Framework;

/// <summary>SavedState：快照 AnimationKey/Gait 映射，以及闪避后恢复请求。</summary>
public sealed class LocomotionSavedStateTests
{
    /// <summary>Sprint 片对应内层 Gait，不是 Idle。</summary>
    [Test]
    public void PhaseFromAnimationKey_Sprint_IsGait()
    {
        Assert.That(
            LocomotionSavedState.PhaseFromAnimationKey(AnimationKey.Sprint),
            Is.EqualTo(LocomotionPhase.Gait));
        Assert.That(
            LocomotionSavedState.PhaseFromAnimationKey(AnimationKey.StopL),
            Is.EqualTo(LocomotionPhase.Stop));
    }

    /// <summary>出招期间快照 Gait 常为 Walk；Clip 为 Sprint 时以片为准。</summary>
    [Test]
    public void FromAuthority_SprintKey_OverridesWalkGait()
    {
        ActorReplicationSnapshot snapshot = new ActorReplicationSnapshot(
            new SimActorId(1),
            1,
            ReplicationActorKind.Player,
            0,
            0,
            0,
            0,
            0,
            0,
            (byte)AnimationKey.Sprint,
            (byte)LocomotionGait.Walk,
            (byte)MoveCardinal.Forward,
            0,
            string.Empty,
            0,
            0,
            SimActorId.Invalid,
            100000,
            0,
            VitalityReplicationEdge.None,
            250);

        LocomotionSavedState state = LocomotionSavedState.FromAuthority(in snapshot);

        Assert.That(state.Phase, Is.EqualTo(LocomotionPhase.Gait));
        Assert.That(state.Gait, Is.EqualTo(LocomotionGait.Sprint));
        Assert.That(state.AnimationKey, Is.EqualTo(AnimationKey.Sprint));
        Assert.That(state.NormalizedTime, Is.EqualTo(0.25f).Within(0.001f));
        Assert.That(state.GaitCardinal, Is.EqualTo(MoveCardinal.Forward));
    }

    /// <summary>Dodge 结束与 Host ActionState 一样跳过 Start 进 Sprint。</summary>
    [Test]
    public void AfterAction_Dodge_IsSprintAfterDodge()
    {
        LocomotionResumeRequest request = LocomotionResumeRequest.AfterAction(
            CombatActionType.Dodge,
            LocomotionGait.Walk);

        Assert.That(request.IsValid, Is.True);
        Assert.That(request.InitialGait, Is.EqualTo(LocomotionGait.Sprint));
        Assert.That(request.SkipStart, Is.True);
        Assert.That(request.RequireMoveIntent, Is.True);
    }
}
