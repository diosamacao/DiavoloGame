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
            new NetTick(1), new[] { state }, Array.Empty<byte>(), 128, new NetEntityId(1));
        RejectAll(server, first);
        ReplicationTickDelta retry = server.PrepareTickDelta(
            new NetTick(2), new[] { state }, Array.Empty<byte>(), 128, new NetEntityId(1));
        Assert.That(Count(retry, lifecycle: true), Is.EqualTo(1));
        Assert.That(CountUpdates(retry), Is.EqualTo(1));
        CommitAll(server, retry);

        ReplicationTickDelta unchanged = server.PrepareTickDelta(
            new NetTick(3), new[] { state }, Array.Empty<byte>(), 128, new NetEntityId(1));
        Assert.That(CountUpdates(unchanged), Is.Zero);
    }

    /// <summary>未变化完整状态达到 MaxSilenceTicks 后必须重新发送。</summary>
    [Test]
    public void MaxSilenceTicks_ResendsUnchangedState()
    {
        var server = new ReplicationServer();
        ReplicationEntityState state = State(1, 9);
        CommitAll(server, server.PrepareTickDelta(
            new NetTick(1), new[] { state }, Array.Empty<byte>(), 128, new NetEntityId(1)));

        Assert.That(CountUpdates(server.PrepareTickDelta(
            new NetTick(ReplicationServer.MaxSilenceTicks), new[] { state }, Array.Empty<byte>(), 128, new NetEntityId(1))), Is.Zero);
        Assert.That(CountUpdates(server.PrepareTickDelta(
            new NetTick(ReplicationServer.MaxSilenceTicks + 1), new[] { state }, Array.Empty<byte>(), 128, new NetEntityId(1))), Is.EqualTo(1));
    }

    /// <summary>非 Urgent payload 变化遵守 30Hz 节拍；Urgent 变化仍在当前 Tick 发送。</summary>
    [Test]
    public void ChangedPayload_RespectsNonUrgentCadence_WhileUrgentBypasses()
    {
        var server = new ReplicationServer();
        CommitAll(server, server.PrepareTickDelta(
            new NetTick(1), new[] { State(1, 1) }, Array.Empty<byte>(), 128, new NetEntityId(1)));

        Assert.That(CountUpdates(server.PrepareTickDelta(
            new NetTick(2), new[] { State(1, 2) }, Array.Empty<byte>(), 128, new NetEntityId(1))), Is.Zero);
        Assert.That(CountUpdates(server.PrepareTickDelta(
            new NetTick(3), new[] { State(1, 2) }, Array.Empty<byte>(), 128, new NetEntityId(1))), Is.EqualTo(1));

        var urgent = new ReplicationEntityState(
            new NetEntityId(1), new NetArchetypeId(10), 1, new byte[] { 3 }, urgent: true);
        Assert.That(CountUpdates(server.PrepareTickDelta(
            new NetTick(2), new[] { urgent }, Array.Empty<byte>(), 128, new NetEntityId(1))), Is.EqualTo(1));
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
