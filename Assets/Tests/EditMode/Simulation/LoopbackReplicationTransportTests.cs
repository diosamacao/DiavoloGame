using System;
using NUnit.Framework;

/// <summary>验证 ReplicationFrame 经通用 Loopback Transport 后由 Server/Client 单轨应用。</summary>
public sealed class LoopbackReplicationTransportTests
{
    /// <summary>Server full set 构帧、线编码、Loopback 传输与 Client 生命周期应用完整往返。</summary>
    [Test]
    public void ReplicationFrame_ServerToClient_RoundTripsAndApplies()
    {
        var network = new LoopbackNetwork();
        using var serverTransport = new LoopbackTransport(network);
        using var clientTransport = new LoopbackTransport(network);
        var endpoint = new NetEndpoint("loopback", 7788);
        serverTransport.StartServer(endpoint);
        clientTransport.StartClient(endpoint);

        var schema = new CharacterSnapshotSchemaV1();
        var schemas = new ReplicationSchemaRegistry();
        schemas.Register(schema);
        var server = new ReplicationServer();
        var client = new ReplicationClient(schemas);
        ActorReplicationSnapshot snapshot = MinimalSnapshot(new SimActorId(3));
        var state = new ReplicationEntityState(
            new NetEntityId(3),
            new NetArchetypeId(7),
            CharacterSnapshotSchemaV1.Id,
            schema.Encode(in snapshot));
        ReplicationFrame sent = server.BuildFrame(
            new NetTick(1),
            new[] { state },
            Array.Empty<byte>());

        serverTransport.Send(
            serverTransport.Connections[0],
            NetChannel.SnapshotUnreliableSequenced,
            ReplicationFrameCodec.Encode(sent));
        clientTransport.Poll();

        Assert.That(clientTransport.TryReceive(out NetPacket packet), Is.True);
        ReplicationFrame received = ReplicationFrameCodec.Decode(packet.Payload);
        ReplicationClientApplyResult result = client.ApplyFrame(received);
        Assert.That(result.Status, Is.EqualTo(ReplicationClientApplyStatus.Applied));
        Assert.That(result.Spawns, Has.Length.EqualTo(1));
        Assert.That(result.Spawns[0].EntityId.Value, Is.EqualTo(3));
        ActorReplicationSnapshot restored =
            schema.DecodeSnapshot(result.Spawns[0].Payload);
        Assert.That(restored.Equals(snapshot), Is.True);
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
            100000,
            0,
            VitalityReplicationEdge.None);
}
