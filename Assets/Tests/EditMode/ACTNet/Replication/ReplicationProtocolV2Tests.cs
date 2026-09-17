using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>覆盖 V2 codec/MTU、prepare-commit、批处理、周期重发与客户端屏障。</summary>
public sealed class ReplicationProtocolV2Tests
{
    const int Mtu = 1400;
    const int MuxHeaderBytes = 9;
    const int SessionHeaderBytes = 2;
    const int BodyBudget = Mtu - MuxHeaderBytes - SessionHeaderBytes;

    /// <summary>正文用满预算后加 Session 与 Mux 头仍不超过 MTU。</summary>
    [Test]
    public void Codec_ExactBodyBudget_FinalDatagramFitsMtu()
    {
        int payloadBytes = BodyBudget
            - ReplicationProtocolV2Codec.SnapshotHeaderBytes
            - ReplicationProtocolV2Codec.UpdateRecordHeaderBytes;
        var snapshot = new ReplicationSnapshot(
            new NetTick(1),
            0,
            0,
            1,
            new[] { new EntityRecord(new NetEntityId(1), 1, new byte[payloadBytes]) },
            Array.Empty<byte>());

        byte[] body = ReplicationProtocolV2Codec.EncodeSnapshot(snapshot, BodyBudget);

        Assert.That(body.Length, Is.EqualTo(BodyBudget));
        Assert.That(body.Length + SessionHeaderBytes + MuxHeaderBytes, Is.EqualTo(Mtu));
        Assert.That(ReplicationProtocolV2Codec.DecodeSnapshot(body).Updates.Length, Is.EqualTo(1));
        Assert.Throws<NetBufferException>(() => ReplicationProtocolV2Codec.EncodeSnapshot(snapshot, BodyBudget - 1));
    }

    /// <summary>准备不改基线；Reject 可重试，只有成功 Commit 才写注册表和快照 cache。</summary>
    [Test]
    public void PrepareCommit_RejectRetriesWithoutMutatingBaseline()
    {
        var server = new ReplicationServer();
        ReplicationEntityState state = State(1, new byte[] { 7 });
        ReplicationTickDelta first = server.PrepareTickDelta(new NetTick(1), new[] { state }, Array.Empty<byte>(), 128, new NetEntityId(1));

        Assert.That(server.Registry.Count, Is.Zero);
        for (int i = 0; i < first.Packets.Length; i++) server.Reject(first.Packets[i].Token);

        ReplicationTickDelta retry = server.PrepareTickDelta(new NetTick(2), new[] { state }, Array.Empty<byte>(), 128, new NetEntityId(1));
        Assert.That(Array.Exists(retry.Packets, packet => packet.ReliableLifecycle), Is.True);
        CommitAll(server, retry);
        Assert.That(server.Registry.Count, Is.EqualTo(1));

        ReplicationTickDelta unchanged = server.PrepareTickDelta(new NetTick(3), new[] { state }, Array.Empty<byte>(), 128, new NetEntityId(1));
        Assert.That(CountUpdates(unchanged), Is.Zero);
    }

    /// <summary>Commit/Reject 票据只能由创建它的 Server 结束一次，外部 Guid 不能伪造。</summary>
    [Test]
    public void CommitToken_IsOwnerBoundAndOneShot()
    {
        var owner = new ReplicationServer();
        var other = new ReplicationServer();
        ReplicationTickDelta delta = owner.PrepareTickDelta(
            new NetTick(1), new[] { State(1, new byte[] { 1 }) }, Array.Empty<byte>(), 128, new NetEntityId(1));
        ReplicationPacketCommitToken token = delta.Packets[0].Token;

        Assert.Throws<InvalidOperationException>(() => other.Commit(token));
        owner.Reject(token);
        Assert.Throws<InvalidOperationException>(() => owner.Reject(token));
        Assert.Throws<InvalidOperationException>(
            () => owner.Commit(new ReplicationPacketCommitToken(Guid.NewGuid())));
        Assert.That(owner.Registry.Count, Is.Zero);
    }

    /// <summary>20+ Spawn 与完整状态会稳定拆成多个预算内批，Owner 首个快照优先。</summary>
    [Test]
    public void Prepare_MoreThanTwentySpawns_BatchesWithinBudget()
    {
        var states = new List<ReplicationEntityState>();
        for (int i = 1; i <= 24; i++) states.Add(State(i, new byte[20], urgent: i == 2));
        var server = new ReplicationServer();

        ReplicationTickDelta delta = server.PrepareTickDelta(new NetTick(1), states, Array.Empty<byte>(), 80, new NetEntityId(24));

        Assert.That(delta.Packets.Length, Is.GreaterThan(2));
        for (int i = 0; i < delta.Packets.Length; i++) Assert.That(delta.Packets[i].Body.Length, Is.LessThanOrEqualTo(80));
        PreparedReplicationPacket firstSnapshot = Array.Find(delta.Packets, packet => !packet.ReliableLifecycle);
        Assert.That(ReplicationProtocolV2Codec.DecodeSnapshot(firstSnapshot.Body).Updates[0].EntityId.Value, Is.EqualTo(24));
    }

    /// <summary>不可靠完整状态丢失后，MaxSilenceTicks 到期会重发未变化实体。</summary>
    [Test]
    public void SnapshotLoss_UnchangedStatePeriodicallyResends()
    {
        var server = new ReplicationServer();
        ReplicationEntityState state = State(1, new byte[] { 9 });
        CommitAll(server, server.PrepareTickDelta(new NetTick(1), new[] { state }, Array.Empty<byte>(), 128, new NetEntityId(1)));

        Assert.That(CountUpdates(server.PrepareTickDelta(new NetTick(2), new[] { state }, Array.Empty<byte>(), 128, new NetEntityId(1))), Is.Zero);
        Assert.That(CountUpdates(server.PrepareTickDelta(new NetTick(31), new[] { state }, Array.Empty<byte>(), 128, new NetEntityId(1))), Is.EqualTo(1));
    }

    /// <summary>恢复准备会为已注册实体重新发送可靠 Spawn，并让客户端保留序列后重建注册表。</summary>
    [Test]
    public void Recovery_ForceFull_ReplaysLifecycleWithoutSequenceReset()
    {
        var schemas = new ReplicationSchemaRegistry();
        schemas.Register(new ByteSchema());
        var client = new ReplicationClient(schemas);
        var server = new ReplicationServer();
        ReplicationEntityState state = State(1, new byte[] { 1 });
        ReplicationTickDelta initial = server.PrepareTickDelta(new NetTick(1), new[] { state }, Array.Empty<byte>(), 128, new NetEntityId(1));
        ApplyAndCommit(server, client, initial);
        client.ResetForRecovery();

        ReplicationTickDelta recovery = server.PrepareTickDelta(new NetTick(2), new[] { state }, Array.Empty<byte>(), 128, new NetEntityId(1), forceFull: true);
        ApplyAndCommit(server, client, recovery);

        Assert.That(client.Registry.Count, Is.EqualTo(1));
        Assert.That(client.RecoveryRequested, Is.False);
    }

    /// <summary>乱序快照等待生命周期后释放；重复生命周期幂等且不重复发布。</summary>
    [Test]
    public void Client_BarrierAndLifecycle_AreOrderedAndIdempotent()
    {
        var schemas = new ReplicationSchemaRegistry();
        schemas.Register(new ByteSchema());
        var client = new ReplicationClient(schemas);
        int spawns = 0;
        int updates = 0;
        client.Spawned += _ => spawns++;
        client.Updated += (_, __) => updates++;
        var snapshot = new ReplicationSnapshot(
            new NetTick(5), 1, 0, 1,
            new[] { new EntityRecord(new NetEntityId(1), 1, new byte[] { 3 }) },
            Array.Empty<byte>());

        Assert.That(client.ApplySnapshot(snapshot), Is.EqualTo(ReplicationSnapshotApplyResult.Buffered));
        Assert.That(updates, Is.Zero);
        var lifecycle = new ReplicationLifecycle(
            1, new NetTick(4), 0, 1,
            new[] { new SpawnRecord(new NetEntityId(1), new NetArchetypeId(1), 1, new byte[] { 1 }) },
            Array.Empty<DespawnRecord>());
        Assert.That(client.ApplyLifecycle(lifecycle), Is.True);
        Assert.That(client.ApplyLifecycle(lifecycle), Is.True);
        Assert.That(spawns, Is.EqualTo(1));
        Assert.That(updates, Is.EqualTo(1));
    }

    /// <summary>缓冲超限只触发一次恢复，后续乱序包不制造恢复风暴。</summary>
    [Test]
    public void Client_BufferLimit_RaisesSingleRecovery()
    {
        var schemas = new ReplicationSchemaRegistry();
        schemas.Register(new ByteSchema());
        var client = new ReplicationClient(schemas);
        int recoveries = 0;
        client.RecoveryRequired += _ => recoveries++;
        var updates = new EntityRecord[ReplicationClient.MaxBufferedEntities + 1];
        for (int i = 0; i < updates.Length; i++)
            updates[i] = new EntityRecord(new NetEntityId(i + 1), 1, new byte[] { 1 });

        client.ApplySnapshot(new ReplicationSnapshot(new NetTick(1), 99, 0, 1, updates, Array.Empty<byte>()));
        client.ApplySnapshot(new ReplicationSnapshot(new NetTick(40), 99, 0, 1, updates, Array.Empty<byte>()));

        Assert.That(client.RecoveryRequested, Is.True);
        Assert.That(recoveries, Is.EqualTo(1));
        Assert.That(client.BufferedEntityCount, Is.EqualTo(ReplicationClient.MaxBufferedEntities));
        Assert.That(client.BufferedTickCount, Is.LessThanOrEqualTo(ReplicationClient.MaxBufferedTicks));
    }

    /// <summary>坏 Update 必须在发布 Meta 或任何实体事件前整批拒绝。</summary>
    [Test]
    public void Client_InvalidUpdate_IsRejectedBeforePublishingBatch()
    {
        var schemas = new ReplicationSchemaRegistry();
        schemas.Register(new ByteSchema());
        var client = new ReplicationClient(schemas);
        int metadataApplied = 0;
        int updates = 0;
        client.MetadataApplied += (_, __) => metadataApplied++;
        client.Updated += (_, __) => updates++;

        client.ApplySnapshot(new ReplicationSnapshot(
            new NetTick(1), 0, 0, 1,
            new[] { new EntityRecord(new NetEntityId(99), 1, new byte[] { 1 }) },
            new byte[] { 2 }));

        Assert.That(client.RecoveryRequested, Is.True);
        Assert.That(metadataApplied, Is.Zero);
        Assert.That(updates, Is.Zero);
    }

    /// <summary>同 Tick 互补 Snapshot batch 反序到达时，两批实体都由应用层接收，Meta 只发布一次。</summary>
    [Test]
    public void Client_SameTickComplementaryBatches_Reversed_AppliesBoth()
    {
        var schemas = new ReplicationSchemaRegistry();
        schemas.Register(new ByteSchema());
        var client = new ReplicationClient(schemas);
        client.ApplyLifecycle(new ReplicationLifecycle(
            1,
            new NetTick(1),
            0,
            1,
            new[]
            {
                new SpawnRecord(new NetEntityId(1), new NetArchetypeId(1), 1, new byte[] { 0 }),
                new SpawnRecord(new NetEntityId(2), new NetArchetypeId(1), 1, new byte[] { 0 }),
            },
            Array.Empty<DespawnRecord>()));
        var applied = new List<int>();
        int metaCount = 0;
        client.Updated += (record, _) => applied.Add(record.EntityId.Value);
        client.MetadataApplied += (_, __) => metaCount++;
        byte[] meta = { 9 };

        client.ApplySnapshot(new ReplicationSnapshot(
            new NetTick(10), 1, 1, 2,
            new[] { new EntityRecord(new NetEntityId(2), 1, new byte[] { 2 }) },
            meta));
        client.ApplySnapshot(new ReplicationSnapshot(
            new NetTick(10), 1, 0, 2,
            new[] { new EntityRecord(new NetEntityId(1), 1, new byte[] { 1 }) },
            meta));

        Assert.That(applied, Is.EquivalentTo(new[] { 1, 2 }));
        Assert.That(metaCount, Is.EqualTo(1));
    }

    static ReplicationEntityState State(int id, byte[] payload, bool urgent = false) =>
        new ReplicationEntityState(new NetEntityId(id), new NetArchetypeId(1), 1, payload, urgent);

    static void CommitAll(ReplicationServer server, ReplicationTickDelta delta)
    {
        for (int i = 0; i < delta.Packets.Length; i++) server.Commit(delta.Packets[i].Token);
    }

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

    static int CountUpdates(ReplicationTickDelta delta)
    {
        int count = 0;
        for (int i = 0; i < delta.Packets.Length; i++)
            if (!delta.Packets[i].ReliableLifecycle) count += ReplicationProtocolV2Codec.DecodeSnapshot(delta.Packets[i].Body).Updates.Length;
        return count;
    }

    sealed class ByteSchema : IReplicationSchema
    {
        public ushort SchemaId => 1;
        public byte[] Encode(object state) => (byte[])state;
        public object Decode(byte[] payload) => payload;
    }
}
