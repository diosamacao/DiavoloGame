using System;
using System.Collections.Generic;

/// <summary>服务端 Session 状态机：版本校验、玩家预留、保活、超时和应用消息路由。Transport 经 ChannelMux 补可靠控制/事件。</summary>
public sealed class ServerSession : IDisposable
{
    readonly ChannelMuxTransport _transport;
    readonly SessionConfig _config;
    readonly ConnectionRegistry _connections = new();
    readonly PlayerRegistry _players;
    readonly Queue<SessionPlayerRequest> _joinRequests = new();
    readonly Queue<SessionApplicationPacket> _applicationPackets = new();
    readonly List<NetConnectionId> _connectionScratch = new();
    bool _disposed;

    /// <summary>创建并立即启动服务端 Transport 的 Session。</summary>
    public ServerSession(INetTransport transport, SessionConfig config, NetEndpoint endpoint)
    {
        _transport = ChannelMuxTransport.Wrap(transport ?? throw new ArgumentNullException(nameof(transport)));
        _config = config;
        _players = new PlayerRegistry(config.FirstPlayerId);
        _transport.StartServer(endpoint);
    }

    /// <summary>Gameplay 清理玩家实体时收到的断开通知。</summary>
    public event Action<SessionDisconnected> Disconnected;

    /// <summary>已预留或完成 Join 的远端连接数量。</summary>
    public int ConnectionCount => _connections.Count;

    /// <summary>底层 Transport 实际绑定端点。</summary>
    public NetEndpoint? LocalEndpoint => _transport.LocalEndpoint;

    /// <summary>轮询 Transport、处理 Session 控制消息并剔除超时连接。</summary>
    public void Poll(long nowMs)
    {
        EnsureNotDisposed();
        _transport.AdvanceClock(nowMs);
        _transport.Poll();
        while (_transport.TryReceive(out NetPacket packet))
        {
            try
            {
                HandlePacket(packet, nowMs);
            }
            catch (Exception)
            {
                DisconnectInternal(
                    packet.ConnectionId,
                    DisconnectReason.MalformedPacket,
                    notifyClient: false);
            }
        }

        DisconnectTimedOut(nowMs);
    }

    /// <summary>取出一个已通过版本与容量校验、等待 Gameplay 建实体的玩家请求。</summary>
    public bool TryDequeuePlayerRequest(out SessionPlayerRequest request)
    {
        if (_joinRequests.Count == 0)
        {
            request = default;
            return false;
        }

        request = _joinRequests.Dequeue();
        return true;
    }

    /// <summary>Gameplay 完成实体创建后发 JoinAccept 并开放应用消息。</summary>
    public void AcceptPlayer(
        NetConnectionId connectionId,
        NetEntityId entityId,
        NetEntityId authorityEntityId,
        NetTick authorityTick)
    {
        EnsureNotDisposed();
        if (!_connections.TryGetPlayer(connectionId, out NetPlayerId playerId)
            || _connections.IsJoined(connectionId))
        {
            throw new InvalidOperationException($"连接不处于待接纳状态：{connectionId}。");
        }

        var accept = new SessionJoinAccept(
            playerId,
            entityId,
            authorityEntityId,
            _config.ContentVersion,
            authorityTick);
        _transport.Send(
            connectionId,
            NetChannel.ControlReliableOrdered,
            SessionCodec.WriteJoinAccept(in accept));
        _connections.MarkJoined(connectionId);
    }

    /// <summary>Gameplay 无法创建实体时拒绝待接纳玩家。</summary>
    public void RejectPlayer(NetConnectionId connectionId, SessionRejectReason reason)
    {
        EnsureNotDisposed();
        if (!_connections.Contains(connectionId))
            return;
        SendJoinReject(connectionId, reason);
        _connections.Remove(connectionId, out NetPlayerId playerId);
        _players.Release(connectionId, out _);
        RemoveQueuedPackets(connectionId);
        Disconnected?.Invoke(new SessionDisconnected(
            connectionId,
            playerId,
            MapRejectReason(reason)));
    }

    /// <summary>取出一条已完成 Join 的应用消息。</summary>
    public bool TryDequeueApplication(out SessionApplicationPacket packet)
    {
        if (_applicationPackets.Count == 0)
        {
            packet = default;
            return false;
        }

        packet = _applicationPackets.Dequeue();
        return true;
    }

    /// <summary>向已完成 Join 的指定连接发送应用正文。</summary>
    public void SendApplication(
        NetConnectionId connectionId,
        byte messageType,
        NetChannel channel,
        byte[] payload)
    {
        EnsureNotDisposed();
        if (!_connections.IsJoined(connectionId))
            throw new InvalidOperationException($"连接尚未完成 Join：{connectionId}。");
        if (SessionCodec.IsControlMessage(messageType))
            throw new ArgumentException("应用消息不能占用 Session 控制类型。", nameof(messageType));

        _transport.Send(
            connectionId,
            channel,
            SessionCodec.WriteEnvelope(messageType, payload));
    }

    /// <summary>主动终止指定连接，并通知客户端与 Gameplay。</summary>
    public void Disconnect(NetConnectionId connectionId, DisconnectReason reason) =>
        DisconnectInternal(connectionId, reason, notifyClient: true);

    /// <summary>以 ServerEnded 通知并清空全部连接。</summary>
    public void Shutdown()
    {
        if (_disposed)
            return;
        _connections.CopyConnectionIds(_connectionScratch);
        for (int i = 0; i < _connectionScratch.Count; i++)
        {
            DisconnectInternal(
                _connectionScratch[i],
                DisconnectReason.ServerShutdown,
                notifyClient: true);
        }
    }

    /// <summary>关闭全部 Session 与底层 Transport。</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        Shutdown();
        _disposed = true;
        _transport.Dispose();
        _joinRequests.Clear();
        _applicationPackets.Clear();
    }

    /// <summary>按消息类型消费控制流，非控制消息只向已 Join 连接开放。</summary>
    void HandlePacket(NetPacket packet, long nowMs)
    {
        SessionCodec.ReadEnvelope(packet.Payload, out byte messageType, out byte[] body);
        if (messageType == (byte)SessionMessageKind.JoinRequest)
        {
            HandleJoinRequest(packet.ConnectionId, body, nowMs);
            return;
        }

        if (!_connections.Contains(packet.ConnectionId))
        {
            _transport.Disconnect(packet.ConnectionId, DisconnectReason.Unauthorized);
            return;
        }

        _connections.Touch(packet.ConnectionId, nowMs);
        if (messageType == (byte)SessionMessageKind.Heartbeat)
        {
            if (!_connections.IsJoined(packet.ConnectionId))
                return;
            SessionHeartbeat heartbeat = SessionCodec.ReadHeartbeat(body);
            var echo = new SessionHeartbeat(heartbeat.SendTimeMs, heartbeat.SendTimeMs);
            _transport.Send(
                packet.ConnectionId,
                NetChannel.ControlReliableOrdered,
                SessionCodec.WriteHeartbeat(in echo));
            return;
        }

        if (SessionCodec.IsControlMessage(messageType))
            return;
        if (_connections.IsJoined(packet.ConnectionId))
        {
            _applicationPackets.Enqueue(new SessionApplicationPacket(
                packet.ConnectionId,
                messageType,
                body));
        }
    }

    /// <summary>校验 Join 版本和容量，然后预留 PlayerId 给 Gameplay。</summary>
    void HandleJoinRequest(NetConnectionId connectionId, byte[] body, long nowMs)
    {
        if (_connections.Contains(connectionId))
        {
            _connections.Touch(connectionId, nowMs);
            return;
        }

        SessionJoinRequest request = SessionCodec.ReadJoinRequest(body);
        if (request.ProtocolVersion != _config.ProtocolVersion
            || request.ContentVersion != _config.ContentVersion)
        {
            SendJoinReject(connectionId, SessionRejectReason.VersionMismatch);
            return;
        }

        if (_config.GameplayFingerprint.IsValid
            && request.GameplayFingerprint != _config.GameplayFingerprint)
        {
            SendJoinReject(connectionId, SessionRejectReason.ContentMismatch);
            return;
        }

        if (_connections.Count >= _config.MaxRemotePlayers)
        {
            SendJoinReject(connectionId, SessionRejectReason.ServerFull);
            return;
        }

        NetPlayerId playerId = _players.Reserve(connectionId);
        _connections.Add(connectionId, playerId, nowMs);
        _joinRequests.Enqueue(new SessionPlayerRequest(connectionId, playerId));
    }

    /// <summary>扫描每连接活动时刻，只断开达到超时边界的连接。</summary>
    void DisconnectTimedOut(long nowMs)
    {
        _connections.CopyConnectionIds(_connectionScratch);
        for (int i = 0; i < _connectionScratch.Count; i++)
        {
            NetConnectionId connectionId = _connectionScratch[i];
            if (_connections.IsTimedOut(connectionId, nowMs, _config.IdleTimeoutMs))
            {
                DisconnectInternal(
                    connectionId,
                    DisconnectReason.Timeout,
                    notifyClient: true);
            }
        }
    }

    /// <summary>统一移除 Registry、发送 Kick、关闭 Transport 连接并通知 Gameplay。</summary>
    void DisconnectInternal(
        NetConnectionId connectionId,
        DisconnectReason reason,
        bool notifyClient)
    {
        bool hadConnection = _connections.Remove(connectionId, out NetPlayerId playerId);
        _players.Release(connectionId, out _);
        RemoveQueuedPackets(connectionId);

        if (notifyClient && hadConnection)
        {
            SessionKickReason kickReason = reason == DisconnectReason.Timeout
                ? SessionKickReason.IdleTimeout
                : SessionKickReason.ServerEnded;
            try
            {
                _transport.Send(
                    connectionId,
                    NetChannel.ControlReliableOrdered,
                    SessionCodec.WriteKick(kickReason));
            }
            catch (Exception)
            {
            }
        }

        // Kick/Reject 必须先到达对端；客户端处理控制包后负责关闭 Transport 连接。
        // 无通知的损坏连接可立即关闭，避免继续向 Session 注入包。
        if (!notifyClient || !hadConnection)
            _transport.Disconnect(connectionId, reason);
        if (hadConnection)
            Disconnected?.Invoke(new SessionDisconnected(connectionId, playerId, reason));
    }

    /// <summary>断开时清除尚未交给 Gameplay 的旧请求与应用包。</summary>
    void RemoveQueuedPackets(NetConnectionId connectionId)
    {
        int joins = _joinRequests.Count;
        for (int i = 0; i < joins; i++)
        {
            SessionPlayerRequest request = _joinRequests.Dequeue();
            if (request.ConnectionId != connectionId)
                _joinRequests.Enqueue(request);
        }

        int applications = _applicationPackets.Count;
        for (int i = 0; i < applications; i++)
        {
            SessionApplicationPacket packet = _applicationPackets.Dequeue();
            if (packet.ConnectionId != connectionId)
                _applicationPackets.Enqueue(packet);
        }
    }

    void SendJoinReject(NetConnectionId connectionId, SessionRejectReason reason) =>
        _transport.Send(
            connectionId,
            NetChannel.ControlReliableOrdered,
            SessionCodec.WriteJoinReject(reason));

    static DisconnectReason MapRejectReason(SessionRejectReason reason) =>
        reason == SessionRejectReason.VersionMismatch
            ? DisconnectReason.ProtocolMismatch
            : reason == SessionRejectReason.ServerFull
                ? DisconnectReason.ServerFull
                : DisconnectReason.InternalError;

    void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ServerSession));
    }
}
