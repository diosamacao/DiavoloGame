using System.Collections.Generic;
using NUnit.Framework;

/// <summary>通道 Mux：可靠有序重传、Snapshot 不可靠交付、超 MTU 拒绝。不引用 ACT。</summary>
public sealed class ChannelMuxTransportTests
{
    static readonly NetEndpoint Endpoint = new("mux-loopback", 1);

    /// <summary>可靠消息乱序到达后仍按发送顺序交付。</summary>
    [Test]
    public void Reliable_OutOfOrder_DeliversInSequence()
    {
        using var link = new DuplexLink();
        link.HoldClientSends = true;
        ChannelMuxTransport server = ChannelMuxTransport.Wrap(link.Server);
        ChannelMuxTransport client = ChannelMuxTransport.Wrap(link.Client);
        server.StartServer(Endpoint);
        client.StartClient(Endpoint);

        client.AdvanceClock(0);
        client.Send(client.Connections[0], NetChannel.EventReliableOrdered, new byte[] { 1 });
        client.Send(client.Connections[0], NetChannel.EventReliableOrdered, new byte[] { 2 });
        client.Send(client.Connections[0], NetChannel.EventReliableOrdered, new byte[] { 3 });
        link.ReleaseHeldClientSends(2, 0, 1);
        server.AdvanceClock(0);
        server.Poll();

        Assert.That(DrainPayloads(server), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    /// <summary>首包丢失后，超时重传仍能交付且只交付一次。</summary>
    [Test]
    public void Reliable_FirstPacketDropped_RetransmitDeliversOnce()
    {
        using var link = new DuplexLink();
        link.DropNextClientSends = 1;
        ChannelMuxTransport server = ChannelMuxTransport.Wrap(link.Server);
        ChannelMuxTransport client = ChannelMuxTransport.Wrap(link.Client);
        server.StartServer(Endpoint);
        client.StartClient(Endpoint);

        client.AdvanceClock(0);
        client.Send(client.Connections[0], NetChannel.ControlReliableOrdered, new byte[] { 7 });
        server.AdvanceClock(0);
        server.Poll();
        Assert.That(DrainPayloads(server), Is.Empty);

        client.AdvanceClock(50);
        client.Poll();
        server.AdvanceClock(50);
        server.Poll();
        Assert.That(DrainPayloads(server), Is.EqualTo(new[] { 7 }));

        client.AdvanceClock(100);
        client.Poll();
        server.AdvanceClock(100);
        server.Poll();
        Assert.That(DrainPayloads(server), Is.Empty);
    }

    /// <summary>首次 Poll 前发送的握手没有时钟基线；ACK 后 RTT 仍应保持未知而非整数溢出。</summary>
    [Test]
    public void Reliable_SendBeforeClock_DoesNotProduceNegativeMetrics()
    {
        using var link = new DuplexLink();
        ChannelMuxTransport server = ChannelMuxTransport.Wrap(link.Server);
        ChannelMuxTransport client = ChannelMuxTransport.Wrap(link.Client);
        server.StartServer(Endpoint);
        client.StartClient(Endpoint);

        client.Send(client.Connections[0], NetChannel.ControlReliableOrdered, new byte[] { 1 });
        server.AdvanceClock(1_700_000_000_000L);
        server.Poll();
        client.AdvanceClock(1_700_000_000_001L);
        client.Poll();

        Assert.That(client.Metrics.RttMs, Is.EqualTo(-1));
        Assert.That(client.Metrics.JitterMs, Is.EqualTo(-1));
    }

    /// <summary>Snapshot 数据报不在 Mux 丢旧；同 Tick 多 batch 的判定归 ReplicationClient。</summary>
    [Test]
    public void Snapshot_OutOfOrderPackets_AreBothDelivered()
    {
        using var link = new DuplexLink();
        ChannelMuxTransport server = ChannelMuxTransport.Wrap(link.Server);
        ChannelMuxTransport client = ChannelMuxTransport.Wrap(link.Client);
        server.StartServer(Endpoint);
        client.StartClient(Endpoint);

        server.AdvanceClock(0);
        server.Send(server.Connections[0], NetChannel.SnapshotUnreliableSequenced, new byte[] { 10 });
        server.Send(server.Connections[0], NetChannel.SnapshotUnreliableSequenced, new byte[] { 11 });
        byte[] stale = link.LastServerDatagrams[0];
        client.AdvanceClock(0);
        client.Poll();
        Assert.That(DrainPayloads(client), Is.EqualTo(new[] { 10, 11 }));

        link.InjectToClient(stale);
        client.Poll();
        Assert.That(DrainPayloads(client), Is.EqualTo(new[] { 10 }));
        Assert.That(client.Metrics.PacketsDropped, Is.Zero);
    }

    /// <summary>超 MTU 的发送被拒绝且不进入底层。</summary>
    [Test]
    public void Send_OverMtu_IsRejected()
    {
        using var link = new DuplexLink();
        ChannelMuxTransport client = ChannelMuxTransport.Wrap(link.Client, maxDatagramBytes: 32);
        client.StartClient(Endpoint);
        Assert.Throws<System.InvalidOperationException>(
            () => client.Send(
                client.Connections[0],
                NetChannel.CommandUnreliableRedundant,
                new byte[64]));
        Assert.That(client.OversizeRejected, Is.EqualTo(1));
        Assert.That(link.ClientToServer.Count, Is.Zero);
    }

    /// <summary>可靠窗口满时在底层发送前背压，并保留全部最旧未确认包。</summary>
    [Test]
    public void Reliable_WindowFull_RejectsNewestWithoutDroppingOldest()
    {
        using var link = new DuplexLink { HoldClientSends = true };
        ChannelMuxTransport client = ChannelMuxTransport.Wrap(link.Client);
        client.StartClient(Endpoint);
        for (int i = 0; i < 64; i++)
            client.Send(client.Connections[0], NetChannel.EventReliableOrdered, new[] { (byte)i });

        Assert.Throws<System.InvalidOperationException>(
            () => client.Send(client.Connections[0], NetChannel.EventReliableOrdered, new byte[] { 64 }));
        Assert.That(link.HeldClientSends.Count, Is.EqualTo(64));
        Assert.That(client.ReliableBackpressureRejected, Is.EqualTo(1));
    }

    /// <summary>超出可靠接收窗口的未来包不得缓存或 ACK，避免无界 Hold 与永久序列缺口。</summary>
    [Test]
    public void Reliable_FutureOutsideWindow_IsDroppedWithoutAck()
    {
        using var link = new DuplexLink();
        ChannelMuxTransport server = ChannelMuxTransport.Wrap(link.Server);
        server.StartServer(Endpoint);
        link.Server.Inbox.Enqueue(EncodeReliable(seq: 64, payload: 9));

        server.Poll();

        Assert.That(DrainPayloads(server), Is.Empty);
        Assert.That(link.Client.Inbox.Count, Is.Zero);
        Assert.That(server.Metrics.PacketsDropped, Is.EqualTo(1));
    }

    /// <summary>Mux 交付队列达到硬上限后拒绝最新不可靠包，不允许无界增长。</summary>
    [Test]
    public void DeliveredQueue_IsBounded()
    {
        using var link = new DuplexLink();
        ChannelMuxTransport server = ChannelMuxTransport.Wrap(link.Server);
        server.StartServer(Endpoint);
        for (ushort seq = 0; seq < 257; seq++)
            link.Server.Inbox.Enqueue(EncodeUnreliable(seq, (byte)seq));

        server.Poll();

        Assert.That(DrainPayloads(server), Has.Length.EqualTo(256));
        Assert.That(server.Metrics.PacketsDropped, Is.EqualTo(1));
    }

    /// <summary>构造测试用 Mux V1 可靠数据报。</summary>
    static byte[] EncodeReliable(ushort seq, byte payload)
    {
        var writer = new NetBufferWriter(10);
        writer.WriteByte(1);
        writer.WriteByte((byte)NetChannel.EventReliableOrdered);
        writer.WriteByte(1);
        writer.WriteUInt16(seq);
        writer.WriteUInt16(0);
        writer.WriteUInt16(1);
        writer.WriteByte(payload);
        return writer.ToArray();
    }

    /// <summary>构造测试用 Mux V1 不可靠数据报。</summary>
    static byte[] EncodeUnreliable(ushort seq, byte payload)
    {
        var writer = new NetBufferWriter(10);
        writer.WriteByte(1);
        writer.WriteByte((byte)NetChannel.CommandUnreliableRedundant);
        writer.WriteByte(0);
        writer.WriteUInt16(seq);
        writer.WriteUInt16(0);
        writer.WriteUInt16(1);
        writer.WriteByte(payload);
        return writer.ToArray();
    }

    static int[] DrainPayloads(ChannelMuxTransport mux)
    {
        var values = new List<int>();
        while (mux.TryReceive(out NetPacket packet))
            values.Add(packet.Payload[0]);
        return values.ToArray();
    }

    /// <summary>成对内存数据报，可丢弃下一次客户端发送并回灌旧包。</summary>
    sealed class DuplexLink : System.IDisposable
    {
        public readonly MemoryTransport Server = new();
        public readonly MemoryTransport Client = new();
        public readonly List<byte[]> LastServerDatagrams = new();
        public readonly Queue<byte[]> ClientToServer = new();
        public readonly List<byte[]> HeldClientSends = new();
        public int DropNextClientSends;
        public bool HoldClientSends;

        public DuplexLink()
        {
            Server.Peer = Client;
            Client.Peer = Server;
            Client.OnSend = OnClientSend;
            Server.OnSend = OnServerSend;
            Server.IsServerRole = true;
        }

        public void InjectToClient(byte[] datagram) => Client.Inbox.Enqueue(datagram);

        /// <summary>按指定顺序把暂存的客户端数据报交给服务端。</summary>
        public void ReleaseHeldClientSends(params int[] order)
        {
            for (int i = 0; i < order.Length; i++)
                Server.Inbox.Enqueue(HeldClientSends[order[i]]);
            HeldClientSends.Clear();
            HoldClientSends = false;
        }

        void OnClientSend(byte[] datagram)
        {
            if (DropNextClientSends > 0)
            {
                DropNextClientSends--;
                return;
            }

            if (HoldClientSends)
            {
                HeldClientSends.Add(datagram);
                return;
            }

            ClientToServer.Enqueue(datagram);
            Server.Inbox.Enqueue(datagram);
        }

        void OnServerSend(byte[] datagram)
        {
            LastServerDatagrams.Add(datagram);
            Client.Inbox.Enqueue(datagram);
        }

        public void Dispose()
        {
            Client.Dispose();
            Server.Dispose();
        }
    }

    /// <summary>测试用点对点 Transport；连接 Id 固定为 1。</summary>
    sealed class MemoryTransport : INetTransport
    {
        static readonly NetConnectionId PeerId = new(1);
        readonly List<NetConnectionId> _connections = new();

        public MemoryTransport Peer;
        public readonly Queue<byte[]> Inbox = new();
        public System.Action<byte[]> OnSend;
        public bool IsServerRole;

        public bool IsRunning { get; private set; }
        public bool IsServer => IsServerRole;
        public NetEndpoint? LocalEndpoint { get; private set; }
        public IReadOnlyList<NetConnectionId> Connections => _connections;
        public NetMetricsSnapshot Metrics => default;

        public void StartServer(NetEndpoint endpoint)
        {
            IsRunning = true;
            LocalEndpoint = endpoint;
            _connections.Add(PeerId);
        }

        public void StartClient(NetEndpoint endpoint)
        {
            IsRunning = true;
            LocalEndpoint = endpoint;
            _connections.Add(PeerId);
        }

        public void Poll()
        {
        }

        public void Send(NetConnectionId connectionId, NetChannel channel, byte[] payload) =>
            OnSend?.Invoke(payload);

        public bool TryReceive(out NetPacket packet)
        {
            if (Inbox.Count == 0)
            {
                packet = default;
                return false;
            }

            packet = new NetPacket(PeerId, NetChannel.Unspecified, Inbox.Dequeue());
            return true;
        }

        public void Disconnect(NetConnectionId connectionId, DisconnectReason reason)
        {
        }

        public void Dispose() => IsRunning = false;
    }
}
