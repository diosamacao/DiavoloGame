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

    /// <summary>SavedState 原样保存 PhaseFrame 与整数计数，不经过归一化时间。</summary>
    [Test]
    public void Constructor_PreservesIntegerClock()
    {
        var state = new LocomotionSavedState(
            LocomotionPhase.Gait,
            LocomotionGait.Sprint,
            AnimationKey.Sprint,
            599,
            179,
            8,
            MoveCardinal.Forward,
            3,
            AnimationKey.Start,
            LocomotionGait.Run,
            MoveCardinal.Forward,
            AnimationKey.StopR,
            false,
            UnityEngine.Vector3.forward,
            UnityEngine.Vector3.back,
            UnityEngine.Vector3.forward,
            true,
            false,
            AnimationKey.Sprint,
            0f,
            FootSide.Right,
            true,
            false);

        Assert.That(state.PhaseFrame, Is.EqualTo(599));
        Assert.That(state.RunHoldFrames, Is.EqualTo(179));
        Assert.That(state.GaitInputGapFrames, Is.EqualTo(8));
    }

    /// <summary>V2 Stop/Pivot 快照恢复时必须重建根位移会话，而非只恢复动画帧。</summary>
    [Test]
    public void FromSnapshot_Stop_RebuildsRootMotionSession()
    {
        var snapshot = new ActorReplicationSnapshot(
            new SimActorId(1), 1, ReplicationActorKind.Player,
            0, 0, 0, 90000, 0, 0,
            (byte)AnimationKey.StopR, (byte)LocomotionGait.Run, (byte)MoveCardinal.Forward,
            0, 0, 0, 0, SimActorId.Invalid, 1000, 0, VitalityReplicationEdge.None,
            locomotionPhaseFrame: 12);

        LocomotionSavedState state = LocomotionSavedState.FromSnapshot(in snapshot);
        Assert.That(state.Phase, Is.EqualTo(LocomotionPhase.Stop));
        Assert.That(state.PhaseFrame, Is.EqualTo(12));
        Assert.That(state.RootMotionActive, Is.True);
        Assert.That(state.RootMotionKey, Is.EqualTo(AnimationKey.StopR));
        Assert.That(state.RootMotionBasisYaw, Is.EqualTo(90f).Within(0.001f));
        Assert.That(state.RootMotionBasisIsCurrentFacing, Is.True);
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
