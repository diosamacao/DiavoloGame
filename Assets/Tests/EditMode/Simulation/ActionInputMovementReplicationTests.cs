using System;
using NUnit.Framework;

/// <summary>输入动作方向时钟的复制，以及只重放电机、不恢复动作的纠偏契约。</summary>
public sealed class ActionInputMovementReplicationTests
{
    [TestCase(0, 0)]
    [TestCase(1, 12)]
    [TestCase(4, 60)]
    public void DirectionClock_RoundTripsAndCopies(int direction, int start)
    {
        int state = ActionInputMoveState.Pack(direction, start);
        var snapshot = Snapshot(100, state);
        var restored = ActorReplicationSnapshotCodec.Decode(ActorReplicationSnapshotCodec.Encode(in snapshot));
        Assert.That(restored, Is.EqualTo(snapshot));
        Assert.That(ActionInputMoveState.Cardinal(restored.ActionMovementState), Is.EqualTo(direction));
        Assert.That(ActionInputMoveState.StartFrame(restored.ActionMovementState), Is.EqualTo(start));
        Assert.That(snapshot.WithLocomotion(0, 0).ActionMovementState, Is.EqualTo(state));
        var motor = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, 280);
        Assert.That(snapshot.WithMotorPose(motor).ActionMovementState, Is.EqualTo(state));
        Assert.That(snapshot.WithAction(2, 100).ActionMovementState, Is.Zero);
        Assert.That(snapshot.WithAction(0, 0).ActionMovementState, Is.Zero);
    }

    [Test]
    public void InvalidOrRewoundClock_CannotCarryFutureDirection()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionInputMoveState.Pack(5, 0));
        Assert.That(Snapshot(5, ActionInputMoveState.Pack(1, 10)).ActionMovementState, Is.Zero);
        Assert.That(Snapshot(100, ActionInputMoveState.Pack(1, 10)).WithAction(1, 5).ActionMovementState, Is.Zero);
        Assert.That(Snapshot(100, 7).ActionMovementState, Is.Zero);
    }

    [Test]
    public void Codec_RejectsPreviousLayoutAndTrailingBytes()
    {
        var snapshot = Snapshot(100, 0);
        byte[] payload = ActorReplicationSnapshotCodec.Encode(in snapshot);
        Array.Resize(ref payload, payload.Length - 4);
        Assert.Throws<NetBufferException>(() => ActorReplicationSnapshotCodec.Decode(payload));
        payload = ActorReplicationSnapshotCodec.Encode(in snapshot);
        Array.Resize(ref payload, payload.Length + 1);
        Assert.Throws<NetBufferException>(() => ActorReplicationSnapshotCodec.Decode(payload));
    }

    [Test]
    public void Correction_ReplaysMovementRequests_IncludingFrozenZero_WithoutLocomotionRestore()
    {
        var motor = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, 280);
        var driver = new PredictedLocomotionDriver(motor, PredictedLocomotionConfig.Default);
        var replay = new Replay(motor);
        motor.TeleportMm(1000, 0, 0);
        for (int frame = 1; frame <= 4; frame++)
        {
            var input = new InputFrame(frame, new SimActorId(1), 0, 127, 0, 0, 0, 0);
            var command = new ActionInputMovementCommand(new SimVec2(0, frame == 3 ? 0 : 100),
                0, ActionBodyCollisionMode.SoftSeparationOnly, 20);
            driver.RecordAutonomous(in input, command);
        }
        var authority = Snapshot(10, 0);
        var result = driver.Reconcile(1, in authority, replay, snapThresholdMm: 0, authorityInputMovement: true);
        Assert.That(result.Snapped, Is.True);
        Assert.That(result.ReplayedInputs, Is.EqualTo(3));
        Assert.That(motor.PositionMm.X, Is.Zero);
        Assert.That(motor.PositionMm.Z, Is.EqualTo(200));
        Assert.That(replay.LocomotionCalls, Is.Zero);
        var repeated = driver.Reconcile(1, in authority, replay, snapThresholdMm: 0, authorityInputMovement: true);
        Assert.That(repeated.Snapped, Is.False);
        Assert.That(motor.PositionMm.Z, Is.EqualTo(200));
    }

    [Test]
    public void Hit_RejectsInputMovementReplay()
    {
        var motor = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, 280);
        var driver = new PredictedLocomotionDriver(motor, PredictedLocomotionConfig.Default);
        var replay = new Replay(motor);
        motor.TeleportMm(1000, 0, 0);
        for (int frame = 1; frame <= 2; frame++)
        {
            var input = new InputFrame(frame, new SimActorId(1), 0, 127, 0, 0, 0, 0);
            driver.RecordAutonomous(in input, new ActionInputMovementCommand(new SimVec2(0, 100),
                0, ActionBodyCollisionMode.SoftSeparationOnly, 20));
        }
        var authority = Snapshot(10, 0, VitalityReplicationEdge.Hit);
        var result = driver.Reconcile(1, in authority, replay, 0, authorityInputMovement: true);
        Assert.That(result.ReplayedInputs, Is.Zero);
        Assert.That(motor.PositionMm.Z, Is.Zero);
    }

    [Test]
    public void Correction_DoesNotJumpAcrossUnsupportedActionIntoLaterMovement()
    {
        var motor = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, 280);
        var driver = new PredictedLocomotionDriver(motor, PredictedLocomotionConfig.Default);
        var replay = new Replay(motor);
        motor.TeleportMm(1000, 0, 0);
        for (int frame = 1; frame <= 4; frame++)
        {
            var input = new InputFrame(frame, new SimActorId(1), 0, 127, 0, 0, 0, 0);
            driver.RecordAutonomous(in input, frame == 3 ? default : new ActionInputMovementCommand(new SimVec2(0, 100),
                0, ActionBodyCollisionMode.SoftSeparationOnly, 20));
        }
        var authority = Snapshot(10, 0);
        var result = driver.Reconcile(1, in authority, replay, 0, authorityInputMovement: true);
        Assert.That(result.ReplayedInputs, Is.EqualTo(1));
        Assert.That(motor.PositionMm.Z, Is.EqualTo(100));
        Assert.That(replay.LocomotionCalls, Is.Zero);
    }

    static ActorReplicationSnapshot Snapshot(int frame, int state, VitalityReplicationEdge edge = VitalityReplicationEdge.None) =>
        new ActorReplicationSnapshot(new SimActorId(1), 1, ReplicationActorKind.Player,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, frame, 0, SimActorId.Invalid, 1000, 0, edge,
            actionMovementState: state);

    sealed class Replay : IPredictedLocomotionReplay, IActionInputMovementReplay
    {
        readonly CharacterMotorSim motor;
        public int LocomotionCalls;
        public Replay(CharacterMotorSim motor) { this.motor = motor; }
        public void RestoreFromAuthority(in ActorReplicationSnapshot snapshot) { LocomotionCalls++; }
        public void ReplayTick(in InputFrame input) { LocomotionCalls++; }
        public void ReplayMovement(in ActionInputMovementCommand command) =>
            motor.TryMoveWorldMm(command.Delta.X, command.Delta.Z);
    }
}
