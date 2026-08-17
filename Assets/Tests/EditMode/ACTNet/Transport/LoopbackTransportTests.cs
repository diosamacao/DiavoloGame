using System.Collections.Generic;
using NUnit.Framework;

/// <summary>验证通用 Loopback Transport 的多连接、隔离与确定性延迟。</summary>
public sealed class LoopbackTransportTests
{
    static readonly NetEndpoint Endpoint =
        new("loopback", 7777);

    /// <summary>三个客户端必须获得独立服务端连接并只收到定向载荷。</summary>
    [Test]
    public void ThreeClients_SendReceiveAndDisconnect_AreIndependent()
    {
        var network = new LoopbackNetwork();
        using var server = new LoopbackTransport(network);
        using var clientA = new LoopbackTransport(network);
        using var clientB = new LoopbackTransport(network);
        using var clientC = new LoopbackTransport(network);
        server.StartServer(Endpoint);
        clientA.StartClient(Endpoint);
        clientB.StartClient(Endpoint);
        clientC.StartClient(Endpoint);

        Assert.That(server.Connections, Has.Count.EqualTo(3));
        Assert.That(new HashSet<NetConnectionId>(server.Connections), Has.Count.EqualTo(3));

        SendClientByte(clientA, 11);
        SendClientByte(clientB, 22);
        SendClientByte(clientC, 33);
        server.Poll();

        var receivedByValue = new Dictionary<byte, NetConnectionId>();
        while (server.TryReceive(out NetPacket packet))
            receivedByValue.Add(packet.Payload[0], packet.ConnectionId);

        Assert.That(receivedByValue.Keys, Is.EquivalentTo(new byte[] { 11, 22, 33 }));
        foreach (KeyValuePair<byte, NetConnectionId> pair in receivedByValue)
        {
            server.Send(
                pair.Value,
                NetChannel.SnapshotUnreliableSequenced,
                new[] { (byte)(pair.Key + 1) });
        }

        clientA.Poll();
        clientB.Poll();
        clientC.Poll();
        AssertReceivedByte(clientA, 12);
        AssertReceivedByte(clientB, 23);
        AssertReceivedByte(clientC, 34);

        clientB.Disconnect(
            clientB.Connections[0],
            DisconnectReason.Requested);
        Assert.That(server.Connections, Has.Count.EqualTo(2));
        Assert.That(clientB.Connections, Is.Empty);
        Assert.That(clientA.Connections, Has.Count.EqualTo(1));
        Assert.That(clientC.Connections, Has.Count.EqualTo(1));
    }

    /// <summary>数据包只能在模拟时钟达到单向延迟后交付。</summary>
    [Test]
    public void ConfiguredLatency_DelaysDeliveryUntilAdvance()
    {
        var network = new LoopbackNetwork();
        network.SetLatencyMs(50);
        using var server = new LoopbackTransport(network);
        using var client = new LoopbackTransport(network);
        server.StartServer(Endpoint);
        client.StartClient(Endpoint);

        SendClientByte(client, 7);
        server.Poll();
        Assert.That(server.TryReceive(out _), Is.False);

        network.AdvanceTimeMs(49);
        server.Poll();
        Assert.That(server.TryReceive(out _), Is.False);

        network.AdvanceTimeMs(1);
        server.Poll();
        Assert.That(server.TryReceive(out NetPacket packet), Is.True);
        Assert.That(packet.Payload, Is.EqualTo(new byte[] { 7 }));
    }

    /// <summary>从客户端唯一服务端连接发送一字节命令。</summary>
    static void SendClientByte(LoopbackTransport client, byte value) =>
        client.Send(
            client.Connections[0],
            NetChannel.CommandUnreliableRedundant,
            new[] { value });

    /// <summary>断言客户端只收到期望的一条定向载荷。</summary>
    static void AssertReceivedByte(LoopbackTransport client, byte expected)
    {
        Assert.That(client.TryReceive(out NetPacket packet), Is.True);
        Assert.That(packet.Payload, Is.EqualTo(new[] { expected }));
        Assert.That(client.TryReceive(out _), Is.False);
    }
}
