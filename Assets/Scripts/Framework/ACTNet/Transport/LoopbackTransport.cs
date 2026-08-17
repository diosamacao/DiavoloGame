using System;
using System.Collections.Generic;

/// <summary>通过 LoopbackNetwork 支持一服多客、确定性延迟和独立收件箱的内存 Transport。</summary>
public sealed class LoopbackTransport : INetTransport
{
    internal static readonly NetConnectionId ClientServerConnection = new(1);

    readonly LoopbackNetwork _network;
    readonly List<NetConnectionId> _connections = new();
    readonly Queue<NetPacket> _received = new();
    long _bytesSent;
    long _bytesReceived;
    long _packetsSent;
    long _packetsReceived;
    bool _disposed;

    /// <summary>创建绑定到指定内存网络的 Transport 端点。</summary>
    public LoopbackTransport(LoopbackNetwork network)
    {
        _network = network ?? throw new ArgumentNullException(nameof(network));
    }

    /// <inheritdoc />
    public bool IsRunning { get; private set; }

    /// <inheritdoc />
    public bool IsServer { get; private set; }

    /// <inheritdoc />
    public NetEndpoint? LocalEndpoint { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<NetConnectionId> Connections => _connections;

    /// <inheritdoc />
    public NetMetricsSnapshot Metrics => new(
        _connections.Count,
        _bytesSent,
        _bytesReceived,
        _packetsSent,
        _packetsReceived,
        packetsDropped: 0,
        rttMs: -1,
        jitterMs: _network.LatencyMs);

    /// <inheritdoc />
    public void StartServer(NetEndpoint endpoint)
    {
        EnsureCanStart();
        IsServer = true;
        LocalEndpoint = endpoint;
        _network.RegisterServer(this);
        IsRunning = true;
    }

    /// <inheritdoc />
    public void StartClient(NetEndpoint endpoint)
    {
        EnsureCanStart();
        IsServer = false;
        LocalEndpoint = new NetEndpoint("loopback-client", 0, allowEphemeralPort: true);
        _network.RegisterClient(this);
        AddRemoteConnection(ClientServerConnection);
        IsRunning = true;
    }

    /// <inheritdoc />
    public void Poll()
    {
        if (IsRunning)
            _network.DeliverDue();
    }

    /// <inheritdoc />
    public void Send(NetConnectionId connectionId, NetChannel channel, byte[] payload)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));
        EnsureRunning();
        if (!_connections.Contains(connectionId))
            throw new InvalidOperationException($"Loopback 连接不存在：{connectionId}。");

        _network.Route(this, connectionId, channel, payload);
        _bytesSent += payload.Length;
        _packetsSent++;
    }

    /// <inheritdoc />
    public bool TryReceive(out NetPacket packet)
    {
        if (_received.Count == 0)
        {
            packet = default;
            return false;
        }

        packet = _received.Dequeue();
        return true;
    }

    /// <inheritdoc />
    public void Disconnect(NetConnectionId connectionId, DisconnectReason reason)
    {
        if (!IsRunning || !_connections.Contains(connectionId))
            return;
        _network.Disconnect(this, connectionId);
    }

    /// <summary>注销端点并清空连接与收件箱。</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _network.Unregister(this);
        IsRunning = false;
        _connections.Clear();
        _received.Clear();
        LocalEndpoint = null;
    }

    /// <summary>由 LoopbackNetwork 在本地作用域登记连接。</summary>
    internal void AddRemoteConnection(NetConnectionId connectionId)
    {
        if (!_connections.Contains(connectionId))
            _connections.Add(connectionId);
    }

    /// <summary>由 LoopbackNetwork 同步移除连接及其排队包。</summary>
    internal void RemoveRemoteConnection(NetConnectionId connectionId)
    {
        _connections.Remove(connectionId);
        int count = _received.Count;
        for (int i = 0; i < count; i++)
        {
            NetPacket packet = _received.Dequeue();
            if (packet.ConnectionId != connectionId)
                _received.Enqueue(packet);
        }
    }

    /// <summary>由 LoopbackNetwork 交付已经复制完成的数据包。</summary>
    internal void EnqueueReceived(NetPacket packet)
    {
        if (!IsRunning || !_connections.Contains(packet.ConnectionId))
            return;

        _received.Enqueue(packet);
        _bytesReceived += packet.Payload.Length;
        _packetsReceived++;
    }

    void EnsureCanStart()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LoopbackTransport));
        if (IsRunning)
            throw new InvalidOperationException("Transport 已启动。");
    }

    void EnsureRunning()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LoopbackTransport));
        if (!IsRunning)
            throw new InvalidOperationException("Transport 尚未启动。");
    }
}
