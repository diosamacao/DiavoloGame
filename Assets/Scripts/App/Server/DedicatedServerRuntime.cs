using System;
using System.Collections.Generic;

/// <summary>无本地玩家的 Dedicated 运行时：Session / Match / 每连接 ACK，并驱动权威 World。</summary>
public sealed class DedicatedServerRuntime : IDisposable
{
    readonly ServerLaunchConfig _config;
    readonly ServerSession _session;
    readonly MatchCoordinator _match;
    readonly IDedicatedAuthorityWorld _authority;
    readonly Dictionary<NetConnectionId, DedicatedPlayerRuntime> _players = new();
    bool _disposed;

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
    }

    /// <summary>本运行时固定为 DedicatedServer，禁止 Listen 冒充。</summary>
    public NetProcessRole ProcessRole => NetProcessRole.DedicatedServer;

    /// <summary>Dedicated 不创建本机玩家座位。</summary>
    public int LocalPlayerCount => 0;

    /// <summary>Transport 已绑定并可 Accept。</summary>
    public bool IsListening { get; }

    /// <summary>启动或运行失败时的退出码。</summary>
    public ServerExitCode ExitCode { get; private set; }

    /// <summary>已接纳远端玩家数。</summary>
    public int JoinedPlayerCount => _players.Count;

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
        catch (Exception)
        {
            transport.Dispose();
            exitCode = ServerExitCode.BindFailed;
            return null;
        }
    }

    /// <summary>泵 Session、接纳玩家、按连接合并命令 Hint。</summary>
    public void Poll(long nowMs)
    {
        EnsureNotDisposed();
        BeginPlayerTicks();
        _session.Poll(nowMs);
        DrainJoins();
        DrainCommands();
        _authority.Advance(nowMs);
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
        _players.Clear();
        _authority.Dispose();
        _session.Dispose();
    }

    void BeginPlayerTicks()
    {
        foreach (DedicatedPlayerRuntime player in _players.Values)
            player.BeginTick();
    }

    /// <summary>Join 只问 Match，不再等待 Host Local Actor。</summary>
    void DrainJoins()
    {
        while (_session.TryDequeuePlayerRequest(out SessionPlayerRequest request))
        {
            if (!_match.TryAccept(in request, out MatchPlayerSlot slot)
                || !_authority.TryAcceptPlayer(in slot))
            {
                _match.Release(request.ConnectionId);
                _session.RejectPlayer(request.ConnectionId, SessionRejectReason.GameRejected);
                continue;
            }

            var player = new DedicatedPlayerRuntime(in slot);
            _players.Add(request.ConnectionId, player);
            _session.AcceptPlayer(
                request.ConnectionId,
                slot.EntityId,
                NetEntityId.Invalid,
                new NetTick(0));
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
                player.ApplyUnappliedHints(commands);
                _authority.ApplyCommands(packet.ConnectionId, commands);
            }
            catch (Exception)
            {
                // 非法正文不影响其他连接。
            }
        }
    }

    /// <summary>只清理断开连接的 Match/复制状态，其余玩家保留。</summary>
    void OnSessionDisconnected(SessionDisconnected disconnected)
    {
        _players.Remove(disconnected.ConnectionId);
        _match.Release(disconnected.ConnectionId);
        _authority.RemovePlayer(disconnected.ConnectionId);
    }

    void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DedicatedServerRuntime));
    }
}
