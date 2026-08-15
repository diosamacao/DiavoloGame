using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

/// <summary>
/// UDP 复制传输：只收发字节。Host Bind、Client Connect；权威广播已登记客机。
/// </summary>
public sealed class UdpReplicationTransport : IReplicationTransport, IDisposable
{
    readonly List<IPEndPoint> _clients = new();
    readonly Queue<QueuedAuthority> _authorityReady = new();
    readonly Queue<byte[]> _clientReady = new();
    UdpClient _udp;
    IPEndPoint _authorityEndPoint;
    bool _disposed;

    /// <summary>本机绑定端口；未绑定为 0。</summary>
    public int BoundPort =>
        _udp?.Client?.LocalEndPoint is IPEndPoint endPoint ? endPoint.Port : 0;

    /// <summary>本机端点；未绑定为空。</summary>
    public IPEndPoint LocalEndPoint => _udp?.Client?.LocalEndPoint as IPEndPoint;

    /// <summary>已登记的下行客机数。</summary>
    public int ClientCount => _clients.Count;

    /// <summary>权威绑定端口；port=0 使用系统临时端口（单测）。</summary>
    public void Bind(int port)
    {
        EnsureNotDisposed();
        DisposeSocket();
        _authorityEndPoint = null;
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, port < 0 ? 0 : port));
        _udp.Client.Blocking = false;
    }

    /// <summary>客机连接权威地址；本机用临时端口收包。</summary>
    public void Connect(string host, int port)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("权威地址不能为空。", nameof(host));
        if (port <= 0 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));

        EnsureNotDisposed();
        DisposeSocket();
        _authorityEndPoint = new IPEndPoint(IPAddress.Parse(host), port);
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
        _udp.Client.Blocking = false;
    }

    /// <summary>权威登记一名已入房客机，供 SendAuthorityToClients 广播。</summary>
    public void AddClient(IPEndPoint endpoint)
    {
        if (endpoint == null)
            throw new ArgumentNullException(nameof(endpoint));
        if (IndexOfClient(endpoint) < 0)
            _clients.Add(CloneEndPoint(endpoint));
    }

    /// <summary>权威移除客机端点。</summary>
    public void RemoveClient(IPEndPoint endpoint)
    {
        int index = IndexOfClient(endpoint);
        if (index >= 0)
            _clients.RemoveAt(index);
    }

    /// <summary>向指定端点发一包；JoinAccept 在 AddClient 之前使用。</summary>
    public void SendTo(IPEndPoint endpoint, byte[] payload)
    {
        if (endpoint == null)
            throw new ArgumentNullException(nameof(endpoint));
        SendRaw(payload, endpoint);
    }

    /// <inheritdoc />
    public void SendClientToAuthority(byte[] payload)
    {
        if (_authorityEndPoint == null)
            throw new InvalidOperationException("尚未 Connect，不能上行。");
        SendRaw(payload, _authorityEndPoint);
    }

    /// <inheritdoc />
    public void SendAuthorityToClients(byte[] payload)
    {
        for (int i = 0; i < _clients.Count; i++)
            SendRaw(payload, _clients[i]);
    }

    /// <inheritdoc />
    public void Pump()
    {
        if (_udp == null)
            return;

        while (TryReceive(out byte[] data, out IPEndPoint from))
        {
            if (_authorityEndPoint != null)
                _clientReady.Enqueue(data);
            else
                _authorityReady.Enqueue(new QueuedAuthority(data, from));
        }
    }

    /// <inheritdoc />
    public bool TryDequeueAuthority(out byte[] payload)
    {
        if (_authorityReady.Count == 0)
        {
            payload = null;
            return false;
        }

        payload = _authorityReady.Dequeue().Payload;
        return true;
    }

    /// <summary>权威取出上行包及发送端；Join 需要端点。</summary>
    public bool TryDequeueAuthorityFrom(out byte[] payload, out IPEndPoint from)
    {
        if (_authorityReady.Count == 0)
        {
            payload = null;
            from = null;
            return false;
        }

        QueuedAuthority queued = _authorityReady.Dequeue();
        payload = queued.Payload;
        from = queued.From;
        return true;
    }

    /// <inheritdoc />
    public bool TryDequeueClient(out byte[] payload)
    {
        if (_clientReady.Count == 0)
        {
            payload = null;
            return false;
        }

        payload = _clientReady.Dequeue();
        return true;
    }

    /// <summary>关闭套接字并清空队列。</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        DisposeSocket();
        _clients.Clear();
        _authorityReady.Clear();
        _clientReady.Clear();
    }

    void SendRaw(byte[] payload, IPEndPoint endpoint)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));
        if (_udp == null)
            throw new InvalidOperationException("UDP 套接字未绑定。");

        _udp.Send(payload, payload.Length, endpoint);
    }

    bool TryReceive(out byte[] data, out IPEndPoint from)
    {
        data = null;
        from = null;
        if (_udp == null || _udp.Available <= 0)
            return false;

        try
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] received = _udp.Receive(ref remote);
            if (received == null || received.Length == 0)
                return false;

            data = new byte[received.Length];
            Buffer.BlockCopy(received, 0, data, 0, received.Length);
            from = CloneEndPoint(remote);
            return true;
        }
        catch (SocketException ex) when (
            ex.SocketErrorCode == SocketError.WouldBlock
            || ex.SocketErrorCode == SocketError.TimedOut)
        {
            return false;
        }
    }

    int IndexOfClient(IPEndPoint endpoint)
    {
        if (endpoint == null)
            return -1;
        for (int i = 0; i < _clients.Count; i++)
        {
            if (_clients[i].Equals(endpoint))
                return i;
        }

        return -1;
    }

    static IPEndPoint CloneEndPoint(IPEndPoint endpoint) =>
        new IPEndPoint(endpoint.Address, endpoint.Port);

    void DisposeSocket()
    {
        if (_udp == null)
            return;
        try
        {
            _udp.Close();
        }
        catch (SocketException)
        {
        }

        _udp = null;
    }

    void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UdpReplicationTransport));
    }

    readonly struct QueuedAuthority
    {
        public QueuedAuthority(byte[] payload, IPEndPoint from)
        {
            Payload = payload;
            From = from;
        }

        public byte[] Payload { get; }
        public IPEndPoint From { get; }
    }
}
