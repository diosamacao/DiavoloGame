using System;
using System.Collections.Generic;

/// <summary>
/// 在现有数据报 Transport 上补通道头：Control/Event 可靠有序，Snapshot 丢旧，Command 原样交付。
/// W10 定案不换 LiteNetLib / Unity Transport，避免与预测提取同风险面换库。
/// </summary>
public sealed class ChannelMuxTransport : INetTransport
{
    const byte HeaderVersion = 1;
    const byte KindUnreliable = 0;
    const byte KindReliable = 1;
    const byte KindAck = 2;
    const int DefaultRetransmitMs = 50;
    const int MaxUnacked = 64;

    readonly INetTransport _inner;
    readonly int _maxDatagramBytes;
    readonly Dictionary<int, ConnectionState> _connections = new();
    readonly Queue<NetPacket> _delivered = new();
    long _nowMs;
    long _bytesSent;
    long _bytesReceived;
    long _packetsSent;
    long _packetsReceived;
    long _packetsDropped;
    int _rttMs = -1;
    int _jitterMs = -1;
    bool _disposed;

    /// <summary>包装底层 Transport；已是 Mux 则原样返回。</summary>
    public static ChannelMuxTransport Wrap(INetTransport transport, int maxDatagramBytes = TransportMtuGate.DefaultMaxDatagramBytes)
    {
        if (transport == null)
            throw new ArgumentNullException(nameof(transport));
        if (transport is ChannelMuxTransport mux)
            return mux;
        return new ChannelMuxTransport(transport, maxDatagramBytes);
    }

    ChannelMuxTransport(INetTransport inner, int maxDatagramBytes)
    {
        _inner = inner;
        _maxDatagramBytes = maxDatagramBytes;
    }

    /// <summary>累计因超 MTU 被拒绝的发送次数。</summary>
    public int OversizeRejected { get; private set; }

    /// <inheritdoc />
    public bool IsRunning => _inner.IsRunning;

    /// <inheritdoc />
    public bool IsServer => _inner.IsServer;

    /// <inheritdoc />
    public NetEndpoint? LocalEndpoint => _inner.LocalEndpoint;

    /// <inheritdoc />
    public IReadOnlyList<NetConnectionId> Connections => _inner.Connections;

    /// <inheritdoc />
    public NetMetricsSnapshot Metrics => new(
        _inner.Connections.Count,
        _bytesSent + _inner.Metrics.BytesSent,
        _bytesReceived + _inner.Metrics.BytesReceived,
        _packetsSent + _inner.Metrics.PacketsSent,
        _packetsReceived + _inner.Metrics.PacketsReceived,
        _packetsDropped,
        _rttMs,
        _jitterMs);

    /// <summary>推进可靠重传时钟；Session.Poll 必须先调用。</summary>
    public void AdvanceClock(long nowMs) => _nowMs = nowMs < 0 ? 0 : nowMs;

    /// <inheritdoc />
    public void StartServer(NetEndpoint endpoint) => _inner.StartServer(endpoint);

    /// <inheritdoc />
    public void StartClient(NetEndpoint endpoint) => _inner.StartClient(endpoint);

    /// <inheritdoc />
    public void Poll()
    {
        _inner.Poll();
        while (_inner.TryReceive(out NetPacket packet))
            HandleIncoming(in packet);
        RetransmitDue();
    }

    /// <inheritdoc />
    public void Send(NetConnectionId connectionId, NetChannel channel, byte[] payload)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));

        ConnectionState state = GetOrCreate(connectionId);
        bool reliable = IsReliable(channel);
        ushort seq = reliable ? state.NextReliableSend++ : state.NextUnreliableSend++;
        byte[] datagram = Encode(
            channel,
            reliable ? KindReliable : KindUnreliable,
            seq,
            state.LastReliableRecv,
            payload);
        if (!TransportMtuGate.TryAccept(datagram.Length, _maxDatagramBytes, out _))
        {
            OversizeRejected++;
            throw new InvalidOperationException(
                $"ChannelMux 拒绝超 MTU 发送：{datagram.Length}/{_maxDatagramBytes} channel={channel}。");
        }

        _inner.Send(connectionId, channel, datagram);
        _bytesSent += datagram.Length;
        _packetsSent++;
        if (!reliable)
            return;

        state.Unacked.Add(new PendingReliable(seq, channel, datagram, _nowMs));
        while (state.Unacked.Count > MaxUnacked)
            state.Unacked.RemoveAt(0);
    }

    /// <inheritdoc />
    public bool TryReceive(out NetPacket packet)
    {
        if (_delivered.Count == 0)
        {
            packet = default;
            return false;
        }

        packet = _delivered.Dequeue();
        return true;
    }

    /// <inheritdoc />
    public void Disconnect(NetConnectionId connectionId, DisconnectReason reason)
    {
        _connections.Remove(connectionId.Value);
        RemoveDelivered(connectionId);
        _inner.Disconnect(connectionId, reason);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _connections.Clear();
        _delivered.Clear();
        _inner.Dispose();
    }

    void HandleIncoming(in NetPacket packet)
    {
        if (!TryDecode(packet.Payload, out NetChannel channel, out byte kind, out ushort seq, out ushort ack, out byte[] payload))
        {
            _packetsDropped++;
            return;
        }

        _bytesReceived += packet.Payload.Length;
        _packetsReceived++;
        ConnectionState state = GetOrCreate(packet.ConnectionId);
        if (kind == KindAck)
        {
            Acknowledge(state, ack);
            return;
        }

        if (kind == KindReliable)
        {
            SendAck(packet.ConnectionId, channel, seq);
            if (SeqCompare(seq, state.NextReliableRecv) < 0)
                return;
            if (seq != state.NextReliableRecv)
            {
                if (!state.Hold.ContainsKey(seq))
                    state.Hold[seq] = new HeldPacket(channel, payload);
                return;
            }

            Deliver(packet.ConnectionId, channel, payload);
            state.LastReliableRecv = seq;
            state.NextReliableRecv++;
            DrainHold(packet.ConnectionId, state);
            return;
        }

        if (channel == NetChannel.SnapshotUnreliableSequenced)
        {
            if (state.HasSnapshotSeq && SeqCompare(seq, state.LastSnapshotSeq) <= 0)
            {
                _packetsDropped++;
                return;
            }

            if (state.HasSnapshotSeq)
            {
                int gap = SeqDelta(seq, state.LastSnapshotSeq) - 1;
                if (gap > 0)
                    _packetsDropped += gap;
            }

            state.HasSnapshotSeq = true;
            state.LastSnapshotSeq = seq;
        }

        Deliver(packet.ConnectionId, channel, payload);
    }

    void DrainHold(NetConnectionId connectionId, ConnectionState state)
    {
        while (state.Hold.TryGetValue(state.NextReliableRecv, out HeldPacket held))
        {
            state.Hold.Remove(state.NextReliableRecv);
            Deliver(connectionId, held.Channel, held.Payload);
            state.LastReliableRecv = state.NextReliableRecv;
            state.NextReliableRecv++;
        }
    }

    void Acknowledge(ConnectionState state, ushort ack)
    {
        for (int i = state.Unacked.Count - 1; i >= 0; i--)
        {
            if (state.Unacked[i].Seq != ack)
                continue;

            int rtt = (int)Math.Max(0L, _nowMs - state.Unacked[i].SentAtMs);
            ObserveRtt(rtt);
            state.Unacked.RemoveAt(i);
            return;
        }
    }

    void RetransmitDue()
    {
        foreach (KeyValuePair<int, ConnectionState> pair in _connections)
        {
            List<PendingReliable> unacked = pair.Value.Unacked;
            for (int i = 0; i < unacked.Count; i++)
            {
                PendingReliable pending = unacked[i];
                if (_nowMs - pending.SentAtMs < DefaultRetransmitMs)
                    continue;

                _inner.Send(new NetConnectionId(pair.Key), pending.Channel, pending.Datagram);
                _packetsSent++;
                _bytesSent += pending.Datagram.Length;
                unacked[i] = new PendingReliable(
                    pending.Seq,
                    pending.Channel,
                    pending.Datagram,
                    _nowMs);
            }
        }
    }

    void SendAck(NetConnectionId connectionId, NetChannel channel, ushort seq)
    {
        byte[] datagram = Encode(channel, KindAck, 0, seq, Array.Empty<byte>());
        _inner.Send(connectionId, channel, datagram);
        _packetsSent++;
        _bytesSent += datagram.Length;
    }

    void Deliver(NetConnectionId connectionId, NetChannel channel, byte[] payload)
    {
        _delivered.Enqueue(new NetPacket(connectionId, channel, payload));
    }

    void ObserveRtt(int rttMs)
    {
        if (_rttMs < 0)
        {
            _rttMs = rttMs;
            _jitterMs = 0;
            return;
        }

        int delta = rttMs - _rttMs;
        if (delta < 0)
            delta = -delta;
        _jitterMs += (delta - _jitterMs) >> 4;
        _rttMs = rttMs;
    }

    ConnectionState GetOrCreate(NetConnectionId connectionId)
    {
        if (_connections.TryGetValue(connectionId.Value, out ConnectionState existing))
            return existing;

        var created = new ConnectionState();
        _connections.Add(connectionId.Value, created);
        return created;
    }

    void RemoveDelivered(NetConnectionId connectionId)
    {
        int count = _delivered.Count;
        for (int i = 0; i < count; i++)
        {
            NetPacket packet = _delivered.Dequeue();
            if (packet.ConnectionId != connectionId)
                _delivered.Enqueue(packet);
        }
    }

    static bool IsReliable(NetChannel channel) =>
        channel == NetChannel.ControlReliableOrdered
        || channel == NetChannel.EventReliableOrdered;

    static byte[] Encode(NetChannel channel, byte kind, ushort seq, ushort ack, byte[] payload)
    {
        var writer = new NetBufferWriter(TransportMtuGate.HeaderBytes + payload.Length);
        writer.WriteByte(HeaderVersion);
        writer.WriteByte((byte)channel);
        writer.WriteByte(kind);
        writer.WriteUInt16(seq);
        writer.WriteUInt16(ack);
        writer.WriteUInt16((ushort)payload.Length);
        if (payload.Length > 0)
            writer.WriteBytes(payload, 0, payload.Length);
        return writer.ToArray();
    }

    static bool TryDecode(
        byte[] datagram,
        out NetChannel channel,
        out byte kind,
        out ushort seq,
        out ushort ack,
        out byte[] payload)
    {
        channel = NetChannel.Unspecified;
        kind = 0;
        seq = 0;
        ack = 0;
        payload = Array.Empty<byte>();
        if (datagram == null || datagram.Length < TransportMtuGate.HeaderBytes)
            return false;

        try
        {
            var reader = new NetBufferReader(datagram);
            if (reader.ReadByte() != HeaderVersion)
                return false;
            channel = (NetChannel)reader.ReadByte();
            kind = reader.ReadByte();
            seq = reader.ReadUInt16();
            ack = reader.ReadUInt16();
            int length = reader.ReadUInt16();
            payload = length == 0 ? Array.Empty<byte>() : reader.ReadBytes(length);
            reader.EnsureComplete();
            return kind <= KindAck;
        }
        catch (Exception)
        {
            return false;
        }
    }

    static int SeqCompare(ushort left, ushort right) => (short)(left - right);

    static int SeqDelta(ushort newer, ushort older) => (ushort)(newer - older);

    sealed class ConnectionState
    {
        public ushort NextReliableSend;
        public ushort NextReliableRecv;
        public ushort LastReliableRecv;
        public ushort NextUnreliableSend;
        public ushort LastSnapshotSeq;
        public bool HasSnapshotSeq;
        public readonly List<PendingReliable> Unacked = new();
        public readonly Dictionary<ushort, HeldPacket> Hold = new();
    }

    readonly struct PendingReliable
    {
        public PendingReliable(ushort seq, NetChannel channel, byte[] datagram, long sentAtMs)
        {
            Seq = seq;
            Channel = channel;
            Datagram = datagram;
            SentAtMs = sentAtMs;
        }

        public ushort Seq { get; }
        public NetChannel Channel { get; }
        public byte[] Datagram { get; }
        public long SentAtMs { get; }
    }

    readonly struct HeldPacket
    {
        public HeldPacket(NetChannel channel, byte[] payload)
        {
            Channel = channel;
            Payload = payload;
        }

        public NetChannel Channel { get; }
        public byte[] Payload { get; }
    }
}
