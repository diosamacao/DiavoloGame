using System;
using NUnit.Framework;

/// <summary>验证 V2 Server/Client 的 Spawn、完整 Update 与 Despawn 端到端行为。</summary>
public sealed class ReplicationRuntimeTests
{
    /// <summary>状态经真实 V2 Codec 完成 Spawn、Update、Despawn，且缺 Update 不推断删除。</summary>
    [Test]
    public void ServerClient_CompletesLifecycleAndFullState()
    {
        var schemas = new ReplicationSchemaRegistry();
        schemas.Register(new OneByteSchema());
        var client = new ReplicationClient(schemas);
        var server = new ReplicationServer();
        byte latest = 0;
        int despawned = 0;
        client.Updated += (record, _) => latest = record.Payload[0];
        client.Despawned += _ => despawned++;

        ApplyAndCommit(server, client, Prepare(server, 1, State(1, 1)));
        Assert.That(client.Registry.Count, Is.EqualTo(1));
        Assert.That(latest, Is.EqualTo(1));

        ApplyAndCommit(server, client, Prepare(server, 2, State(1, 9)));
        Assert.That(latest, Is.EqualTo(9));
        Assert.That(client.Registry.Count, Is.EqualTo(1));

        ApplyAndCommit(server, client, server.PrepareTickDelta(
            new NetTick(3), Array.Empty<ReplicationEntityState>(), Array.Empty<byte>(), 128, NetEntityId.Invalid));
        Assert.That(client.Registry.Count, Is.Zero);
        Assert.That(despawned, Is.EqualTo(1));
    }

    /// <summary>较旧实体快照不能覆盖已应用的新 Tick。</summary>
    [Test]
    public void Snapshot_OutOfOrderPerEntity_DoesNotRollback()
    {
        var schemas = new ReplicationSchemaRegistry();
        schemas.Register(new OneByteSchema());
        var client = new ReplicationClient(schemas);
        client.ApplyLifecycle(new ReplicationLifecycle(
            1, new NetTick(1), 0, 1,
            new[] { new SpawnRecord(new NetEntityId(1), new NetArchetypeId(10), 1, new byte[] { 0 }) },
            Array.Empty<DespawnRecord>()));
        byte latest = 0;
        client.Updated += (record, _) => latest = record.Payload[0];

        client.ApplySnapshot(Snapshot(3, 3));
        client.ApplySnapshot(Snapshot(2, 2));
        Assert.That(latest, Is.EqualTo(3));
    }

    static ReplicationTickDelta Prepare(ReplicationServer server, long tick, ReplicationEntityState state) =>
        server.PrepareTickDelta(new NetTick(tick), new[] { state }, Array.Empty<byte>(), 128, state.EntityId);

    static ReplicationEntityState State(int id, byte value) =>
        new(new NetEntityId(id), new NetArchetypeId(10), 1, new[] { value });

    static ReplicationSnapshot Snapshot(long tick, byte value) =>
        new(new NetTick(tick), 1, 0, 1,
            new[] { new EntityRecord(new NetEntityId(1), 1, new[] { value }) },
            Array.Empty<byte>());

    static void ApplyAndCommit(ReplicationServer server, ReplicationClient client, ReplicationTickDelta delta)
    {
        for (int i = 0; i < delta.Packets.Length; i++)
        {
            PreparedReplicationPacket packet = delta.Packets[i];
            if (packet.ReliableLifecycle)
                client.ApplyLifecycle(ReplicationProtocolV2Codec.DecodeLifecycle(packet.Body));
            else
                client.ApplySnapshot(ReplicationProtocolV2Codec.DecodeSnapshot(packet.Body));
            server.Commit(packet.Token);
        }
    }

    /// <summary>测试用严格单字节完整状态 Schema。</summary>
    sealed class OneByteSchema : IReplicationSchema
    {
        /// <inheritdoc />
        public ushort SchemaId => 1;
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
