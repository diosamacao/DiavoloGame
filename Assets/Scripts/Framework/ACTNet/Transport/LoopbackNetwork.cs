using System;
using System.Collections.Generic;

/// <summary>为一个服务端和多个客户端提供确定性时钟与内存路由的 Loopback 网络。</summary>
public sealed class LoopbackNetwork
{
    readonly Dictionary<NetConnectionId, LoopbackTransport> _clientByServerConnection = new();
    readonly Dictionary<LoopbackTransport, NetConnectionId> _serverConnectionByClient = new();
    readonly List<ScheduledDelivery> _pending = new();
    LoopbackTransport _server;
    long _nowMs;
    int _latencyMs;
    int _nextServerConnectionValue = 1;

    /// <summary>当前单向模拟延迟毫秒。</summary>
    public int LatencyMs => _latencyMs;

    /// <summary>设置单向延迟；负值钳为 0。</summary>
    public void SetLatencyMs(int latencyMs) => _latencyMs = Math.Max(0, latencyMs);

    /// <summary>推进确定性模拟时钟；随后由任一 Transport.Poll 交付到期包。</summary>
    public void AdvanceTimeMs(int deltaMs)
    {
        if (deltaMs < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaMs));
        _nowMs += deltaMs;
    }

    /// <summary>注册唯一 Loopback 服务端。</summary>
    internal void RegisterServer(LoopbackTransport server)
    {
        if (_server != null && !ReferenceEquals(_server, server))
            throw new InvalidOperationException("LoopbackNetwork 只允许一个服务端。");
        _server = server ?? throw new ArgumentNullException(nameof(server));
    }

    /// <summary>连接一个客户端并返回服务端作用域内的 ConnectionId。</summary>
    internal NetConnectionId RegisterClient(LoopbackTransport client)
    {
        if (_server == null)
            throw new InvalidOperationException("Loopback 服务端尚未启动。");
        if (_serverConnectionByClient.TryGetValue(client, out NetConnectionId existing))
            return existing;

        var serverConnection = new NetConnectionId(_nextServerConnectionValue++);
        _serverConnectionByClient.Add(client, serverConnection);
        _clientByServerConnection.Add(serverConnection, client);
        _server.AddRemoteConnection(serverConnection);
        return serverConnection;
    }

    /// <summary>按发送方本地 ConnectionId 路由并复制一条数据包。</summary>
    internal void Route(
        LoopbackTransport sender,
        NetConnectionId connectionId,
        NetChannel channel,
        byte[] payload)
    {
        if (sender == null)
            throw new ArgumentNullException(nameof(sender));
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));

        LoopbackTransport target;
        NetConnectionId targetConnection;
        if (sender.IsServer)
        {
            if (!_clientByServerConnection.TryGetValue(connectionId, out target))
                throw new InvalidOperationException($"Loopback 服务端连接不存在：{connectionId}。");
            targetConnection = LoopbackTransport.ClientServerConnection;
        }
        else
        {
            if (connectionId != LoopbackTransport.ClientServerConnection
                || !_serverConnectionByClient.TryGetValue(sender, out targetConnection))
            {
                throw new InvalidOperationException($"Loopback 客户端连接不存在：{connectionId}。");
            }

            target = _server;
        }

        var copy = new byte[payload.Length];
        Buffer.BlockCopy(payload, 0, copy, 0, payload.Length);
        _pending.Add(new ScheduledDelivery(
            _nowMs + _latencyMs,
            target,
            new NetPacket(targetConnection, channel, copy)));
    }

    /// <summary>交付全部到期数据包；相同时间按发送顺序稳定交付。</summary>
    internal void DeliverDue()
    {
        for (int i = 0; i < _pending.Count;)
        {
            ScheduledDelivery delivery = _pending[i];
            if (delivery.DeliverAtMs > _nowMs)
            {
                i++;
                continue;
            }

            _pending.RemoveAt(i);
            delivery.Target.EnqueueReceived(delivery.Packet);
        }
    }

    /// <summary>断开连接并同步移除服务端与客户端两侧状态。</summary>
    internal void Disconnect(LoopbackTransport sender, NetConnectionId connectionId)
    {
        if (sender.IsServer)
        {
            if (!_clientByServerConnection.TryGetValue(connectionId, out LoopbackTransport client))
                return;
            RemovePair(connectionId, client);
            return;
        }

        if (connectionId != LoopbackTransport.ClientServerConnection
            || !_serverConnectionByClient.TryGetValue(sender, out NetConnectionId serverConnection))
        {
            return;
        }

        RemovePair(serverConnection, sender);
    }

    /// <summary>Transport 销毁时注销其服务端或客户端角色。</summary>
    internal void Unregister(LoopbackTransport transport)
    {
        if (ReferenceEquals(_server, transport))
        {
            var connections = new List<NetConnectionId>(_clientByServerConnection.Keys);
            for (int i = 0; i < connections.Count; i++)
            {
                NetConnectionId connection = connections[i];
                RemovePair(connection, _clientByServerConnection[connection]);
            }

            _server = null;
        }
        else if (_serverConnectionByClient.TryGetValue(transport, out NetConnectionId connection))
        {
            RemovePair(connection, transport);
        }

        _pending.RemoveAll(delivery => ReferenceEquals(delivery.Target, transport));
    }

    /// <summary>删除一对连接，并清除所有尚未交付的数据包。</summary>
    void RemovePair(NetConnectionId serverConnection, LoopbackTransport client)
    {
        _clientByServerConnection.Remove(serverConnection);
        _serverConnectionByClient.Remove(client);
        _server?.RemoveRemoteConnection(serverConnection);
        client.RemoveRemoteConnection(LoopbackTransport.ClientServerConnection);
        _pending.RemoveAll(delivery =>
            (ReferenceEquals(delivery.Target, _server)
                && delivery.Packet.ConnectionId == serverConnection)
            || (ReferenceEquals(delivery.Target, client)
                && delivery.Packet.ConnectionId == LoopbackTransport.ClientServerConnection));
    }

    readonly struct ScheduledDelivery
    {
        public ScheduledDelivery(long deliverAtMs, LoopbackTransport target, NetPacket packet)
        {
            DeliverAtMs = deliverAtMs;
            Target = target;
            Packet = packet;
        }

        public long DeliverAtMs { get; }
        public LoopbackTransport Target { get; }
        public NetPacket Packet { get; }
    }
}
