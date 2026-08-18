using System;
using UnityEngine;

/// <summary>Host 房间的 ACT Gameplay 编排：玩家接纳、远端输入、权威 Capture 与 Guest 生命周期。</summary>
public sealed class ActHostRoomGameplay
{
    readonly CombatWorldController _world;
    readonly ActContentRegistry _content;
    readonly ActContentPrefillService _contentPrefill;
    readonly ActAuthorityReplicationAdapter _authority;
    readonly ActGameSessionHandler _gameSession;
    ReplicationServer _replicationServer = new();
    ActGameGuest _guest;

    /// <summary>创建 Host Gameplay 唯一编排入口，并立即登记场景内容。</summary>
    public ActHostRoomGameplay(
        CombatWorldController world,
        ACTGameArchitecture architecture)
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
        _contentPrefill.InitializeFromScene();
    }

    /// <summary>当前是否已有一个已接纳 Guest。</summary>
    public bool HasGuest => _guest != null;

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

    /// <summary>消费已通过 Session 校验的 Join 请求，并创建/接纳唯一 Guest。</summary>
    public void DrainPlayerRequests(ServerSession session)
    {
        if (session == null)
            return;

        while (session.TryDequeuePlayerRequest(out SessionPlayerRequest request))
        {
            PlayerController hostPlayer = _contentPrefill.LocalPlayer;
            CharacterActor hostActor = hostPlayer != null ? hostPlayer.Actor : null;
            if (_guest != null
                || hostActor == null
                || !hostActor.SimulationId.IsValid
                || !TryCreateGuest(session, hostPlayer, hostActor, in request))
            {
                session.RejectPlayer(request.ConnectionId, SessionRejectReason.GameRejected);
            }
        }
    }

    /// <summary>消费已鉴权 ClientCommand 正文，并把新 Hint 合并到下一权威输入帧。</summary>
    public void DrainApplicationMessages(ServerSession session)
    {
        if (session == null)
            return;

        while (session.TryDequeueApplication(out SessionApplicationPacket packet))
        {
            if (packet.MessageType != (byte)RoomMessageKind.ClientCommand
                || _guest == null
                || _guest.ConnectionId != packet.ConnectionId)
            {
                continue;
            }

            try
            {
                LastCommandBytes = packet.Payload.Length + 2;
                ApplyGuestCommands(RoomCodec.ReadClientCommandBatch(packet.Payload));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ActHostRoomGameplay: 丢弃非法命令正文。{ex.Message}");
            }
        }
    }

    /// <summary>权威步后构建当前 Guest 的 ReplicationFrame；无 Guest 时返回 false。</summary>
    public bool TryBuildReplicationFrame(
        long authorityFrame,
        out NetConnectionId connectionId,
        out byte[] body)
    {
        connectionId = default;
        body = null;
        if (_guest == null || !_guest.Actor.SimulationId.IsValid)
            return false;

        _contentPrefill.EnsureActionsReady();
        SimulationHost host = _world.SimulationHost;
        _authority.CaptureAuthorityActors(
            _contentPrefill.LocalPlayer,
            _guest.Actor,
            _guest.ArchetypeId,
            host);
        ReplicatedHitEvent[] hits = _authority.CopyHits(host?.FrameHits);
        long appliedHint = _guest.AppliedHintThisTick;
        _guest.AppliedHintThisTick = 0;
        byte[] applicationBytes = ActReplicationApplicationPayloadCodec.Encode(
            new ActReplicationApplicationPayload(appliedHint, hits));
        ReplicationFrame frame = _replicationServer.BuildFrame(
            new NetTick(authorityFrame),
            _authority.EntityStates,
            applicationBytes);

        connectionId = _guest.ConnectionId;
        body = ReplicationFrameCodec.Encode(frame);
        return true;
    }

    /// <summary>Session 断开时只清理匹配连接创建的 ACT Gameplay 对象。</summary>
    public void OnSessionDisconnected(SessionDisconnected disconnected)
    {
        if (_guest == null || _guest.ConnectionId != disconnected.ConnectionId)
            return;
        CleanupGuest(disconnected.Reason);
    }

    /// <summary>房间销毁时清理 Guest；重复调用安全。</summary>
    public void Shutdown() => CleanupGuest(DisconnectReason.ServerShutdown);

    /// <summary>创建 Guest、重置连接复制 baseline，并由 ServerSession 发出 Accept。</summary>
    bool TryCreateGuest(
        ServerSession session,
        PlayerController hostPlayer,
        CharacterActor hostActor,
        in SessionPlayerRequest request)
    {
        SimulationHost host = _world.SimulationHost;
        if (!_gameSession.TryCreateGuest(
                hostPlayer,
                host,
                request.ConnectionId,
                _contentPrefill.EnsureActionsReady,
                out ActGameGuest guest))
        {
            return false;
        }

        // Sequence/Registry baseline 属于连接；新 Guest 必须从完整 Spawn 开始。
        _replicationServer = new ReplicationServer();
        _guest = guest;
        session.AcceptPlayer(
            request.ConnectionId,
            new NetEntityId(guest.Actor.SimulationId.Value),
            new NetEntityId(hostActor.SimulationId.Value),
            new NetTick(host.CurrentFrame));
        Debug.Log(
            $"ActHostRoomGameplay: 客机加入 player={request.PlayerId.Value} "
            + $"actor={guest.Actor.SimulationId.Value} connection={request.ConnectionId}。");
        return true;
    }

    /// <summary>把本批未应用命令灌入下一权威帧，并更新 Guest Hint 状态。</summary>
    void ApplyGuestCommands(ClientCommand[] commands)
    {
        SimulationHost host = _world.SimulationHost;
        if (_guest == null || host?.World == null)
            return;

        ActAuthorityInputApplyResult result = _authority.ApplyGuestCommands(
            host.World.InputFrames,
            host.CurrentFrame,
            _guest.Actor.SimulationId,
            commands,
            _guest.LastAppliedFrameHint);
        if (!result.Applied)
            return;

        _guest.LastAppliedFrameHint = result.NewestHint;
        _guest.AppliedHintThisTick = result.NewestHint;
    }

    /// <summary>注销并销毁 Guest Gameplay；Session 连接表由调用方管理。</summary>
    void CleanupGuest(DisconnectReason reason)
    {
        if (_guest == null)
            return;

        ActGameGuest guest = _guest;
        _guest = null;
        _gameSession.DestroyGuest(guest, _world.SimulationHost);
        Debug.Log($"ActHostRoomGameplay: 客机 Gameplay 已清理 reason={reason}。");
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
