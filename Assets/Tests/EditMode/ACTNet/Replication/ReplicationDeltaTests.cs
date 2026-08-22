using System;
using NUnit.Framework;

/// <summary>W11：未变跳过、节拍、预算、baseline 恢复。不引用 ACT。</summary>
public sealed class ReplicationDeltaTests
{
    /// <summary>相同载荷的第二帧不得再发 Update。</summary>
    [Test]
    public void UnchangedPayload_SkipsUpdate()
    {
        var server = new ReplicationServer();
        ReplicationEntityState state = State(1, 10, 4);
        server.BuildFrame(new NetTick(1), new[] { state }, Array.Empty<byte>());

        ReplicationFrame second = server.BuildFrame(
            new NetTick(2),
            new[] { State(1, 10, 4) },
            Array.Empty<byte>());

        Assert.That(second.Updates, Is.Empty);
        Assert.That(second.Spawns, Is.Empty);
        Assert.That(second.Despawns, Is.Empty);
        Assert.That(server.LastSkippedUnchanged, Is.EqualTo(1));
    }

    /// <summary>非优先实体只在间隔 Tick 刷新；Owner 变脏立刻发出。</summary>
    [Test]
    public void Cadence_OwnerSendsOffInterval_OtherWaits()
    {
        var server = new ReplicationServer();
        var options = new ReplicationBuildOptions(
            skipUnchanged: true,
            maxUpdateBytes: 0,
            snapshotIntervalTicks: 2,
            preferredEntity: new NetEntityId(1),
            forceFull: false);
        server.BuildFrame(
            new NetTick(1),
            new[] { State(1, 10, 1), State(2, 20, 1) },
            Array.Empty<byte>(),
            options);

        ReplicationFrame odd = server.BuildFrame(
            new NetTick(3),
            new[] { State(1, 10, 2), State(2, 20, 2) },
            Array.Empty<byte>(),
            options);

        Assert.That(odd.Updates, Has.Length.EqualTo(1));
        Assert.That(odd.Updates[0].EntityId.Value, Is.EqualTo(1));

        ReplicationFrame even = server.BuildFrame(
            new NetTick(4),
            new[] { State(1, 10, 2), State(2, 20, 2) },
            Array.Empty<byte>(),
            options);
        Assert.That(even.Updates, Has.Length.EqualTo(1));
        Assert.That(even.Updates[0].EntityId.Value, Is.EqualTo(2));
    }

    /// <summary>Urgent 实体在奇数 Tick 也必须发出，不能等节拍。</summary>
    [Test]
    public void Urgent_SendsOffInterval()
    {
        var server = new ReplicationServer();
        var options = new ReplicationBuildOptions(
            skipUnchanged: true,
            maxUpdateBytes: 0,
            snapshotIntervalTicks: 2,
            preferredEntity: new NetEntityId(1),
            forceFull: false);
        server.BuildFrame(
            new NetTick(1),
            new[] { State(1, 10, 1), State(2, 20, 1) },
            Array.Empty<byte>(),
            options);

        ReplicationFrame odd = server.BuildFrame(
            new NetTick(3),
            new[] { State(1, 10, 1), State(2, 20, 2, urgent: true) },
            Array.Empty<byte>(),
            options);

        Assert.That(odd.Updates, Has.Length.EqualTo(1));
        Assert.That(odd.Updates[0].EntityId.Value, Is.EqualTo(2));
    }

    /// <summary>预算先装 Owner，装不下的敌人保持脏以便下帧重试。</summary>
    [Test]
    public void Budget_PrefersOwner_ThenRetriesRemainder()
    {
        var server = new ReplicationServer();
        var options = new ReplicationBuildOptions(
            skipUnchanged: true,
            maxUpdateBytes: 16,
            snapshotIntervalTicks: 1,
            preferredEntity: new NetEntityId(1),
            forceFull: false);
        server.BuildFrame(
            new NetTick(1),
            new[] { State(1, 10, 1), State(2, 20, 1) },
            Array.Empty<byte>(),
            options);

        ReplicationFrame first = server.BuildFrame(
            new NetTick(2),
            new[] { State(1, 10, 2), State(2, 20, 2) },
            Array.Empty<byte>(),
            options);
        Assert.That(first.Updates, Has.Length.EqualTo(1));
        Assert.That(first.Updates[0].EntityId.Value, Is.EqualTo(1));

        ReplicationFrame second = server.BuildFrame(
            new NetTick(3),
            new[] { State(1, 10, 2), State(2, 20, 2) },
            Array.Empty<byte>(),
            options);
        Assert.That(second.Updates, Has.Length.EqualTo(1));
        Assert.That(second.Updates[0].EntityId.Value, Is.EqualTo(2));
    }

    /// <summary>ResetBaseline 后下一帧把仍在场的实体重新 Spawn。</summary>
    [Test]
    public void ResetBaseline_RespawnsLivingEntities()
    {
        var server = new ReplicationServer();
        ReplicationClient client = CreateClient();
        client.ApplyFrame(server.BuildFrame(
            new NetTick(1),
            new[] { State(1, 10, 1) },
            Array.Empty<byte>()));

        server.ResetBaseline();
        client.ResetRegistry();
        ReplicationFrame recovered = server.BuildFrame(
            new NetTick(2),
            new[] { State(1, 10, 1) },
            Array.Empty<byte>(),
            ReplicationBuildOptions.Compatible.WithForceFull(true));

        Assert.That(recovered.Spawns, Has.Length.EqualTo(1));
        Assert.That(client.ApplyFrame(recovered).Status, Is.EqualTo(ReplicationClientApplyStatus.Applied));
        Assert.That(client.Registry.Count, Is.EqualTo(1));
    }

    /// <summary>兴趣半径外的实体不进入 relevant set，因而会被 Despawn。</summary>
    [Test]
    public void Interest_FarEntity_IsNotRelevant()
    {
        Assert.That(
            ReplicationInterest.IsRelevant(false, false, 50000, 0, ReplicationInterest.DefaultRadiusMm),
            Is.False);
        Assert.That(
            ReplicationInterest.IsRelevant(true, false, 50000, 0, ReplicationInterest.DefaultRadiusMm),
            Is.True);
        Assert.That(
            ReplicationInterest.IsRelevant(false, true, 50000, 0, ReplicationInterest.DefaultRadiusMm),
            Is.True);
    }

    static ReplicationClient CreateClient()
    {
        var schemas = new ReplicationSchemaRegistry();
        schemas.Register(new OneByteSchema());
        return new ReplicationClient(schemas);
    }

    static ReplicationEntityState State(int entityId, int archetypeId, byte value, bool urgent = false) =>
        new ReplicationEntityState(
            new NetEntityId(entityId),
            new NetArchetypeId(archetypeId),
            1,
            new[] { value },
            urgent);

    sealed class OneByteSchema : IReplicationSchema
    {
        public ushort SchemaId => 1;

        public byte[] Encode(object state) => new[] { Convert.ToByte(state) };

        public object Decode(byte[] payload)
        {
            if (payload == null || payload.Length != 1)
                throw new FormatException("payload 必须恰好包含一个字节。");
            return payload[0];
        }
    }
}
