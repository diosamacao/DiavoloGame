using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>无本地玩家座位的权威运行时：Match、每连接 ACK、World 与下行复制；Listen 与 Dedicated 共用，不在此类型分支角色。</summary>
public sealed class DedicatedServerRuntime : IDisposable
{
    readonly ServerLaunchConfig _config;
    readonly ServerSession _session;
    readonly MatchCoordinator _match;
    readonly IDedicatedAuthorityWorld _authority;
    readonly Dictionary<NetConnectionId, DedicatedPlayerRuntime> _players = new();
    readonly List<DedicatedReplicationSend> _outbound = new();
    readonly List<DedicatedEventSend> _outboundEvents = new();
    readonly List<NetConnectionId> _playerScratch = new();
    bool _disposed;
    bool _pendingCompletedEnd;
    bool _endingMatch;
    bool _shouldExit;
    bool _hadPlayers;
    long _listenStartedMs = -1;

    DedicatedServerRuntime(
        ServerLaunchConfig config,
        ServerSession session,
        MatchCoordinator match,
        IDedicatedAuthorityWorld authority)
    {
        _config = config;
        _session = session;
        _match = match;
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _session.Disconnected += OnSessionDisconnected;
        ExitCode = ServerExitCode.Success;
        IsListening = true;
        MatchPhase = DedicatedMatchPhase.Lobby;
    }

    /// <summary>本运行时固定为 DedicatedServer，禁止 Listen 冒充。</summary>
    public NetProcessRole ProcessRole => NetProcessRole.DedicatedServer;

    /// <summary>Dedicated 不创建本机玩家座位。</summary>
    public int LocalPlayerCount => 0;

    /// <summary>Transport 已绑定并可 Accept。</summary>
    public bool IsListening { get; }

    /// <summary>已监听且尚未请求进程退出；Bootstrap 用它打 READY。</summary>
    public bool IsReady => IsListening && !_shouldExit && !_disposed;

    /// <summary>空房超时或对局结束且 ExitOnMatchEnd 时为 true；玩家构建应退出进程。</summary>
    public bool ShouldExit => _shouldExit;

    /// <summary>启动或运行失败时的退出码。</summary>
    public ServerExitCode ExitCode { get; private set; }

    /// <summary>已接纳远端玩家数。</summary>
    public int JoinedPlayerCount => _players.Count;

    /// <summary>当前对局阶段。</summary>
    public DedicatedMatchPhase MatchPhase { get; private set; }

    /// <summary>底层 Session，供测试读取连接表。</summary>
    public ServerSession Session => _session;

    /// <summary>Match 分配器。</summary>
    public MatchCoordinator Match => _match;

    /// <summary>启动时使用的配置。</summary>
    public ServerLaunchConfig Config => _config;

    /// <summary>校验配置并启动 Session；失败时释放 Transport 并返回退出码。</summary>
    public static DedicatedServerRuntime TryStart(
        INetTransport transport,
        ServerLaunchConfig config,
        IDedicatedAuthorityWorld authority,
        out ServerExitCode exitCode)
    {
        if (transport == null)
            throw new ArgumentNullException(nameof(transport));
        if (authority == null)
            throw new ArgumentNullException(nameof(authority));
        if (!config.Validate(out exitCode))
        {
            transport.Dispose();
            return null;
        }

        try
        {
            var session = new ServerSession(transport, config.CreateSessionConfig(), config.BindEndpoint);
            var match = new MatchCoordinator(config.MaxPlayers, config.PlayerArchetypeId);
            exitCode = ServerExitCode.Success;
            return new DedicatedServerRuntime(config, session, match, authority);
        }
        catch (Exception ex)
        {
            // 端口占用、权限或解析失败都落 BindFailed；必须打出底层原因，否则 Editor 只看到退出码。
            transport.Dispose();
            exitCode = ServerExitCode.BindFailed;
            Debug.LogError(
                $"DedicatedServerRuntime: 绑定失败 {config.BindHost}:{config.BindPort}。{ex.Message}");
            return null;
        }
    }

    /// <summary>泵 Session、接纳玩家、灌命令、步进并按连接发送 ReplicationFrame。</summary>
    public void Poll(long nowMs)
    {
        EnsureNotDisposed();
        if (_shouldExit)
            return;
        if (_listenStartedMs < 0)
            _listenStartedMs = nowMs;

        BeginPlayerTicks();
        _session.Poll(nowMs);
        DrainJoins();
        _authority.PublishImmediateReplication();
        DrainCommands();
        PromoteStartingToPlaying();
        _authority.Advance(nowMs);
        FlushReplication();
        FlushEvents();
        FinishPendingMatchEnd();
        CheckEmptyLobbyTimeout(nowMs);
    }

    /// <summary>只读预览本拍 Advance 步数；Listen 按此发本机命令，避免按渲染帧预测。</summary>
    public int PeekAdvanceSteps(long nowMs)
    {
        EnsureNotDisposed();
        return _authority.PeekAdvanceSteps(nowMs);
    }

    /// <summary>请求结束对局；下一 Poll 向仍在线连接可靠下发 MatchEnd。</summary>
    public void RequestMatchEnd()
    {
        EnsureNotDisposed();
        if (MatchPhase == DedicatedMatchPhase.Playing
            || MatchPhase == DedicatedMatchPhase.Starting)
        {
            _pendingCompletedEnd = true;
        }
    }

    /// <summary>按连接读取 ACK 状态；未知连接返回 false。</summary>
    public bool TryGetAck(NetConnectionId connectionId, out long lastAppliedHint, out long appliedHintThisTick)
    {
        if (_players.TryGetValue(connectionId, out DedicatedPlayerRuntime player))
        {
            lastAppliedHint = player.LastAppliedFrameHint;
            appliedHintThisTick = player.AppliedHintThisTick;
            return true;
        }

        lastAppliedHint = 0;
        appliedHintThisTick = 0;
        return false;
    }

    /// <summary>按连接读取 Match 槽位。</summary>
    public bool TryGetPlayer(NetConnectionId connectionId, out DedicatedPlayerRuntime player) =>
        _players.TryGetValue(connectionId, out player);

    /// <summary>按 Session PlayerId 查找连接运行时。</summary>
    public bool TryGetPlayerByPlayerId(NetPlayerId playerId, out DedicatedPlayerRuntime player)
    {
        foreach (KeyValuePair<NetConnectionId, DedicatedPlayerRuntime> pair in _players)
        {
            if (pair.Value.Slot.PlayerId == playerId)
            {
                player = pair.Value;
                return true;
            }
        }

        player = null;
        return false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _session.Disconnected -= OnSessionDisconnected;
        if (_players.Count > 0)
            BroadcastMatchEnd(MatchEndReason.ServerShutdown);
        _players.Clear();
        _authority.Dispose();
        _session.Dispose();
    }

    void BeginPlayerTicks()
    {
        foreach (DedicatedPlayerRuntime player in _players.Values)
            player.BeginTick();
    }

    /// <summary>Join 只问 Match 与权威世界；Ending 之后拒收。</summary>
    void DrainJoins()
    {
        while (_session.TryDequeuePlayerRequest(out SessionPlayerRequest request))
        {
            if (!CanAcceptJoin()
                || !_match.TryAccept(in request, out MatchPlayerSlot slot)
                || !_authority.TryAcceptPlayer(in slot, out NetEntityId entityId)
                || !entityId.IsValid)
            {
                _match.Release(request.ConnectionId);
                _session.RejectPlayer(request.ConnectionId, SessionRejectReason.GameRejected);
                continue;
            }

            var player = new DedicatedPlayerRuntime(in slot, entityId);
            _players.Add(request.ConnectionId, player);
            _hadPlayers = true;
            if (MatchPhase == DedicatedMatchPhase.Lobby)
                MatchPhase = DedicatedMatchPhase.Starting;

            long tick = _authority.CurrentFrame < 0 ? 0 : _authority.CurrentFrame;
            _session.AcceptPlayer(
                request.ConnectionId,
                entityId,
                NetEntityId.Invalid,
                new NetTick(tick));
            Debug.Log(
                $"DedicatedServerRuntime: join connection={request.ConnectionId} "
                + $"player={request.PlayerId.Value} entity={entityId.Value} tick={tick}。");
        }
    }

    void DrainCommands()
    {
        while (_session.TryDequeueApplication(out SessionApplicationPacket packet))
        {
            if (packet.MessageType != (byte)RoomMessageKind.ClientCommand)
                continue;
            if (!_players.TryGetValue(packet.ConnectionId, out DedicatedPlayerRuntime player))
                continue;

            try
            {
                ClientCommand[] commands = RoomCodec.ReadClientCommandBatch(packet.Payload);
                ClientCommand[] owned = FilterOwnerCommands(player.Slot.PlayerId, commands);
                if (owned.Length == 0)
                    continue;

                player.ApplyUnappliedHints(owned);
                _authority.ApplyCommands(packet.ConnectionId, owned);
                long newestHint = player.AppliedHintThisTick > 0
                    ? player.AppliedHintThisTick
                    : player.LastAppliedFrameHint;
                Debug.Log(
                    $"DedicatedServerRuntime: cmd connection={packet.ConnectionId} "
                    + $"player={player.Slot.PlayerId.Value} entity={player.EntityId.Value} "
                    + $"tick={_authority.CurrentFrame} hint={newestHint}。");
            }
            catch (Exception)
            {
                // 非法正文不影响其他连接。
            }
        }
    }

    /// <summary>只保留本连接 PlayerId 的命令，禁止代打其他座位。</summary>
    static ClientCommand[] FilterOwnerCommands(NetPlayerId ownerPlayerId, ClientCommand[] commands)
    {
        if (commands == null || commands.Length == 0 || !ownerPlayerId.IsValid)
            return Array.Empty<ClientCommand>();

        int owner = ownerPlayerId.Value;
        int keep = 0;
        for (int i = 0; i < commands.Length; i++)
        {
            if (commands[i].SenderPlayerId == owner)
                keep++;
        }

        if (keep == 0)
            return Array.Empty<ClientCommand>();
        if (keep == commands.Length)
            return commands;

        var owned = new ClientCommand[keep];
        int write = 0;
        for (int i = 0; i < commands.Length; i++)
        {
            if (commands[i].SenderPlayerId == owner)
                owned[write++] = commands[i];
        }

        return owned;
    }

    void PromoteStartingToPlaying()
    {
        if (MatchPhase == DedicatedMatchPhase.Starting)
            MatchPhase = DedicatedMatchPhase.Playing;
    }

    void FlushReplication()
    {
        if (MatchPhase != DedicatedMatchPhase.Playing)
            return;

        _authority.DrainOutboundReplication(_outbound);
        for (int i = 0; i < _outbound.Count; i++)
        {
            DedicatedReplicationSend send = _outbound[i];
            if (!_players.ContainsKey(send.ConnectionId) || send.Body == null || send.Body.Length == 0)
                continue;

            try
            {
                _session.SendApplication(
                    send.ConnectionId,
                    (byte)RoomMessageKind.ReplicationFrame,
                    NetChannel.SnapshotUnreliableSequenced,
                    send.Body);
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>本帧命中走可靠事件通道；失败只记该连接，不中断其余下行。</summary>
    void FlushEvents()
    {
        if (MatchPhase != DedicatedMatchPhase.Playing)
            return;

        _authority.DrainOutboundEvents(_outboundEvents);
        for (int i = 0; i < _outboundEvents.Count; i++)
        {
            DedicatedEventSend send = _outboundEvents[i];
            if (!_players.ContainsKey(send.ConnectionId) || send.Body == null || send.Body.Length == 0)
                continue;

            try
            {
                _session.SendApplication(
                    send.ConnectionId,
                    (byte)RoomMessageKind.ReplicationEvent,
                    NetChannel.EventReliableOrdered,
                    send.Body);
            }
            catch (Exception)
            {
            }
        }
    }

    void FinishPendingMatchEnd()
    {
        if (_endingMatch)
            return;

        if (_pendingCompletedEnd)
        {
            EnterEnding(MatchEndReason.Completed);
            return;
        }

        if (MatchPhase == DedicatedMatchPhase.Playing && _players.Count == 0)
            EnterEnding(MatchEndReason.EmptyRoom);
    }

    /// <summary>向仍在线连接发 MatchEnd，再踢线并回到 Lobby。</summary>
    void EnterEnding(MatchEndReason reason)
    {
        _endingMatch = true;
        _pendingCompletedEnd = false;
        MatchPhase = DedicatedMatchPhase.Ending;
        BroadcastMatchEnd(reason);
        CopyPlayerIds(_playerScratch);
        for (int i = 0; i < _playerScratch.Count; i++)
        {
            try
            {
                _session.Disconnect(_playerScratch[i], DisconnectReason.ServerShutdown);
            }
            catch (Exception)
            {
            }
        }

        MatchPhase = DedicatedMatchPhase.Cleanup;
        _players.Clear();
        MatchPhase = DedicatedMatchPhase.Lobby;
        _endingMatch = false;
        if (_config.ExitOnMatchEnd)
            RequestProcessExit();
    }

    void BroadcastMatchEnd(MatchEndReason reason)
    {
        long tick = _authority.CurrentFrame < 0 ? 0 : _authority.CurrentFrame;
        byte[] body = RoomCodec.WriteMatchEnd(new MatchEndMessage(reason, tick));
        CopyPlayerIds(_playerScratch);
        for (int i = 0; i < _playerScratch.Count; i++)
        {
            try
            {
                _session.SendApplication(
                    _playerScratch[i],
                    (byte)RoomMessageKind.MatchEnd,
                    NetChannel.ControlReliableOrdered,
                    body);
            }
            catch (Exception)
            {
            }
        }
    }

    void CopyPlayerIds(List<NetConnectionId> results)
    {
        results.Clear();
        foreach (NetConnectionId id in _players.Keys)
            results.Add(id);
    }

    bool CanAcceptJoin() =>
        !_shouldExit
        && (MatchPhase == DedicatedMatchPhase.Lobby
            || MatchPhase == DedicatedMatchPhase.Starting
            || MatchPhase == DedicatedMatchPhase.Playing);

    /// <summary>无人到访过的 Lobby 超过配置后请求退出；已有过玩家则只走 ExitOnMatchEnd。</summary>
    void CheckEmptyLobbyTimeout(long nowMs)
    {
        if (_shouldExit || _hadPlayers || _config.EmptyLobbyTimeoutMs <= 0)
            return;
        if (MatchPhase != DedicatedMatchPhase.Lobby || _players.Count > 0)
            return;
        if (nowMs - _listenStartedMs >= _config.EmptyLobbyTimeoutMs)
            RequestProcessExit();
    }

    /// <summary>请求进程级退出；退出码保持 Success，由 Bootstrap 在玩家构建里 Quit。</summary>
    void RequestProcessExit()
    {
        _shouldExit = true;
        ExitCode = ServerExitCode.Success;
    }

    /// <summary>只清理断开连接的 Match/复制状态，其余玩家保留。</summary>
    void OnSessionDisconnected(SessionDisconnected disconnected)
    {
        _players.Remove(disconnected.ConnectionId);
        _match.Release(disconnected.ConnectionId);
        _authority.RemovePlayer(disconnected.ConnectionId);
        if (!_endingMatch
            && MatchPhase == DedicatedMatchPhase.Playing
            && _players.Count == 0)
        {
            _pendingCompletedEnd = false;
        }
    }

    void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DedicatedServerRuntime));
    }
}
