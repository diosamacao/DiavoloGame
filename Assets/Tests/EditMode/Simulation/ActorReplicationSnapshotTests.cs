using System.Reflection;
using NUnit.Framework;

/// <summary>复制快照往返、缺字段默认、排序与表现字段隔离。</summary>
public sealed class ActorReplicationSnapshotTests
{
    /// <summary>编解码后全部复制字段相等。</summary>
    [Test]
    public void Codec_RoundTrip_PreservesSnapshotAndTick()
    {
        var snapshot = new ActorReplicationSnapshot(
            new SimActorId(3),
            teamId: 2,
            ReplicationActorKind.Enemy,
            posXMm: 1500,
            posZMm: -400,
            posYMm: 0,
            facingMilliDeg: 90000,
            moveVxMm: 1200,
            moveVzMm: 0,
            locomotionPhase: 1,
            gait: 2,
            cardinal: 3,
            actionId: 17,
            graphNodeId: "Node_Light1",
            actionFrame: 8,
            freezeFrames: 2,
            selectedTargetId: new SimActorId(1),
            healthMilli: 85000,
            flagsPacked: 4,
            VitalityReplicationEdge.Hit,
            locomotionNormalizedMilli: 750);

        var hit = new ReplicatedHitEvent(
            12,
            new SimHitKey(12, new SimActorId(1), 5, 0, new SimActorId(3)),
            actionId: 17,
            hitXMm: 1100,
            hitYMm: 900,
            hitZMm: -200,
            dirXMm: 1000,
            dirZMm: 0);
        var tick = new AuthorityTick(
            12,
            new[] { snapshot },
            new[] { hit },
            new[] { new SimActorId(3) },
            System.Array.Empty<SimActorId>());

        AuthorityTick restored = ReplicationCodec.ReadAuthorityTick(
            ReplicationCodec.WriteAuthorityTick(tick));

        Assert.That(restored.Equals(tick), Is.True);
        Assert.That(restored.Actors[0].Equals(snapshot), Is.True);
        Assert.That(restored.Hits[0].ActionId, Is.EqualTo(17));
        Assert.That(restored.Hits[0].HitXMm, Is.EqualTo(1100));
        Assert.That(restored.Hits[0].HitYMm, Is.EqualTo(900));
        Assert.That(restored.Hits[0].DirXMm, Is.EqualTo(1000));
    }

    /// <summary>独立 Snapshot Codec 往返保留布局，并把无效 Actor Id 保持为 Invalid。</summary>
    [Test]
    public void SnapshotCodec_StandaloneRoundTrip_PreservesAllFields()
    {
        var snapshot = new ActorReplicationSnapshot(
            SimActorId.Invalid,
            teamId: 3,
            ReplicationActorKind.Player,
            posXMm: -100,
            posZMm: 200,
            posYMm: 300,
            facingMilliDeg: 45000,
            moveVxMm: -600,
            moveVzMm: 700,
            locomotionPhase: 2,
            gait: 1,
            cardinal: 5,
            actionId: 12,
            graphNodeId: "Graph/Node",
            actionFrame: 9,
            freezeFrames: 4,
            selectedTargetId: SimActorId.Invalid,
            healthMilli: 99000,
            flagsPacked: 17,
            VitalityReplicationEdge.Hit,
            locomotionNormalizedMilli: 1337);

        byte[] payload = ActorReplicationSnapshotCodec.Encode(in snapshot);
        // ActorId 线值改为 -1，确认提取后仍保持既有“任意非正值均为 Invalid”语义。
        payload[0] = 0xFF;
        payload[1] = 0xFF;
        payload[2] = 0xFF;
        payload[3] = 0xFF;
        ActorReplicationSnapshot restored = ActorReplicationSnapshotCodec.Decode(payload);

        Assert.That(restored.Equals(snapshot), Is.True);
        Assert.That(restored.ActorId, Is.EqualTo(SimActorId.Invalid));
        Assert.That(restored.SelectedTargetId, Is.EqualTo(SimActorId.Invalid));
    }

    /// <summary>default 快照字段安全：无动作、无目标、无边沿。</summary>
    [Test]
    public void DefaultSnapshot_IsSafe()
    {
        var snapshot = default(ActorReplicationSnapshot);
        Assert.That(snapshot.ActorId.IsValid, Is.False);
        Assert.That(snapshot.SelectedTargetId.IsValid, Is.False);
        Assert.That(snapshot.ActionId, Is.Zero);
        Assert.That(snapshot.GraphNodeId, Is.Null.Or.Empty);
        Assert.That(snapshot.VitalityEdge, Is.EqualTo(VitalityReplicationEdge.None));
    }

    /// <summary>Builder 从 Motor 与空闲 Action 快照取毫米位姿，idle 时 actionId 置 0。</summary>
    [Test]
    public void Builder_FromAuthority_CopiesMotorAndClearsIdleAction()
    {
        var motor = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, radiusMm: 280);
        motor.TeleportMm(2000, 100, -500);
        motor.SetFacingMilliDeg(45000);
        var idle = new ActionSimSnapshot(null, null, "ShouldIgnore", 9, 3, false, false, 4);

        ActorReplicationSnapshot snapshot = ReplicationSnapshotBuilder.FromAuthority(
            new SimActorId(1),
            teamId: 1,
            ReplicationActorKind.Player,
            motor,
            in idle,
            actionId: 99,
            SimActorId.Invalid,
            healthMilli: 100000,
            flagsPacked: 0,
            VitalityReplicationEdge.None);

        Assert.That(snapshot.PosXMm, Is.EqualTo(2000));
        Assert.That(snapshot.PosZMm, Is.EqualTo(-500));
        Assert.That(snapshot.PosYMm, Is.EqualTo(100));
        Assert.That(snapshot.FacingMilliDeg, Is.EqualTo(45000));
        Assert.That(snapshot.ActionId, Is.Zero);
        Assert.That(snapshot.GraphNodeId, Is.Empty);
        Assert.That(snapshot.ActionFrame, Is.Zero);
        Assert.That(snapshot.LocomotionNormalizedMilli, Is.Zero);
    }

    /// <summary>Tick 构造按 SimActorId 升序排列 actors。</summary>
    [Test]
    public void AuthorityTick_SortsActorsBySimActorId()
    {
        ActorReplicationSnapshot a = MinimalSnapshot(new SimActorId(4));
        ActorReplicationSnapshot b = MinimalSnapshot(new SimActorId(1));
        ActorReplicationSnapshot c = MinimalSnapshot(new SimActorId(2));
        var tick = new AuthorityTick(0, new[] { a, b, c });

        Assert.That(tick.Actors[0].ActorId.Value, Is.EqualTo(1));
        Assert.That(tick.Actors[1].ActorId.Value, Is.EqualTo(2));
        Assert.That(tick.Actors[2].ActorId.Value, Is.EqualTo(4));
    }

    /// <summary>快照类型不得包含本地表现字段。</summary>
    [Test]
    public void SnapshotType_HasNoPresentationFields()
    {
        foreach (PropertyInfo property in typeof(ActorReplicationSnapshot).GetProperties())
        {
            string name = property.Name;
            Assert.That(name.IndexOf("CameraLock", System.StringComparison.OrdinalIgnoreCase), Is.LessThan(0));
            Assert.That(name.IndexOf("Look", System.StringComparison.OrdinalIgnoreCase), Is.LessThan(0));
            Assert.That(name.IndexOf("Lean", System.StringComparison.OrdinalIgnoreCase), Is.LessThan(0));
        }
    }

    /// <summary>上行命令往返保留 InputFrame，不含世界坐标字段。</summary>
    [Test]
    public void ClientCommand_RoundTrip_PreservesInputOnly()
    {
        var input = new InputFrame(
            7,
            new SimActorId(1),
            10,
            -20,
            1ul,
            1ul,
            0ul,
            1800);
        var command = new ClientCommand(7, senderPlayerId: 1, in input);
        ClientCommand restored = ReplicationCodec.ReadClientCommand(
            ReplicationCodec.WriteClientCommand(in command));

        Assert.That(restored.Equals(command), Is.True);
        Assert.That(typeof(ClientCommand).GetProperty("HealthMilli"), Is.Null);
        Assert.That(typeof(ClientCommand).GetProperty("PosXMm"), Is.Null);
    }

    static ActorReplicationSnapshot MinimalSnapshot(SimActorId id) =>
        new(
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
            0,
            string.Empty,
            0,
            0,
            SimActorId.Invalid,
            0,
            0,
            VitalityReplicationEdge.None);
}
