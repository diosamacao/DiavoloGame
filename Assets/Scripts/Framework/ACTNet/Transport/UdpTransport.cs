using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

/// <summary>多连接 UDP Adapter；连接 Id 仅在本 Transport 实例内稳定。</summary>
public sealed class UdpTransport : INetTransport
{
    static readonly NetConnectionId ClientServerConnection = new(1);

    readonly List<NetConnectionId> _connections = new();
    readonly Dictionary<NetConnectionId, IPEndPoint> _remoteByConnection = new();
    readonly Dictionary<string, NetConnectionId> _connectionByRemote = new(StringComparer.Ordinal);
    readonly Queue<NetPacket> _received = new();
    UdpClient _udp;
    IPEndPoint _clientServerEndPoint;
    int _nextServerConnectionValue = 1;
    long _bytesSent;
    long _bytesReceived;
    long _packetsSent;
    long _packetsReceived;
    bool _disposed;

    /// <inheritdoc />
    public bool IsRunning => _udp != null;

    /// <inheritdoc />
    public bool IsServer { get; private set; }

    /// <inheritdoc />
    public NetEndpoint? LocalEndpoint
    {
        get
        {
            if (_udp?.Client?.LocalEndPoint is not IPEndPoint endpoint)
                return null;
            return new NetEndpoint(endpoint.Address.ToString(), endpoint.Port, allowEphemeralPort: true);
        }
    }

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
        jitterMs: -1);

    /// <inheritdoc />
    public void StartServer(NetEndpoint endpoint)
    {
        EnsureCanStart();
        IPAddress bindAddress = ResolveBindAddress(endpoint.Host);
        _udp = new UdpClient(new IPEndPoint(bindAddress, endpoint.Port));
        _udp.Client.Blocking = false;
        IsServer = true;
    }

    /// <inheritdoc />
    public void StartClient(NetEndpoint endpoint)
    {
        EnsureCanStart();
        _clientServerEndPoint = new IPEndPoint(ResolveRemoteAddress(endpoint.Host), endpoint.Port);
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
        _udp.Client.Blocking = false;
        IsServer = false;
        AddConnection(ClientServerConnection, _clientServerEndPoint);
    }

    /// <inheritdoc />
    public void Poll()
    {
        if (_udp == null)
            return;

        while (TryReceiveDatagram(out byte[] payload, out IPEndPoint remote))
        {
            NetConnectionId connectionId;
            if (IsServer)
            {
                connectionId = GetOrCreateServerConnection(remote);
            }
            else
            {
                if (_clientServerEndPoint == null || !_clientServerEndPoint.Equals(remote))
                    continue;
                connectionId = ClientServerConnection;
            }

            _bytesReceived += payload.Length;
            _packetsReceived++;
            _received.Enqueue(new NetPacket(connectionId, NetChannel.Unspecified, payload));
        }
    }

    /// <inheritdoc />
    public void Send(NetConnectionId connectionId, NetChannel channel, byte[] payload)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));
        EnsureRunning();
        if (!_remoteByConnection.TryGetValue(connectionId, out IPEndPoint remote))
            throw new InvalidOperationException($"连接不存在：{connectionId}。");

        // 通道头由 ChannelMuxTransport 写入 payload；此处只发数据报。
        _udp.Send(payload, payload.Length, remote);
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
        if (!_remoteByConnection.TryGetValue(connectionId, out IPEndPoint remote))
            return;

        _remoteByConnection.Remove(connectionId);
        _connectionByRemote.Remove(RemoteKey(remote));
        _connections.Remove(connectionId);
        RemoveQueuedPackets(connectionId);
        if (!IsServer && connectionId == ClientServerConnection)
            _clientServerEndPoint = null;
    }

    /// <summary>关闭 Socket 并清空所有本地连接与收件箱。</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            _udp?.Close();
        }
        catch (SocketException)
        {
        }

        _udp = null;
        _clientServerEndPoint = null;
        _connections.Clear();
        _remoteByConnection.Clear();
        _connectionByRemote.Clear();
        _received.Clear();
    }

    /// <summary>服务端首次收到某端点的数据报时分配稳定连接 Id。</summary>
    NetConnectionId GetOrCreateServerConnection(IPEndPoint remote)
    {
        string key = RemoteKey(remote);
        if (_connectionByRemote.TryGetValue(key, out NetConnectionId existing))
            return existing;

        var created = new NetConnectionId(_nextServerConnectionValue++);
        AddConnection(created, remote);
        return created;
    }

    /// <summary>登记本地连接及其不可变远端副本。</summary>
    void AddConnection(NetConnectionId connectionId, IPEndPoint remote)
    {
        var copy = new IPEndPoint(remote.Address, remote.Port);
        _connections.Add(connectionId);
        _remoteByConnection.Add(connectionId, copy);
        _connectionByRemote[RemoteKey(copy)] = connectionId;
    }

    /// <summary>读取一条非阻塞 UDP 数据报并复制端点。</summary>
    bool TryReceiveDatagram(out byte[] payload, out IPEndPoint remote)
    {
        payload = null;
        remote = null;
        if (_udp == null || _udp.Available <= 0)
            return false;

        try
        {
            IPEndPoint sender = new(IPAddress.Any, 0);
            byte[] received = _udp.Receive(ref sender);
            if (received == null || received.Length == 0)
                return false;

            payload = received;
            remote = new IPEndPoint(sender.Address, sender.Port);
            return true;
        }
        catch (SocketException ex) when (
            ex.SocketErrorCode == SocketError.WouldBlock
            || ex.SocketErrorCode == SocketError.TimedOut)
        {
            return false;
        }
    }

    /// <summary>断开时移除已经排队但尚未交付的旧连接数据报。</summary>
    void RemoveQueuedPackets(NetConnectionId connectionId)
    {
        int count = _received.Count;
        for (int i = 0; i < count; i++)
        {
            NetPacket packet = _received.Dequeue();
            if (packet.ConnectionId != connectionId)
                _received.Enqueue(packet);
        }
    }

    static string RemoteKey(IPEndPoint endpoint) => endpoint.ToString();

    static IPAddress ResolveBindAddress(string host)
    {
        if (host == "0.0.0.0" || host == "*")
            return IPAddress.Any;
        return ResolveRemoteAddress(host);
    }

    static IPAddress ResolveRemoteAddress(string host)
    {
        if (IPAddress.TryParse(host, out IPAddress parsed))
            return parsed;

        IPAddress[] addresses = Dns.GetHostAddresses(host);
        for (int i = 0; i < addresses.Length; i++)
        {
            if (addresses[i].AddressFamily == AddressFamily.InterNetwork)
                return addresses[i];
        }

        throw new InvalidOperationException($"无法解析 IPv4 主机：{host}。");
    }

    void EnsureCanStart()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UdpTransport));
        if (_udp != null)
            throw new InvalidOperationException("Transport 已启动。");
    }

    void EnsureRunning()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UdpTransport));
        if (_udp == null)
            throw new InvalidOperationException("Transport 尚未启动。");
    }
}
