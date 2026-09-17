using System;
using System.Collections.Generic;

/// <summary>
/// 在现有数据报 Transport 上补通道头：Control/Event 可靠有序，Snapshot/Command 不可靠交付。
/// Snapshot 通道名保留，但 Tick 去重与丢旧归 ReplicationClient，允许同 Tick 互补 batch 乱序到达。
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
    const int MaxDelivered = 256;

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
    bool _hasClock;
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

    /// <summary>累计因可靠窗口已满而在发送前拒绝的次数。</summary>
    public int ReliableBackpressureRejected { get; private set; }

    /// <summary>配置的数据报 MTU。</summary>
    public int MaxDatagramBytes => _maxDatagramBytes;

    /// <summary>扣除 Mux 头后允许的 Session payload。</summary>
    public int MaxPayloadBytes => TransportMtuGate.MaxPayloadBytes(_maxDatagramBytes);

    /// <inheritdoc />
    public bool IsRunning => _inner.IsRunning;

    /// <inheritdoc />
    public bool IsServer => _inner.IsServer;

    /// <inheritdoc />
    public NetEndpoint? LocalEndpoint => _inner.LocalEndpoint;

    /// <inheritdoc />
    public IReadOnlyList<NetConnectionId> Connections => _inner.Connections;

    /// <inheritdoc />
    public NetMetricsSnapshot Metrics
    {
        get
        {
            NetMetricsSnapshot inner = _inner.Metrics;
            // Inner 已统计真实数据报；Mux 计数只用于协议内部诊断，不能再次叠加造成双计数。
            return new NetMetricsSnapshot(
                _inner.Connections.Count,
                inner.BytesSent,
                inner.BytesReceived,
                inner.PacketsSent,
                inner.PacketsReceived,
                inner.PacketsDropped + _packetsDropped,
                _rttMs,
                _jitterMs);
        }
    }

    /// <summary>推进可靠重传时钟；Session.Poll 必须先调用。</summary>
    public void AdvanceClock(long nowMs)
    {
        _nowMs = nowMs < 0 ? 0 : nowMs;
        _hasClock = true;
    }

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
        // 可靠窗口必须在分配序列与底层发送之前背压，绝不能丢弃最旧未确认包。
        if (reliable && state.Unacked.Count >= MaxUnacked)
        {
            ReliableBackpressureRejected++;
            throw new InvalidOperationException($"ChannelMux 可靠发送窗口已满：{MaxUnacked}。");
        }
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

        try
        {
            _inner.Send(connectionId, channel, datagram);
        }
        catch
        {
            // 底层拒绝时回滚尚未进入可靠窗口的序列，避免接收端永久等待空洞。
            if (reliable)
                state.NextReliableSend--;
            else
                state.NextUnreliableSend--;
            throw;
        }
        _bytesSent += datagram.Length;
        _packetsSent++;
        if (!reliable)
            return;

        // Session 可能在首次 Poll 前发送握手；-1 表示尚无可比较时钟，避免 Unix 毫秒转 int 溢出。
        state.Unacked.Add(new PendingReliable(
            seq,
            channel,
            datagram,
            _hasClock ? _nowMs : -1));
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
            int distance = SeqCompare(seq, state.NextReliableRecv);
            if (distance < 0)
            {
                // 已交付重传仍需 ACK，帮助发送端结束旧包。
                SendAck(packet.ConnectionId, channel, seq);
                return;
            }
            if (distance >= MaxUnacked)
            {
                // 超出接收窗口的未来包既不缓存也不 ACK，否则发送端会永久跳过缺口。
                _packetsDropped++;
                return;
            }
            if (distance == 0 && _delivered.Count >= MaxDelivered)
            {
                // 不 ACK 尚未交付的可靠包；发送端保留旧包并在消费恢复后重传。
                _packetsDropped++;
                return;
            }

            SendAck(packet.ConnectionId, channel, seq);
            if (distance > 0)
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

        if (_delivered.Count >= MaxDelivered)
        {
            _packetsDropped++;
            return;
        }

        // 不可靠包一律交付；Snapshot 的 batch/tick 语义不等于数据报发送序号。
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

            long sentAtMs = state.Unacked[i].SentAtMs;
            if (_hasClock && sentAtMs >= 0 && _nowMs >= sentAtMs)
            {
                long elapsed = _nowMs - sentAtMs;
                ObserveRtt(elapsed > int.MaxValue ? int.MaxValue : (int)elapsed);
            }
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
                if (pending.SentAtMs < 0)
                {
                    // 首次获得时钟只建立重传基线，不把启动前握手误判为超时。
                    unacked[i] = new PendingReliable(
                        pending.Seq,
                        pending.Channel,
                        pending.Datagram,
                        _nowMs);
                    continue;
                }
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

    sealed class ConnectionState
    {
        public ushort NextReliableSend;
        public ushort NextReliableRecv;
        public ushort LastReliableRecv;
        public ushort NextUnreliableSend;
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
