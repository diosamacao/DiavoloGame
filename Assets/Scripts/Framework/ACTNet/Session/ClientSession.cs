using System;
using System.Collections.Generic;

/// <summary>客户端 Session 状态。</summary>
public enum ClientSessionState : byte
{
    /// <summary>尚未启动。</summary>
    Stopped = 0,

    /// <summary>已发送 Join，等待服务端结果。</summary>
    Connecting = 1,

    /// <summary>Join 完成，可收发应用消息。</summary>
    Joined = 2,

    /// <summary>被拒绝、Kick、超时或主动结束。</summary>
    Ended = 3,
}

/// <summary>客户端 Session 状态机：Join、自动心跳、RTT、权威超时和应用消息路由。</summary>
public sealed class ClientSession : IDisposable
{
    readonly INetTransport _transport;
    readonly SessionConfig _config;
    readonly Queue<SessionApplicationPacket> _applicationPackets = new();
    NetConnectionId _serverConnection;
    long _lastAuthorityActivityMs;
    long _nextHeartbeatMs;
    bool _disposed;

    /// <summary>创建尚未启动的客户端 Session。</summary>
    public ClientSession(INetTransport transport, SessionConfig config)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _config = config;
        State = ClientSessionState.Stopped;
        RttMs = -1;
    }

    /// <summary>当前握手或结束状态。</summary>
    public ClientSessionState State { get; private set; }

    /// <summary>最近一次成功 Join 的身份与实体分配。</summary>
    public SessionJoinAccept JoinAccept { get; private set; }

    /// <summary>最近心跳回显计算出的往返毫秒。</summary>
    public int RttMs { get; private set; }

    /// <summary>Session 结束原因；运行中为 None。</summary>
    public DisconnectReason LastDisconnectReason { get; private set; }

    /// <summary>启动客户端 Transport 并发送 JoinRequest。</summary>
    public void Start(NetEndpoint endpoint, long nowMs)
    {
        EnsureNotDisposed();
        if (State != ClientSessionState.Stopped)
            throw new InvalidOperationException("ClientSession 已启动。");

        _transport.StartClient(endpoint);
        if (_transport.Connections.Count == 0)
            throw new InvalidOperationException("Transport 未建立服务端连接。");

        _serverConnection = _transport.Connections[0];
        _lastAuthorityActivityMs = nowMs;
        _nextHeartbeatMs = nowMs + _config.HeartbeatIntervalMs;
        var request = new SessionJoinRequest(
            _config.ContentVersion,
            _config.ProtocolVersion);
        _transport.Send(
            _serverConnection,
            NetChannel.ControlReliableOrdered,
            SessionCodec.WriteJoinRequest(in request));
        State = ClientSessionState.Connecting;
    }

    /// <summary>轮询控制消息、投递应用包、发送到期心跳并检测权威超时。</summary>
    public void Poll(long nowMs)
    {
        EnsureNotDisposed();
        if (State == ClientSessionState.Stopped || State == ClientSessionState.Ended)
            return;

        _transport.Poll();
        while (_transport.TryReceive(out NetPacket packet))
        {
            if (packet.ConnectionId != _serverConnection)
                continue;
            try
            {
                HandlePacket(packet, nowMs);
            }
            catch (Exception)
            {
                End(DisconnectReason.MalformedPacket);
                return;
            }
        }

        if (State == ClientSessionState.Joined && nowMs >= _nextHeartbeatMs)
        {
            var heartbeat = new SessionHeartbeat(nowMs, 0);
            _transport.Send(
                _serverConnection,
                NetChannel.ControlReliableOrdered,
                SessionCodec.WriteHeartbeat(in heartbeat));
            _nextHeartbeatMs = nowMs + _config.HeartbeatIntervalMs;
        }

        if (nowMs - _lastAuthorityActivityMs >= _config.IdleTimeoutMs)
            End(DisconnectReason.Timeout);
    }

    /// <summary>向权威 Session 发送应用正文。</summary>
    public void SendApplication(byte messageType, NetChannel channel, byte[] payload)
    {
        EnsureNotDisposed();
        if (State != ClientSessionState.Joined)
            throw new InvalidOperationException("ClientSession 尚未完成 Join。");
        if (SessionCodec.IsControlMessage(messageType))
            throw new ArgumentException("应用消息不能占用 Session 控制类型。", nameof(messageType));

        _transport.Send(
            _serverConnection,
            channel,
            SessionCodec.WriteEnvelope(messageType, payload));
    }

    /// <summary>取出一条已拆 Session 信封的应用消息。</summary>
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

    /// <summary>主动结束客户端 Session。</summary>
    public void Disconnect()
    {
        if (_disposed || State == ClientSessionState.Ended)
            return;
        End(DisconnectReason.Requested);
    }

    /// <summary>结束 Session 并释放底层 Transport。</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        Disconnect();
        _disposed = true;
        _transport.Dispose();
        _applicationPackets.Clear();
    }

    /// <summary>消费 Session 控制消息；其他消息在 Join 后透传应用层。</summary>
    void HandlePacket(NetPacket packet, long nowMs)
    {
        SessionCodec.ReadEnvelope(packet.Payload, out byte messageType, out byte[] body);
        _lastAuthorityActivityMs = nowMs;

        if (messageType == (byte)SessionMessageKind.JoinAccept)
        {
            if (State != ClientSessionState.Connecting)
                return;
            SessionJoinAccept accept = SessionCodec.ReadJoinAccept(body);
            if (accept.ContentVersion != _config.ContentVersion)
            {
                End(DisconnectReason.ContentMismatch);
                return;
            }

            JoinAccept = accept;
            State = ClientSessionState.Joined;
            _nextHeartbeatMs = nowMs + _config.HeartbeatIntervalMs;
            return;
        }

        if (messageType == (byte)SessionMessageKind.JoinReject)
        {
            SessionRejectReason reason = SessionCodec.ReadJoinReject(body);
            End(reason == SessionRejectReason.VersionMismatch
                ? DisconnectReason.ProtocolMismatch
                : reason == SessionRejectReason.ServerFull
                    ? DisconnectReason.ServerFull
                    : DisconnectReason.InternalError);
            return;
        }

        if (messageType == (byte)SessionMessageKind.Heartbeat)
        {
            SessionHeartbeat heartbeat = SessionCodec.ReadHeartbeat(body);
            if (heartbeat.EchoTimeMs > 0)
                RttMs = (int)Math.Max(0L, nowMs - heartbeat.EchoTimeMs);
            return;
        }

        if (messageType == (byte)SessionMessageKind.Kick)
        {
            SessionKickReason reason = SessionCodec.ReadKick(body);
            End(reason == SessionKickReason.IdleTimeout
                ? DisconnectReason.Timeout
                : DisconnectReason.ServerShutdown);
            return;
        }

        if (SessionCodec.IsControlMessage(messageType))
            return;
        if (State == ClientSessionState.Joined)
        {
            _applicationPackets.Enqueue(new SessionApplicationPacket(
                packet.ConnectionId,
                messageType,
                body));
        }
    }

    /// <summary>统一记录结束原因并关闭客户端本地连接。</summary>
    void End(DisconnectReason reason)
    {
        LastDisconnectReason = reason;
        State = ClientSessionState.Ended;
        _applicationPackets.Clear();
        if (_serverConnection.IsValid)
            _transport.Disconnect(_serverConnection, reason);
    }

    void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ClientSession));
    }

}
