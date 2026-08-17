using System.Threading;
using NUnit.Framework;

/// <summary>验证通用 UDP Transport 的连接分配与 localhost 定向往返。</summary>
public sealed class UdpTransportTests
{
    /// <summary>客户端首包应创建服务端 ConnectionId，并可用同一 Id 定向回包。</summary>
    [Test]
    public void Localhost_SendAndReceive_RoundTripByConnectionId()
    {
        using var server = new UdpTransport();
        using var client = new UdpTransport();
        server.StartServer(new NetEndpoint("0.0.0.0", 0, allowEphemeralPort: true));
        int port = server.LocalEndpoint.Value.Port;
        client.StartClient(new NetEndpoint("127.0.0.1", port));

        client.Send(
            client.Connections[0],
            NetChannel.CommandUnreliableRedundant,
            new byte[] { 1, 2, 3, 4 });

        NetPacket received = default;
        bool hasReceived = false;
        for (int i = 0; i < 20 && !hasReceived; i++)
        {
            server.Poll();
            hasReceived = server.TryReceive(out received);
            if (!hasReceived)
                Thread.Sleep(5);
        }

        Assert.That(hasReceived, Is.True);
        Assert.That(received.ConnectionId.IsValid, Is.True);
        Assert.That(received.Payload, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
        Assert.That(server.Connections, Has.Count.EqualTo(1));

        server.Send(
            received.ConnectionId,
            NetChannel.SnapshotUnreliableSequenced,
            new byte[] { 9, 8, 7 });

        NetPacket echoed = default;
        bool hasEchoed = false;
        for (int i = 0; i < 20 && !hasEchoed; i++)
        {
            client.Poll();
            hasEchoed = client.TryReceive(out echoed);
            if (!hasEchoed)
                Thread.Sleep(5);
        }

        Assert.That(hasEchoed, Is.True);
        Assert.That(echoed.ConnectionId, Is.EqualTo(client.Connections[0]));
        Assert.That(echoed.Payload, Is.EqualTo(new byte[] { 9, 8, 7 }));
    }
}
