using System;
using NUnit.Framework;

/// <summary>验证 V2 注册表冲突、幂等生命周期与 Schema 边界。</summary>
public sealed class ReplicationRegistryTests
{
    /// <summary>重复同定义 Spawn 幂等；同实体改 Archetype 或 Schema 请求单次恢复。</summary>
    [Test]
    public void Lifecycle_DuplicateIsIdempotent_ButConflictRequestsRecovery()
    {
        ReplicationClient client = CreateClient();
        int spawned = 0;
        int recoveries = 0;
        client.Spawned += _ => spawned++;
        client.RecoveryRequired += _ => recoveries++;
        ReplicationLifecycle first = Lifecycle(1, new SpawnRecord(
            new NetEntityId(1), new NetArchetypeId(10), 1, new byte[] { 1 }));

        Assert.That(client.ApplyLifecycle(first), Is.True);
        Assert.That(client.ApplyLifecycle(first), Is.True);
        Assert.That(spawned, Is.EqualTo(1));

        ReplicationLifecycle conflict = Lifecycle(2, new SpawnRecord(
            new NetEntityId(1), new NetArchetypeId(11), 1, new byte[] { 1 }));
        Assert.That(client.ApplyLifecycle(conflict), Is.False);
        Assert.That(client.Registry.Count, Is.EqualTo(1));
        Assert.That(recoveries, Is.EqualTo(1));
    }

    /// <summary>Schema Registry 拒绝零 Id、重复 Id 与坏 payload。</summary>
    [Test]
    public void SchemaRegistry_RejectsInvalidRegistrationAndPayload()
    {
        var schemas = new ReplicationSchemaRegistry();
        Assert.Throws<ArgumentOutOfRangeException>(() => schemas.Register(new OneByteSchema(0)));
        schemas.Register(new OneByteSchema(1));
        Assert.Throws<InvalidOperationException>(() => schemas.Register(new OneByteSchema(1)));
        Assert.Throws<FormatException>(() => schemas.Decode(1, Array.Empty<byte>()));
    }

    static ReplicationClient CreateClient()
    {
        var schemas = new ReplicationSchemaRegistry();
        schemas.Register(new OneByteSchema(1));
        return new ReplicationClient(schemas);
    }

    static ReplicationLifecycle Lifecycle(long sequence, SpawnRecord spawn) =>
        new(sequence, new NetTick(sequence), 0, 1, new[] { spawn }, Array.Empty<DespawnRecord>());

    /// <summary>测试用严格单字节 Schema。</summary>
    sealed class OneByteSchema : IReplicationSchema
    {
        /// <summary>创建指定 Schema Id。</summary>
        public OneByteSchema(ushort id) => SchemaId = id;
        /// <inheritdoc />
        public ushort SchemaId { get; }
        /// <inheritdoc />
        public byte[] Encode(object state) => new[] { Convert.ToByte(state) };
        /// <inheritdoc />
        public object Decode(byte[] payload)
        {
            if (payload == null || payload.Length != 1)
                throw new FormatException("payload 必须为单字节。");
            return payload[0];
        }
    }
}
