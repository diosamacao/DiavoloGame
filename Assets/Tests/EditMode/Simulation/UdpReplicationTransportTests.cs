using System.Net;
using System.Threading;
using NUnit.Framework;

/// <summary>本机 UDP 往返，验证第二传输实现走 IReplicationTransport。</summary>
public sealed class UdpReplicationTransportTests
{
    /// <summary>localhost 上行后权威能取出同一正文，再广播回客机。</summary>
    [Test]
    public void Localhost_SendAndReceive_Roundtrip()
    {
        using var host = new UdpReplicationTransport();
        using var client = new UdpReplicationTransport();
        host.Bind(0);
        client.Connect("127.0.0.1", host.BoundPort);

        byte[] up = { 1, 2, 3, 4 };
        client.SendClientToAuthority(up);

        byte[] received = null;
        IPEndPoint from = null;
        for (int i = 0; i < 20 && received == null; i++)
        {
            host.Pump();
            if (host.TryDequeueAuthorityFrom(out received, out from))
                break;
            Thread.Sleep(5);
        }

        Assert.That(received, Is.EqualTo(up));
        Assert.That(from, Is.Not.Null);

        host.AddClient(from);
        byte[] down = { 9, 8, 7 };
        host.SendAuthorityToClients(down);

        byte[] echoed = null;
        for (int i = 0; i < 20 && echoed == null; i++)
        {
            client.Pump();
            if (client.TryDequeueClient(out echoed))
                break;
            Thread.Sleep(5);
        }

        Assert.That(echoed, Is.EqualTo(down));
    }
}
