using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Host 房间的 ACT Gameplay 编排：N 名远端玩家、独立 ACK、权威 Capture。</summary>
public sealed class ActHostRoomGameplay
{
    readonly CombatWorldController _world;
    readonly ActContentRegistry _content;
    readonly ActContentPrefillService _contentPrefill;
    readonly ActAuthorityReplicationAdapter _authority;
    readonly ActGameSessionHandler _gameSession;
    readonly MatchCoordinator _match;
    readonly Dictionary<NetConnectionId, ActGameGuest> _guests = new();
    readonly Dictionary<NetConnectionId, ReplicationServer> _replicationByConnection = new();
    readonly List<ActGameGuest> _guestSnapshot = new();
    readonly List<NetConnectionId> _connectionScratch = new();

    /// <summary>创建 Host Gameplay 唯一编排入口，并立即登记场景内容。</summary>
    public ActHostRoomGameplay(
        CombatWorldController world,
        ACTGameArchitecture architecture,
        int maxRemotePlayers)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        if (architecture == null)
            throw new ArgumentNullException(nameof(architecture));

        _content = new ActContentRegistry();
        _contentPrefill = new ActContentPrefillService(architecture, _content);
        _authority = new ActAuthorityReplicationAdapter(_content);
        _gameSession = new ActGameSessionHandler(
            _content,
            CreateGameSessionServices(architecture));
        _match = new MatchCoordinator(maxRemotePlayers, playerArchetypeId: default);
        _contentPrefill.InitializeFromScene();
    }

    /// <summary>当前是否已有至少一名远端玩家。</summary>
    public bool HasGuest => _guests.Count > 0;

    /// <summary>已接纳远端玩家数。</summary>
    public int GuestCount => _guests.Count;

    /// <summary>最近收到的完整 ClientCommand 应用消息字节；尚未收到时为 -1。</summary>
    public int LastCommandBytes { get; private set; } = -1;

    /// <summary>Host 本机权威生命值；玩家尚未装配时为 -1。</summary>
    public int HostHealthMilli
    {
        get
        {
            PlayerController player = _contentPrefill.LocalPlayer;
            return player?.Actor?.Numeric != null
                ? player.Actor.Numeric.Attributes.GetCurrent(AttributeId.Health)
                : -1;
        }
    }

    /// <summary>复制当前已接纳连接，供 Room 逐连接构帧发送。</summary>
    public void CopyGuestConnections(List<NetConnectionId> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));
        results.Clear();
        foreach (NetConnectionId connectionId in _guests.Keys)
            results.Add(connectionId);
    }

    /// <summary>消费 Join：只要求已登记角色配置，不再等待 Host Actor。</summary>
    public void DrainPlayerRequests(ServerSession session)
    {
        if (session == null)
            return;

        _contentPrefill.InitializeFromScene();
        while (session.TryDequeuePlayerRequest(out SessionPlayerRequest request))
        {
            if (_guests.ContainsKey(request.ConnectionId)
                || !TryCreateGuest(session, in request))
            {
                session.RejectPlayer(request.ConnectionId, SessionRejectReason.GameRejected);
            }
        }
    }

    /// <summary>按连接消费 ClientCommand，ACK 不串线。</summary>
    public void DrainApplicationMessages(ServerSession session)
    {
        if (session == null)
            return;

        while (session.TryDequeueApplication(out SessionApplicationPacket packet))
        {
            if (packet.MessageType != (byte)RoomMessageKind.ClientCommand
                || !_guests.TryGetValue(packet.ConnectionId, out ActGameGuest guest))
            {
                continue;
            }

            try
            {
                LastCommandBytes = packet.Payload.Length + 2;
                ApplyGuestCommands(guest, RoomCodec.ReadClientCommandBatch(packet.Payload));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ActHostRoomGameplay: 丢弃非法命令正文。{ex.Message}");
            }
        }
    }

    /// <summary>为指定连接构建 ReplicationFrame；无此 Guest 时返回 false。</summary>
    public bool TryBuildReplicationFrame(
        long authorityFrame,
        NetConnectionId connectionId,
        out byte[] body)
    {
        body = null;
        if (!_guests.TryGetValue(connectionId, out ActGameGuest guest)
            || guest.Actor == null
            || !guest.Actor.SimulationId.IsValid
            || !_replicationByConnection.TryGetValue(connectionId, out ReplicationServer replication))
        {
            return false;
        }

        _contentPrefill.EnsureActionsReady();
        SimulationHost host = _world.SimulationHost;
        CopyGuests(_guestSnapshot);
        _authority.CaptureAuthorityActors(_contentPrefill.LocalPlayer, _guestSnapshot, host);
        ReplicatedHitEvent[] hits = _authority.CopyHits(host?.FrameHits);
        long appliedHint = guest.AppliedHintThisTick;
        guest.AppliedHintThisTick = 0;
        byte[] applicationBytes = ActReplicationApplicationPayloadCodec.Encode(
            new ActReplicationApplicationPayload(appliedHint, hits));
        ReplicationFrame frame = replication.BuildFrame(
            new NetTick(authorityFrame),
            _authority.EntityStates,
            applicationBytes);
        body = ReplicationFrameCodec.Encode(frame);
        return true;
    }

    /// <summary>Session 断开时只清理对应连接的 Guest。</summary>
    public void OnSessionDisconnected(SessionDisconnected disconnected)
    {
        CleanupGuest(disconnected.ConnectionId, disconnected.Reason);
    }

    /// <summary>房间销毁时清理全部 Guest。</summary>
    public void Shutdown()
    {
        _connectionScratch.Clear();
        foreach (NetConnectionId connectionId in _guests.Keys)
            _connectionScratch.Add(connectionId);
        for (int i = 0; i < _connectionScratch.Count; i++)
            CleanupGuest(_connectionScratch[i], DisconnectReason.ServerShutdown);
    }

    /// <summary>创建 Guest、按连接重置复制 baseline，并由 ServerSession 发出 Accept。</summary>
    bool TryCreateGuest(ServerSession session, in SessionPlayerRequest request)
    {
        if (!_match.TryAccept(in request, out MatchPlayerSlot slot))
            return false;

        CharacterConfig config = ResolveJoinConfig();
        SimulationHost host = _world.SimulationHost;
        MatchSpawnPose spawn = slot.Spawn;
        if (config == null
            || !_gameSession.TryCreateGuest(
                config,
                spawn,
                host,
                request.ConnectionId,
                _contentPrefill.EnsureActionsReady,
                out ActGameGuest guest))
        {
            _match.Release(request.ConnectionId);
            return false;
        }

        // Sequence/Registry baseline 属于连接；新 Guest 必须从完整 Spawn 开始。
        _replicationByConnection[request.ConnectionId] = new ReplicationServer();
        _guests.Add(request.ConnectionId, guest);
        NetEntityId hostEntity = TryResolveHostEntityId();
        session.AcceptPlayer(
            request.ConnectionId,
            new NetEntityId(guest.Actor.SimulationId.Value),
            hostEntity,
            new NetTick(host.CurrentFrame < 0 ? 0 : host.CurrentFrame));
        Debug.Log(
            $"ActHostRoomGameplay: 客机加入 player={request.PlayerId.Value} "
            + $"actor={guest.Actor.SimulationId.Value} connection={request.ConnectionId}。");
        return true;
    }

    /// <summary>Join 用已预填或本机配置，不要求 Host Actor 已注册进 World。</summary>
    CharacterConfig ResolveJoinConfig()
    {
        PlayerController local = _contentPrefill.LocalPlayer;
        if (local != null && local.CharacterConfig != null)
            return local.CharacterConfig;
        return _content.TryGetAnyPlayerConfig(out CharacterConfig config) ? config : null;
    }

    /// <summary>Listen 若有本机 Actor 则写入 Accept；否则 Invalid，客户端不得依赖。</summary>
    NetEntityId TryResolveHostEntityId()
    {
        CharacterActor actor = _contentPrefill.LocalPlayer != null
            ? _contentPrefill.LocalPlayer.Actor
            : null;
        if (actor != null && actor.SimulationId.IsValid)
            return new NetEntityId(actor.SimulationId.Value);
        return NetEntityId.Invalid;
    }

    /// <summary>把本批未应用命令灌入下一权威帧；LastApplied=newest，下行 appliedHint=第一条新 Hint。</summary>
    void ApplyGuestCommands(ActGameGuest guest, ClientCommand[] commands)
    {
        SimulationHost host = _world.SimulationHost;
        if (guest == null || host?.World == null)
            return;

        ActAuthorityInputApplyResult result = _authority.ApplyGuestCommands(
            host.World.InputFrames,
            host.CurrentFrame,
            guest.Actor.SimulationId,
            commands,
            guest.LastAppliedFrameHint);
        if (!result.Applied)
            return;

        guest.LastAppliedFrameHint = result.NewestHint;
        guest.AppliedHintThisTick = result.FirstAppliedHint;
    }

    /// <summary>注销并销毁指定连接的 Guest；不影响其他连接。</summary>
    void CleanupGuest(NetConnectionId connectionId, DisconnectReason reason)
    {
        if (!_guests.TryGetValue(connectionId, out ActGameGuest guest))
            return;

        _guests.Remove(connectionId);
        _replicationByConnection.Remove(connectionId);
        _match.Release(connectionId);
        _gameSession.DestroyGuest(guest, _world.SimulationHost);
        Debug.Log($"ActHostRoomGameplay: 客机 Gameplay 已清理 connection={connectionId} reason={reason}。");
    }

    void CopyGuests(List<ActGameGuest> results)
    {
        results.Clear();
        foreach (ActGameGuest guest in _guests.Values)
            results.Add(guest);
    }

    /// <summary>把 Architecture 系统能力收敛为 Guest Handler 的最小服务集合。</summary>
    static ActGameSessionServices CreateGameSessionServices(
        ACTGameArchitecture architecture)
    {
        return new ActGameSessionServices(
            () => architecture.SendQuery(new GetActiveTargetsQuery()),
            (root, actor, animation) =>
                architecture.GetSystem<CombatActorSystem>()?.Register(root, actor, animation),
            root => architecture.GetSystem<CombatActorSystem>()?.Unregister(root),
            target => architecture.GetSystem<TargetSystem>()?.Register(target),
            target => architecture.GetSystem<TargetSystem>()?.Unregister(target),
            (player, isLocalOwner) =>
                architecture.GetSystem<LocalPlayerService>()?.Register(player, isLocalOwner),
            player => architecture.GetSystem<LocalPlayerService>()?.Unregister(player),
            gameObject => UnityEngine.Object.Destroy(gameObject));
    }
}
