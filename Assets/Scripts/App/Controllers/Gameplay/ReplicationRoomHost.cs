using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

/// <summary>
/// Listen Host 房间：绑定 UDP、接纳第二人、把远端 InputFrame 写入权威世界并下行 Tick。
/// 单机一人进关也走本组件，不另开旧 Host 分支。
/// </summary>
[DefaultExecutionOrder(-150)]
[DisallowMultipleComponent]
public sealed class ReplicationRoomHost : AppControllerBase
{
    const int GuestPlayerId = 2;

    CombatWorldController _world;
    UdpReplicationTransport _transport;
    ActionReplicationCatalog _catalog;
    GuestSeat _guest;
    readonly List<PendingJoin> _pendingJoins = new();
    readonly List<EnemyController> _enemies = new();
    readonly List<ActorReplicationSnapshot> _snapshots = new();
    bool _bindFailed;
    int _lastTickBytes = -1;
    int _lastCommandBytes = -1;

    /// <summary>由战斗世界在 Awake 注入；可重复调用。</summary>
    public void Configure(CombatWorldController world)
    {
        UnsubscribeHost();
        _world = world;
        if (isActiveAndEnabled)
            SubscribeHost();
    }

    void OnEnable() => SubscribeHost();

    void Start()
    {
        _catalog = new ActionReplicationCatalog();
        TryBindTransport();
        RefreshHud("Listening");
    }

    void Update()
    {
        // 绑定失败或尚未监听：本帧不收包
        if (_transport == null)
            return;

        // 把套接字收包泵进权威收件箱
        _transport.Pump();
        // 处理 Join/Command/Heartbeat
        DrainAuthorityInbox();
        // 待接纳队列：座位空时发 Accept
        TryAcceptPendingJoins();
        // 客机心跳超时则踢人
        CheckGuestIdle();
    }

    void OnDisable() => UnsubscribeHost();

    void OnDestroy()
    {
        UnsubscribeHost();
        KickGuest(RoomKickReason.HostEnded, notify: true);
        _transport?.Dispose();
        _transport = null;
    }

    /// <summary>权威步前已在 Update 写入远端输入；步后打包全员快照。无新命令时 appliedHint=0。</summary>
    void OnAfterLogicStep(long authorityFrame)
    {
        if (_transport == null || _guest == null || !_guest.Actor.SimulationId.IsValid)
        {
            RefreshHud(_guest != null ? "ClientJoined" : "Listening");
            return;
        }

        // 目录未预填时补齐 Graph/变体，避免客机 TryGet 失败
        PrefillCatalogIfNeeded();
        // 全员权威 Pose/招式写入快照列表
        CaptureAuthorityActors();
        ReplicatedHitEvent[] hits = CopyHits();
        var tick = new AuthorityTick(authorityFrame, _snapshots.ToArray(), hits);
        // CarryForward 必须下发 0；仅本步真正灌入远端命令时非 0
        long appliedHintThisTick = _guest.AppliedHintThisTick;
        _guest.AppliedHintThisTick = 0;
        byte[] payload = RoomCodec.WriteAuthorityTickEnvelope(
            appliedHintThisTick,
            ReplicationCodec.WriteAuthorityTick(tick));
        _lastTickBytes = payload.Length;
        _transport.SendAuthorityToClients(payload);
        RefreshHud("ClientJoined");
    }

    void TryBindTransport()
    {
        int port = _world != null ? _world.ListenPort : ReplicationRoomProtocol.DefaultPort;
        _transport = new UdpReplicationTransport();
        try
        {
            _transport.Bind(port);
            Debug.Log($"ReplicationRoomHost: 监听 UDP {_transport.BoundPort}。", this);
        }
        catch (Exception ex)
        {
            _bindFailed = true;
            Debug.LogError($"ReplicationRoomHost: 绑定端口 {port} 失败，房间不可加入。{ex.Message}", this);
            _transport.Dispose();
            _transport = null;
        }
    }

    void DrainAuthorityInbox()
    {
        while (_transport.TryDequeueAuthorityFrom(out byte[] payload, out IPEndPoint from))
        {
            try
            {
                RoomCodec.ReadEnvelope(payload, out RoomMessageKind kind, out byte[] body);
                if (kind == RoomMessageKind.ClientCommand)
                    _lastCommandBytes = payload.Length;
                HandleMessage(kind, body, from);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ReplicationRoomHost: 丢弃非法包。{ex.Message}", this);
            }
        }
    }

    void HandleMessage(RoomMessageKind kind, byte[] body, IPEndPoint from)
    {
        switch (kind)
        {
            case RoomMessageKind.JoinRequest:
                _pendingJoins.Add(new PendingJoin(from, RoomCodec.ReadJoinRequest(body)));
                break;
            case RoomMessageKind.Heartbeat:
                if (!IsGuestEndpoint(from))
                    return;
                _guest.Idle.Touch(NowMs());
                RoomHeartbeat request = RoomCodec.ReadHeartbeat(body);
                _transport.SendTo(
                    from,
                    RoomCodec.WriteHeartbeat(new RoomHeartbeat(request.SendTimeMs, request.SendTimeMs)));
                break;
            case RoomMessageKind.ClientCommand:
                if (!IsGuestEndpoint(from))
                    return;
                _guest.Idle.Touch(NowMs());
                ApplyGuestCommands(RoomCodec.ReadClientCommandBatch(body));
                break;
        }
    }

    void TryAcceptPendingJoins()
    {
        if (_pendingJoins.Count == 0 || _transport == null)
            return;

        ILocalPlayer local = SendQuery(new GetLocalPlayerQuery());
        CharacterActor hostActor = local?.Actor;
        if (hostActor == null || !hostActor.SimulationId.IsValid)
            return;

        for (int i = 0; i < _pendingJoins.Count; i++)
        {
            PendingJoin pending = _pendingJoins[i];
            if (_guest != null)
            {
                _transport.SendTo(
                    pending.From,
                    RoomCodec.WriteJoinReject(new RoomJoinReject(RoomRejectReason.RoomFull)));
                continue;
            }

            int contentVersion = _world != null ? _world.ContentVersion : 1;
            if (pending.Request.ProtocolVersion != ReplicationRoomProtocol.ProtocolVersion
                || pending.Request.ContentVersion != contentVersion)
            {
                _transport.SendTo(
                    pending.From,
                    RoomCodec.WriteJoinReject(new RoomJoinReject(RoomRejectReason.VersionMismatch)));
                continue;
            }

            if (!TrySpawnGuest(local, hostActor, pending.From, contentVersion))
            {
                _transport.SendTo(
                    pending.From,
                    RoomCodec.WriteJoinReject(new RoomJoinReject(RoomRejectReason.RoomFull)));
            }
        }

        _pendingJoins.Clear();
    }

    bool TrySpawnGuest(ILocalPlayer hostPlayer, CharacterActor hostActor, IPEndPoint from, int contentVersion)
    {
        CharacterConfig config = hostPlayer is PlayerController player
            ? player.CharacterConfig
            : null;
        SimulationHost host = _world != null ? _world.SimulationHost : null;
        if (config == null || host == null)
            return false;

        Vector3 spawn = hostPlayer.Root != null
            ? hostPlayer.Root.position + new Vector3(2f, 0f, 0f)
            : new Vector3(2f, 0f, 0f);
        var go = new GameObject("RemotePlayer");
        go.transform.SetPositionAndRotation(spawn, hostPlayer.Root != null
            ? hostPlayer.Root.rotation
            : Quaternion.identity);

        RemotePlayerSeat seat = go.AddComponent<RemotePlayerSeat>();
        CharacterActor actor = CharacterActorFactory.Create(
            go,
            go.transform,
            config,
            config.Combat.TeamId,
            localInput: null,
            () => SendQuery(new GetActiveTargetsQuery()),
            host.CombatHits,
            out ActionSim _,
            out CharacterAnimationService animation,
            host.CollisionWorld);
        seat.Bind(actor);

        var reactions = new CharacterReactionService(
            actor.Vitality,
            actor,
            new CharacterReactionResolver(config.Combat.Reactions));
        var hurtbox = new CharacterHurtboxTarget(
            go.transform,
            go.transform,
            config.Combat.TeamId,
            config.Combat.Hurtbox,
            actor.Vitality,
            actor.ActionSim,
            () => actor.SimulationId,
            actor.MotorSim,
            id => host.LookupNumeric(id));

        GetSystem<CombatActorSystem>()?.Register(go.transform, actor, animation);
        GetSystem<TargetSystem>()?.Register(hurtbox);
        GetSystem<LocalPlayerService>()?.Register(seat, isLocalOwner: false);

        actor.Enable();
        SimActorRegistration registration = host.RegisterPlayer(actor);
        host.RegisterNumeric(actor.SimulationId, actor.Numeric);

        _catalog.Prefill(config);
        PrefillEnemyCatalog();

        _guest = new GuestSeat(
            from,
            seat,
            actor,
            registration,
            reactions,
            hurtbox);
        _guest.Idle.Touch(NowMs());
        _transport.AddClient(from);

        var accept = new RoomJoinAccept(
            GuestPlayerId,
            actor.SimulationId.Value,
            hostActor.SimulationId.Value,
            contentVersion,
            host.CurrentFrame);
        _transport.SendTo(from, RoomCodec.WriteJoinAccept(in accept));
        Debug.Log(
            $"ReplicationRoomHost: 客机加入 actor={actor.SimulationId.Value} from={from}。",
            this);
        return true;
    }

    /// <summary>
    /// 把本包未应用 Hint 合并进下一权威帧。边沿 OR，避免冗余批只留下最后一帧而丢掉 Attack。
    /// FrameHint 只作乱序过滤，不与 CurrentFrame 比较。
    /// </summary>
    void ApplyGuestCommands(ClientCommand[] commands)
    {
        if (_guest == null || _world?.SimulationHost?.World == null)
            return;

        long targetFrame = _world.SimulationHost.CurrentFrame + 1;
        SimActorId actorId = _guest.Actor.SimulationId;
        if (!RoomRemoteInputMerge.TryMergeUnapplied(
                commands,
                _guest.LastAppliedFrameHint,
                targetFrame,
                actorId,
                out InputFrame merged,
                out long newestHint))
            return;

        InputFrameBuffer buffer = _world.SimulationHost.World.InputFrames;
        if (buffer.TryGetExact(targetFrame, actorId, out InputFrame existing))
            merged = existing.MergeSample(in merged);

        buffer.Set(in merged);
        _guest.LastAppliedFrameHint = newestHint;
        _guest.AppliedHintThisTick = newestHint;
    }

    void CheckGuestIdle()
    {
        if (_guest == null)
            return;
        if (_guest.Idle.IsTimedOut(NowMs()))
            KickGuest(RoomKickReason.IdleTimeout, notify: true);
    }

    void KickGuest(RoomKickReason reason, bool notify)
    {
        if (_guest == null)
            return;

        GuestSeat guest = _guest;
        _guest = null;
        if (notify && _transport != null)
        {
            try
            {
                _transport.SendTo(guest.EndPoint, RoomCodec.WriteKick(new RoomKick(reason)));
            }
            catch (Exception)
            {
            }
        }

        _transport?.RemoveClient(guest.EndPoint);
        SimulationHost host = _world != null ? _world.SimulationHost : null;
        GetSystem<LocalPlayerService>()?.Unregister(guest.Seat);
        GetSystem<TargetSystem>()?.Unregister(guest.Hurtbox);
        GetSystem<CombatActorSystem>()?.Unregister(guest.Seat != null ? guest.Seat.transform : null);
        if (host != null)
            host.Unregister(guest.Registration);
        guest.Reactions?.Dispose();
        guest.Actor?.Dispose();
        if (guest.Seat != null)
            Destroy(guest.Seat.gameObject);

        Debug.Log($"ReplicationRoomHost: 客机已剔除 reason={reason}。", this);
        RefreshHud("Listening");
    }

    void CaptureAuthorityActors()
    {
        _snapshots.Clear();
        ILocalPlayer local = SendQuery(new GetLocalPlayerQuery());
        if (local?.Actor != null)
        {
            _snapshots.Add(CharacterReplicationCapture.FromActor(
                local.Actor,
                _catalog,
                ReplicationActorKind.Player));
        }

        if (_guest?.Actor != null)
        {
            _snapshots.Add(CharacterReplicationCapture.FromActor(
                _guest.Actor,
                _catalog,
                ReplicationActorKind.Player));
        }

        SimulationHost host = _world != null ? _world.SimulationHost : null;
        if (host == null)
            return;

        host.CopyEnemyControllers(_enemies);
        for (int i = 0; i < _enemies.Count; i++)
        {
            CharacterActor enemy = _enemies[i].Actor;
            if (enemy == null)
                continue;
            _snapshots.Add(CharacterReplicationCapture.FromActor(
                enemy,
                _catalog,
                ReplicationActorKind.Enemy));
        }
    }

    ReplicatedHitEvent[] CopyHits()
    {
        SimulationHost host = _world != null ? _world.SimulationHost : null;
        if (host == null || host.FrameHits.Count == 0)
            return null;

        var copy = new ReplicatedHitEvent[host.FrameHits.Count];
        for (int i = 0; i < host.FrameHits.Count; i++)
        {
            ReplicatedHitEvent hit = host.FrameHits[i];
            copy[i] = hit.WithActionId(ResolveHitActionId(in hit));
        }

        return copy;
    }

    /// <summary>用本 Tick 攻击者快照的 ActionId 盖上命中，供客机还原 Hitbox Feedback。</summary>
    int ResolveHitActionId(in ReplicatedHitEvent hit)
    {
        int attacker = hit.Key.AttackerId.Value;
        for (int i = 0; i < _snapshots.Count; i++)
        {
            if (_snapshots[i].ActorId.Value == attacker)
                return _snapshots[i].ActionId;
        }

        return hit.ActionId;
    }

    void PrefillCatalogIfNeeded()
    {
        if (_catalog.Count > 0)
            return;
        ILocalPlayer local = SendQuery(new GetLocalPlayerQuery());
        if (local is PlayerController player)
            _catalog.Prefill(player.CharacterConfig);
        PrefillEnemyCatalog();
    }

    void PrefillEnemyCatalog()
    {
        EnemySpawnController[] spawns = FindObjectsOfType<EnemySpawnController>();
        for (int i = 0; i < spawns.Length; i++)
        {
            var configs = new List<CharacterConfig>();
            spawns[i].CollectCharacterConfigs(configs);
            for (int c = 0; c < configs.Count; c++)
                _catalog.Prefill(configs[c]);
        }
    }

    bool IsGuestEndpoint(IPEndPoint from) =>
        _guest != null && _guest.EndPoint.Equals(from);

    void RefreshHud(string status)
    {
        if (_world == null)
            return;

        int health = -1;
        ILocalPlayer local = SendQuery(new GetLocalPlayerQuery());
        if (local?.Actor?.Numeric != null)
            health = local.Actor.Numeric.Attributes.GetCurrent(AttributeId.Health);

        long frame = _world.SimulationHost != null ? _world.SimulationHost.CurrentFrame : -1;
        _world.RoomHud = new ReplicationRoomHudInfo(
            true,
            ReplicationRole.ListenHost,
            _bindFailed ? "BindFailed" : status,
            frame,
            rttMs: -1,
            health,
            _lastTickBytes,
            _lastCommandBytes,
            proxyCount: -1,
            predictionPendingCount: -1);
    }

    void SubscribeHost()
    {
        SimulationHost host = _world != null ? _world.SimulationHost : null;
        if (host != null)
            host.AfterLogicStep += OnAfterLogicStep;
    }

    void UnsubscribeHost()
    {
        SimulationHost host = _world != null ? _world.SimulationHost : null;
        if (host != null)
            host.AfterLogicStep -= OnAfterLogicStep;
    }

    static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    sealed class GuestSeat
    {
        public GuestSeat(
            IPEndPoint endPoint,
            RemotePlayerSeat seat,
            CharacterActor actor,
            SimActorRegistration registration,
            CharacterReactionService reactions,
            CharacterHurtboxTarget hurtbox)
        {
            EndPoint = endPoint;
            Seat = seat;
            Actor = actor;
            Registration = registration;
            Reactions = reactions;
            Hurtbox = hurtbox;
            Idle = new RoomIdleTracker();
        }

        public IPEndPoint EndPoint { get; }
        public RemotePlayerSeat Seat { get; }
        public CharacterActor Actor { get; }
        public SimActorRegistration Registration { get; }
        public CharacterReactionService Reactions { get; }
        public CharacterHurtboxTarget Hurtbox { get; }
        public RoomIdleTracker Idle { get; }
        public long LastAppliedFrameHint { get; set; }

        /// <summary>本逻辑步真正灌入的最新 Hint；无新命令时下行 0，避免 CarryForward 用旧 Hint 错位纠偏。</summary>
        public long AppliedHintThisTick { get; set; }
    }

    readonly struct PendingJoin
    {
        public PendingJoin(IPEndPoint from, RoomJoinRequest request)
        {
            From = from;
            Request = request;
        }

        public IPEndPoint From { get; }
        public RoomJoinRequest Request { get; }
    }
}
