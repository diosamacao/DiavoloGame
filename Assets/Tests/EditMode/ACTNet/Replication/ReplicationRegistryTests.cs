using System;
using NUnit.Framework;

/// <summary>验证实体与 Schema 注册表对重复、未知、错配和非法输入给出明确结果。</summary>
public sealed class ReplicationRegistryTests
{
    /// <summary>重复 Spawn、未知 Update、Schema mismatch 与未知 Despawn 均不得静默成功。</summary>
    [Test]
    public void EntityRegistry_InvalidLifecycleOperations_ReturnExplicitResults()
    {
        var registry = new ReplicatedEntityRegistry();
        NetEntityId known = new NetEntityId(1);

        Assert.That(
            registry.TrySpawn(known, new NetArchetypeId(10), 1),
            Is.EqualTo(ReplicatedEntityOperationResult.Success));
        Assert.That(
            registry.TrySpawn(known, new NetArchetypeId(10), 1),
            Is.EqualTo(ReplicatedEntityOperationResult.DuplicateSpawn));
        Assert.That(
            registry.TryUpdate(new NetEntityId(99), 1),
            Is.EqualTo(ReplicatedEntityOperationResult.UnknownEntity));
        Assert.That(
            registry.TryUpdate(known, 2),
            Is.EqualTo(ReplicatedEntityOperationResult.SchemaMismatch));
        Assert.That(
            registry.TryDespawn(new NetEntityId(99)),
            Is.EqualTo(ReplicatedEntityOperationResult.UnknownEntity));
    }

    /// <summary>Schema Registry 必须拒绝零 Id 和重复 Id，并能完成业务对象往返。</summary>
    [Test]
    public void SchemaRegistry_RejectsZeroAndDuplicate_AndRoundTrips()
    {
        var registry = new ReplicationSchemaRegistry();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            registry.Register(new IntegerSchema(0)));

        registry.Register(new IntegerSchema(7));
        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new IntegerSchema(7)));

        byte[] payload = registry.Encode(7, 123456);
        Assert.That(registry.Decode(7, payload), Is.EqualTo(123456));
    }

    /// <summary>Client 对重复 Spawn 必须拒绝整帧且不提交第一条记录。</summary>
    [Test]
    public void Client_DuplicateSpawn_RejectsFrameAtomically()
    {
        ReplicationClient client = CreateClient();
        SpawnRecord duplicate = Spawn(1, 1);
        ReplicationClientApplyResult result = client.ApplyFrame(new ReplicationFrame(
            new NetTick(1),
            new NetSequence(1),
            new[] { duplicate, duplicate },
            Array.Empty<EntityRecord>(),
            Array.Empty<DespawnRecord>(),
            Array.Empty<byte>()));

        Assert.That(result.Status, Is.EqualTo(ReplicationClientApplyStatus.Rejected));
        Assert.That(
            result.OperationResult,
            Is.EqualTo(ReplicatedEntityOperationResult.DuplicateSpawn));
        Assert.That(client.Registry.Count, Is.Zero);
    }

    /// <summary>Client 对未知 Update 与未知 Despawn 必须分别返回 UnknownEntity。</summary>
    [Test]
    public void Client_UnknownUpdateAndDespawn_ReturnUnknownEntity()
    {
        ReplicationClient updateClient = CreateClient();
        ReplicationClientApplyResult update = updateClient.ApplyFrame(new ReplicationFrame(
            new NetTick(1),
            new NetSequence(1),
            Array.Empty<SpawnRecord>(),
            new[] { new EntityRecord(new NetEntityId(1), 1, OnePayload()) },
            Array.Empty<DespawnRecord>(),
            Array.Empty<byte>()));

        ReplicationClient despawnClient = CreateClient();
        ReplicationClientApplyResult despawn = despawnClient.ApplyFrame(new ReplicationFrame(
            new NetTick(1),
            new NetSequence(1),
            Array.Empty<SpawnRecord>(),
            Array.Empty<EntityRecord>(),
            new[] { new DespawnRecord(new NetEntityId(1)) },
            Array.Empty<byte>()));

        Assert.That(
            update.OperationResult,
            Is.EqualTo(ReplicatedEntityOperationResult.UnknownEntity));
        Assert.That(
            despawn.OperationResult,
            Is.EqualTo(ReplicatedEntityOperationResult.UnknownEntity));
    }

    /// <summary>Client 对活动实体的 Schema mismatch 必须拒绝且保留原注册信息。</summary>
    [Test]
    public void Client_SchemaMismatch_RejectsUpdate()
    {
        ReplicationClient client = CreateClient();
        client.ApplyFrame(new ReplicationFrame(
            new NetTick(1),
            new NetSequence(1),
            new[] { Spawn(1, 1) },
            Array.Empty<EntityRecord>(),
            Array.Empty<DespawnRecord>(),
            Array.Empty<byte>()));

        ReplicationClientApplyResult result = client.ApplyFrame(new ReplicationFrame(
            new NetTick(2),
            new NetSequence(2),
            Array.Empty<SpawnRecord>(),
            new[] { new EntityRecord(new NetEntityId(1), 2, OnePayload()) },
            Array.Empty<DespawnRecord>(),
            Array.Empty<byte>()));

        Assert.That(
            result.OperationResult,
            Is.EqualTo(ReplicatedEntityOperationResult.SchemaMismatch));
        Assert.That(client.LatestSequence.Value, Is.EqualTo(1));
        Assert.That(
            client.Registry.TryGet(new NetEntityId(1), out ReplicatedEntityMetadata metadata),
            Is.True);
        Assert.That(metadata.SchemaId, Is.EqualTo(1));
    }

    /// <summary>记录构造边界必须拒绝 null payload，而不是把它隐式解释为空状态。</summary>
    [Test]
    public void RecordConstructors_NullPayload_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SpawnRecord(new NetEntityId(1), new NetArchetypeId(1), 1, null));
        Assert.Throws<ArgumentNullException>(() =>
            new EntityRecord(new NetEntityId(1), 1, null));
    }

    static ReplicationClient CreateClient()
    {
        var schemas = new ReplicationSchemaRegistry();
        schemas.Register(new IntegerSchema(1));
        schemas.Register(new IntegerSchema(2));
        return new ReplicationClient(schemas);
    }

    static SpawnRecord Spawn(int entityId, ushort schemaId) =>
        new SpawnRecord(
            new NetEntityId(entityId),
            new NetArchetypeId(10),
            schemaId,
            OnePayload());

    static byte[] OnePayload() => new byte[] { 1, 0, 0, 0 };

    /// <summary>测试用四字节小端整数 Schema。</summary>
    sealed class IntegerSchema : IReplicationSchema
    {
        /// <summary>创建指定标识的测试 Schema，零值留给注册表拒绝测试。</summary>
        public IntegerSchema(ushort schemaId) => SchemaId = schemaId;

        /// <inheritdoc />
        public ushort SchemaId { get; }

        /// <inheritdoc />
        public byte[] Encode(object state)
        {
            int value = Convert.ToInt32(state);
            return new[]
            {
                (byte)value,
                (byte)(value >> 8),
                (byte)(value >> 16),
                (byte)(value >> 24),
            };
        }

        /// <inheritdoc />
        public object Decode(byte[] payload)
        {
            if (payload == null || payload.Length != 4)
                throw new FormatException("整数 payload 必须为四字节。");
            return payload[0]
                | (payload[1] << 8)
                | (payload[2] << 16)
                | (payload[3] << 24);
        }
    }
}
