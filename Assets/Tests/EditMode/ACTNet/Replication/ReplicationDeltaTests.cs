using System;
using NUnit.Framework;

/// <summary>验证 V2 增量基线只由成功提交推进。</summary>
public sealed class ReplicationDeltaTests
{
    /// <summary>Reject 后同实体仍产生 Spawn/Update；Commit 后未变状态跳过。</summary>
    [Test]
    public void RejectRetriesAndCommitAdvancesBaseline()
    {
        var server = new ReplicationServer();
        ReplicationEntityState state = State(1, 7);

        ReplicationTickDelta first = server.PrepareTickDelta(
            new NetTick(1), new[] { state }, Array.Empty<byte>(), 128, ReplicationBuildOptions.Compatible);
        RejectAll(server, first);
        ReplicationTickDelta retry = server.PrepareTickDelta(
            new NetTick(2), new[] { state }, Array.Empty<byte>(), 128, ReplicationBuildOptions.Compatible);
        Assert.That(Count(retry, lifecycle: true), Is.EqualTo(1));
        Assert.That(CountUpdates(retry), Is.EqualTo(1));
        CommitAll(server, retry);

        ReplicationTickDelta unchanged = server.PrepareTickDelta(
            new NetTick(3), new[] { state }, Array.Empty<byte>(), 128, ReplicationBuildOptions.Compatible);
        Assert.That(CountUpdates(unchanged), Is.Zero);
    }

    /// <summary>未变化完整状态达到 MaxSilenceTicks 后必须重新发送。</summary>
    [Test]
    public void MaxSilenceTicks_ResendsUnchangedState()
    {
        var server = new ReplicationServer();
        ReplicationEntityState state = State(1, 9);
        CommitAll(server, server.PrepareTickDelta(
            new NetTick(1), new[] { state }, Array.Empty<byte>(), 128, ReplicationBuildOptions.Compatible));

        Assert.That(CountUpdates(server.PrepareTickDelta(
            new NetTick(ReplicationServer.MaxSilenceTicks), new[] { state }, Array.Empty<byte>(), 128, ReplicationBuildOptions.Compatible)), Is.Zero);
        Assert.That(CountUpdates(server.PrepareTickDelta(
            new NetTick(ReplicationServer.MaxSilenceTicks + 1), new[] { state }, Array.Empty<byte>(), 128, ReplicationBuildOptions.Compatible)), Is.EqualTo(1));
    }

    /// <summary>非 Urgent payload 变化遵守 30Hz 节拍；Urgent 变化仍在当前 Tick 发送。</summary>
    [Test]
    public void ChangedPayload_RespectsNonUrgentCadence_WhileUrgentBypasses()
    {
        var server = new ReplicationServer();
        CommitAll(server, server.PrepareTickDelta(
            new NetTick(1), new[] { State(1, 1) }, Array.Empty<byte>(), 128, ReplicationBuildOptions.Compact));

        Assert.That(CountUpdates(server.PrepareTickDelta(
            new NetTick(2), new[] { State(1, 2) }, Array.Empty<byte>(), 128, ReplicationBuildOptions.Compact)), Is.Zero);
        Assert.That(CountUpdates(server.PrepareTickDelta(
            new NetTick(3), new[] { State(1, 2) }, Array.Empty<byte>(), 128, ReplicationBuildOptions.Compact)), Is.EqualTo(1));

        var urgent = new ReplicationEntityState(
            new NetEntityId(1), new NetArchetypeId(10), 1, new byte[] { 3 }, urgent: true);
        Assert.That(CountUpdates(server.PrepareTickDelta(
            new NetTick(2), new[] { urgent }, Array.Empty<byte>(), 128, ReplicationBuildOptions.Compact)), Is.EqualTo(1));
    }

    /// <summary>连接级预算先发送 Owner，未装入的普通实体保持脏并在后续 Tick 公平补发。</summary>
    [Test]
    public void UpdateBudget_PrioritizesOwner_AndRetriesDeferredEntities()
    {
        var server = new ReplicationServer();
        var initial = new[] { State(1, 0), State(2, 0), State(3, 0), State(4, 0) };
        CommitAll(server, server.PrepareTickDelta(
            new NetTick(1), initial, Array.Empty<byte>(), 128, ReplicationBuildOptions.Compatible));

        var changed = new[] { State(1, 1), State(2, 1), State(3, 1), State(4, 1) };
        var options = new ReplicationBuildOptions(
            skipUnchanged: true,
            maxUpdateBytes: ReplicationProtocolV2Codec.UpdateRecordHeaderBytes + 1,
            snapshotIntervalTicks: ReplicationServer.NonUrgentSendIntervalTicks,
            preferredEntity: new NetEntityId(4),
            forceFull: false);
        var delivered = new System.Collections.Generic.List<int>();

        for (int tick = 2; tick <= 5; tick++)
        {
            ReplicationTickDelta delta = server.PrepareTickDelta(
                new NetTick(tick), changed, Array.Empty<byte>(), 128, options);
            EntityRecord[] updates = ReadUpdates(delta);
            Assert.That(updates.Length, Is.EqualTo(1));
            delivered.Add(updates[0].EntityId.Value);
            CommitAll(server, delta);
        }

        Assert.That(delivered[0], Is.EqualTo(4));
        Assert.That(delivered, Is.EquivalentTo(new[] { 1, 2, 3, 4 }));
    }

    static ReplicationEntityState State(int id, byte value) =>
        new(new NetEntityId(id), new NetArchetypeId(10), 1, new[] { value });

    static int Count(ReplicationTickDelta delta, bool lifecycle)
    {
        int count = 0;
        for (int i = 0; i < delta.Packets.Length; i++)
            if (delta.Packets[i].ReliableLifecycle == lifecycle) count++;
        return count;
    }

    static int CountUpdates(ReplicationTickDelta delta)
    {
        int count = 0;
        for (int i = 0; i < delta.Packets.Length; i++)
            if (!delta.Packets[i].ReliableLifecycle)
                count += ReplicationProtocolV2Codec.DecodeSnapshot(delta.Packets[i].Body).Updates.Length;
        return count;
    }

    static EntityRecord[] ReadUpdates(ReplicationTickDelta delta)
    {
        for (int i = 0; i < delta.Packets.Length; i++)
            if (!delta.Packets[i].ReliableLifecycle)
                return ReplicationProtocolV2Codec.DecodeSnapshot(delta.Packets[i].Body).Updates;
        return Array.Empty<EntityRecord>();
    }

    static void CommitAll(ReplicationServer server, ReplicationTickDelta delta)
    {
        for (int i = 0; i < delta.Packets.Length; i++)
            server.Commit(delta.Packets[i].Token);
    }

    static void RejectAll(ReplicationServer server, ReplicationTickDelta delta)
    {
        for (int i = 0; i < delta.Packets.Length; i++)
            server.Reject(delta.Packets[i].Token);
    }
}
