using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>验证 Server full set 差分与 Client 显式生命周期、Sequence 和 Archetype 行为。</summary>
public sealed class ReplicationRuntimeTests
{
    /// <summary>FakeEntity 必须由 Server 依次产生 Spawn、Update、Despawn 并被 Client 接受。</summary>
    [Test]
    public void FakeEntity_ServerClient_CompletesSpawnUpdateDespawn()
    {
        ReplicationClient client = CreateClient();
        var server = new ReplicationServer();
        ReplicationEntityState entity = State(2, 10, 1);

        ReplicationFrame spawn = server.BuildFrame(
            new NetTick(1),
            new[] { entity },
            Array.Empty<byte>());
        ReplicationFrame update = server.BuildFrame(
            new NetTick(2),
            new[] { State(2, 10, 2) },
            Array.Empty<byte>());
        ReplicationFrame despawn = server.BuildFrame(
            new NetTick(3),
            Array.Empty<ReplicationEntityState>(),
            Array.Empty<byte>());

        Assert.That(client.ApplyFrame(spawn).Status, Is.EqualTo(ReplicationClientApplyStatus.Applied));
        Assert.That(spawn.Spawns.Length, Is.EqualTo(1));
        Assert.That(client.ApplyFrame(update).Status, Is.EqualTo(ReplicationClientApplyStatus.Applied));
        Assert.That(update.Updates.Length, Is.EqualTo(1));
        Assert.That(client.ApplyFrame(despawn).Status, Is.EqualTo(ReplicationClientApplyStatus.Applied));
        Assert.That(despawn.Despawns.Length, Is.EqualTo(1));
        Assert.That(client.Registry.Count, Is.Zero);
    }

    /// <summary>普通帧没有某实体 Update 且没有 Despawn 时，Client 必须保留该实体。</summary>
    [Test]
    public void ApplyFrame_MissingUpdateWithoutDespawn_KeepsEntity()
    {
        ReplicationClient client = CreateClient();
        client.ApplyFrame(Frame(
            1,
            new[]
            {
                Spawn(1, 10, 1),
                Spawn(2, 20, 1),
            }));

        ReplicationClientApplyResult result = client.ApplyFrame(new ReplicationFrame(
            new NetTick(2),
            new NetSequence(2),
            Array.Empty<SpawnRecord>(),
            new[] { new EntityRecord(new NetEntityId(1), 1, new byte[] { 2 }) },
            Array.Empty<DespawnRecord>(),
            Array.Empty<byte>()));

        Assert.That(result.Status, Is.EqualTo(ReplicationClientApplyStatus.Applied));
        Assert.That(client.Registry.Count, Is.EqualTo(2));
        Assert.That(client.Registry.TryGet(new NetEntityId(2), out _), Is.True);
    }

    /// <summary>整张中间 Update 帧丢失后，后续较新帧仍应更新实体且不能推断 Despawn。</summary>
    [Test]
    public void ApplyFrame_DroppedMiddleFrame_AppliesNewestWithoutDespawn()
    {
        ReplicationClient client = CreateClient();
        var server = new ReplicationServer();
        byte latestPayload = 0;
        client.Spawned += record => latestPayload = record.Payload[0];
        client.Updated += record => latestPayload = record.Payload[0];

        ReplicationFrame spawn = RoundTrip(server.BuildFrame(
            new NetTick(1),
            new[] { State(1, 10, 1) },
            Array.Empty<byte>()));
        ReplicationFrame dropped = server.BuildFrame(
            new NetTick(2),
            new[] { State(1, 10, 2) },
            Array.Empty<byte>());
        ReplicationFrame newest = RoundTrip(server.BuildFrame(
            new NetTick(3),
            new[] { State(1, 10, 3) },
            Array.Empty<byte>()));

        Assert.That(client.ApplyFrame(spawn).Status, Is.EqualTo(ReplicationClientApplyStatus.Applied));
        Assert.That(dropped.Updates, Has.Length.EqualTo(1));
        Assert.That(client.ApplyFrame(newest).Status, Is.EqualTo(ReplicationClientApplyStatus.Applied));
        Assert.That(latestPayload, Is.EqualTo(3));
        Assert.That(client.Registry.Count, Is.EqualTo(1));
        Assert.That(newest.Despawns, Is.Empty);
    }

    /// <summary>旧或重复 Sequence 必须整帧丢弃，不能触发记录事件覆盖新状态。</summary>
    [Test]
    public void ApplyFrame_StaleSequence_DoesNotOverwriteNewState()
    {
        ReplicationClient client = CreateClient();
        byte latestPayload = 0;
        client.Updated += record => latestPayload = record.Payload[0];
        client.ApplyFrame(RoundTrip(Frame(5, new[] { Spawn(1, 10, 1) })));
        client.ApplyFrame(RoundTrip(new ReplicationFrame(
            new NetTick(6),
            new NetSequence(6),
            Array.Empty<SpawnRecord>(),
            new[] { new EntityRecord(new NetEntityId(1), 1, new byte[] { 9 }) },
            Array.Empty<DespawnRecord>(),
            Array.Empty<byte>())));

        ReplicationClientApplyResult stale = client.ApplyFrame(RoundTrip(new ReplicationFrame(
            new NetTick(4),
            new NetSequence(4),
            Array.Empty<SpawnRecord>(),
            new[] { new EntityRecord(new NetEntityId(1), 1, new byte[] { 3 }) },
            Array.Empty<DespawnRecord>(),
            Array.Empty<byte>())));

        Assert.That(stale.Status, Is.EqualTo(ReplicationClientApplyStatus.StaleSequence));
        Assert.That(latestPayload, Is.EqualTo(9));
        Assert.That(client.LatestSequence.Value, Is.EqualTo(6));
    }

    /// <summary>不同实体的 Archetype 必须在 Client Registry 中分别保留，不得回退为同一类型。</summary>
    [Test]
    public void ApplyFrame_TwoArchetypes_RemainDistinct()
    {
        ReplicationClient client = CreateClient();
        var server = new ReplicationServer();
        ReplicationFrame frame = RoundTrip(server.BuildFrame(
            new NetTick(1),
            new[]
            {
                State(1, 100, 1),
                State(2, 200, 1),
            },
            Array.Empty<byte>()));

        ReplicationClientApplyResult result = client.ApplyFrame(frame);

        Assert.That(result.Status, Is.EqualTo(ReplicationClientApplyStatus.Applied));
        Assert.That(result.Spawns, Has.Length.EqualTo(2));
        Assert.That(
            client.Registry.TryGet(new NetEntityId(1), out ReplicatedEntityMetadata first),
            Is.True);
        Assert.That(
            client.Registry.TryGet(new NetEntityId(2), out ReplicatedEntityMetadata second),
            Is.True);
        Assert.That(first.ArchetypeId.Value, Is.EqualTo(100));
        Assert.That(second.ArchetypeId.Value, Is.EqualTo(200));
    }

    /// <summary>同一帧必须先发布 Spawn，再发布 Update，最后发布 Despawn。</summary>
    [Test]
    public void ApplyFrame_PublishesRecordsInLifecycleOrder()
    {
        ReplicationClient client = CreateClient();
        client.ApplyFrame(Frame(1, new[] { Spawn(1, 10, 1) }));
        var order = new List<string>();
        client.Spawned += _ => order.Add("spawn");
        client.Updated += _ => order.Add("update");
        client.Despawned += _ => order.Add("despawn");

        client.ApplyFrame(new ReplicationFrame(
            new NetTick(2),
            new NetSequence(2),
            new[] { Spawn(2, 20, 2) },
            new[] { new EntityRecord(new NetEntityId(1), 1, new byte[] { 2 }) },
            new[] { new DespawnRecord(new NetEntityId(1)) },
            Array.Empty<byte>()));

        Assert.That(order, Is.EqualTo(new[] { "spawn", "update", "despawn" }));
    }

    /// <summary>Schema 解码拒绝的非法 payload 必须拒绝整帧且不提交实体。</summary>
    [Test]
    public void ApplyFrame_InvalidSchemaPayload_RejectsAtomically()
    {
        ReplicationClient client = CreateClient();
        var frame = new ReplicationFrame(
            new NetTick(1),
            new NetSequence(1),
            new[]
            {
                new SpawnRecord(
                    new NetEntityId(1),
                    new NetArchetypeId(10),
                    1,
                    Array.Empty<byte>()),
            },
            Array.Empty<EntityRecord>(),
            Array.Empty<DespawnRecord>(),
            Array.Empty<byte>());

        ReplicationClientApplyResult result = client.ApplyFrame(frame);

        Assert.That(result.Status, Is.EqualTo(ReplicationClientApplyStatus.Rejected));
        Assert.That(result.Message, Does.Contain("payload"));
        Assert.That(client.Registry.Count, Is.Zero);
        Assert.That(client.LatestSequence.IsValid, Is.False);
    }

    static ReplicationClient CreateClient()
    {
        var schemas = new ReplicationSchemaRegistry();
        schemas.Register(new OneByteSchema(1));
        schemas.Register(new OneByteSchema(2));
        return new ReplicationClient(schemas);
    }

    static ReplicationEntityState State(int entityId, int archetypeId, byte value) =>
        new ReplicationEntityState(
            new NetEntityId(entityId),
            new NetArchetypeId(archetypeId),
            1,
            new[] { value });

    static SpawnRecord Spawn(int entityId, int archetypeId, byte value) =>
        new SpawnRecord(
            new NetEntityId(entityId),
            new NetArchetypeId(archetypeId),
            1,
            new[] { value });

    static ReplicationFrame Frame(long sequence, SpawnRecord[] spawns) =>
        new ReplicationFrame(
            new NetTick(sequence),
            new NetSequence(sequence),
            spawns,
            Array.Empty<EntityRecord>(),
            Array.Empty<DespawnRecord>(),
            Array.Empty<byte>());

    // 强制经过真实 V1 线格式，避免 Runtime 测试只覆盖内存对象。
    static ReplicationFrame RoundTrip(ReplicationFrame frame) =>
        ReplicationFrameCodec.Decode(ReplicationFrameCodec.Encode(frame));

    /// <summary>测试用严格单字节 Schema，用于证明后续业务 Schema 可接入同一接口。</summary>
    sealed class OneByteSchema : IReplicationSchema
    {
        /// <summary>创建指定非零标识的测试 Schema。</summary>
        public OneByteSchema(ushort schemaId) => SchemaId = schemaId;

        /// <inheritdoc />
        public ushort SchemaId { get; }

        /// <inheritdoc />
        public byte[] Encode(object state) => new[] { Convert.ToByte(state) };

        /// <inheritdoc />
        public object Decode(byte[] payload)
        {
            if (payload == null || payload.Length != 1)
                throw new FormatException("payload 必须恰好包含一个字节。");
            return payload[0];
        }
    }
}
