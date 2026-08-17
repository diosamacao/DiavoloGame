using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Listen Host 房间：绑定 UDP、接纳第二人、把远端 InputFrame 写入权威世界并下行 ReplicationFrame。
/// 单机一人进关也走本组件，不另开旧 Host 分支。
/// </summary>
[DefaultExecutionOrder(-150)]
[DisallowMultipleComponent]
public sealed class ReplicationRoomHost : AppControllerBase
{
    CombatWorldController _world;
    ServerSession _session;
    ActionReplicationCatalog _catalog;
    ReplicationServer _replicationServer;
    CharacterReplicationContentRegistry _content;
    ActAuthorityReplicationAdapter _authority;
    GuestSeat _guest;
    readonly List<EnemyDefinition> _enemyDefinitions = new();
    bool _bindFailed;
    int _lastTickBytes = -1;
    int _lastCommandBytes = -1;

    /// <summary>由 Composition Root 注入战斗世界与已启动的服务端 Session。</summary>
    public void Configure(CombatWorldController world, ServerSession session)
    {
        UnsubscribeHost();
        if (_session != null)
            _session.Disconnected -= OnSessionDisconnected;
        _world = world;
        _session = session;
        _bindFailed = session == null;
        if (_session != null)
            _session.Disconnected += OnSessionDisconnected;
        if (isActiveAndEnabled)
            SubscribeHost();
    }

    void OnEnable() => SubscribeHost();

    void Start()
    {
        _catalog = new ActionReplicationCatalog();
        _replicationServer = new ReplicationServer();
        _content = new CharacterReplicationContentRegistry();
        _authority = new ActAuthorityReplicationAdapter(_catalog, _content);
        RegisterStaticReplicationContent();
        RefreshHud("Listening");
    }

    void Update()
    {
        // 绑定失败或 Session 尚未监听：本帧不收包
        if (_session == null)
            return;

        // Session 独占握手、心跳和超时；Room 只处理 Gameplay 接纳与命令。
        _session.Poll(NowMs());
        DrainPlayerRequests();
        DrainApplicationMessages();
    }

    void OnDisable() => UnsubscribeHost();

    void OnDestroy()
    {
        UnsubscribeHost();
        if (_session != null)
            _session.Disconnected -= OnSessionDisconnected;
        _session?.Dispose();
        _session = null;
        CleanupGuest(DisconnectReason.ServerShutdown);
    }

    /// <summary>权威步后构建 full-set ReplicationFrame；无新命令时 appliedHint=0。</summary>
    void OnAfterLogicStep(long authorityFrame)
    {
        if (_session == null || _guest == null || !_guest.Actor.SimulationId.IsValid)
        {
            RefreshHud(_guest != null ? "ClientJoined" : "Listening");
            return;
        }

        // 目录未预填时补齐 Graph/变体，避免客机 TryGet 失败
        PrefillCatalogIfNeeded();
        // 全员权威 Pose/招式同时写入命中查询快照与通用复制 full set。
        CaptureAuthorityActors();
        ReplicatedHitEvent[] hits = CopyHits();
        // CarryForward 必须下发 0；仅本步真正灌入远端命令时非 0
        long appliedHintThisTick = _guest.AppliedHintThisTick;
        _guest.AppliedHintThisTick = 0;
        var applicationPayload = new ActReplicationApplicationPayload(
            appliedHintThisTick,
            hits);
        byte[] applicationBytes =
            ActReplicationApplicationPayloadCodec.Encode(applicationPayload);
        ReplicationFrame frame = _replicationServer.BuildFrame(
            new NetTick(authorityFrame),
            _authority.EntityStates,
            applicationBytes);
        byte[] body = ReplicationFrameCodec.Encode(frame);
        _lastTickBytes = body.Length + 2;
        _session.SendApplication(
            _guest.ConnectionId,
            (byte)RoomMessageKind.ReplicationFrame,
            NetChannel.SnapshotUnreliableSequenced,
            body);
        RefreshHud("ClientJoined");
    }

    /// <summary>把已通过 Session 校验的玩家请求交给 ACT Gameplay 创建 Authority Actor。</summary>
    void DrainPlayerRequests()
    {
        while (_session.TryDequeuePlayerRequest(out SessionPlayerRequest request))
        {
            ILocalPlayer local = SendQuery(new GetLocalPlayerQuery());
            CharacterActor hostActor = local?.Actor;
            if (_guest != null
                || hostActor == null
                || !hostActor.SimulationId.IsValid
                || !TrySpawnGuest(local, hostActor, in request))
            {
                _session.RejectPlayer(request.ConnectionId, SessionRejectReason.GameRejected);
            }
        }
    }

    /// <summary>只消费 Session 已鉴权连接的 ClientCommand 应用消息。</summary>
    void DrainApplicationMessages()
    {
        while (_session.TryDequeueApplication(out SessionApplicationPacket packet))
        {
            if (packet.MessageType != (byte)RoomMessageKind.ClientCommand
                || _guest == null
                || _guest.ConnectionId != packet.ConnectionId)
            {
                continue;
            }

            try
            {
                _lastCommandBytes = packet.Payload.Length + 2;
                ApplyGuestCommands(RoomCodec.ReadClientCommandBatch(packet.Payload));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ReplicationRoomHost: 丢弃非法命令正文。{ex.Message}", this);
            }
        }
    }

    bool TrySpawnGuest(
        ILocalPlayer hostPlayer,
        CharacterActor hostActor,
        in SessionPlayerRequest request)
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
        NetArchetypeId guestArchetypeId = _content.RegisterPlayer(config);

        // ReplicationServer 的 Registry/Sequence 属于连接；新客机必须从全量 Spawn 开始，不能继承上一连接 baseline。
        _replicationServer = new ReplicationServer();
        _guest = new GuestSeat(
            request.ConnectionId,
            seat,
            actor,
            registration,
            reactions,
            hurtbox,
            guestArchetypeId);
        _session.AcceptPlayer(
            request.ConnectionId,
            new NetEntityId(actor.SimulationId.Value),
            new NetEntityId(hostActor.SimulationId.Value),
            new NetTick(host.CurrentFrame));
        Debug.Log(
            $"ReplicationRoomHost: 客机加入 player={request.PlayerId.Value} actor={actor.SimulationId.Value} connection={request.ConnectionId}。",
            this);
        return true;
    }

    /// <summary>
    /// 把本包未应用 Hint 合并进下一权威帧。边沿 OR，避免冗余批只留下最后一帧而丢掉 Attack。
    /// FrameHint 只作乱序过滤，不与 CurrentFrame 比较。
    /// </summary>
    void ApplyGuestCommands(ClientCommand[] commands)
    {
        SimulationHost host = _world != null ? _world.SimulationHost : null;
        if (_guest == null || host?.World == null || _authority == null)
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

    /// <summary>Session 断开后清理由该连接创建的全部 ACT Gameplay 对象。</summary>
    void OnSessionDisconnected(SessionDisconnected disconnected)
    {
        if (_guest == null || _guest.ConnectionId != disconnected.ConnectionId)
            return;
        CleanupGuest(disconnected.Reason);
    }

    /// <summary>只负责 Gameplay 注销与销毁；网络通知和连接表由 ServerSession 独占。</summary>
    void CleanupGuest(DisconnectReason reason)
    {
        if (_guest == null)
            return;

        GuestSeat guest = _guest;
        _guest = null;
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

        Debug.Log($"ReplicationRoomHost: 客机 Gameplay 已清理 reason={reason}。", this);
        RefreshHud("Listening");
    }

    /// <summary>捕获玩家、Guest 与运行中敌人的完整权威状态，并精准绑定各自原型。</summary>
    void CaptureAuthorityActors()
    {
        ILocalPlayer local = SendQuery(new GetLocalPlayerQuery());
        SimulationHost host = _world != null ? _world.SimulationHost : null;
        _authority.CaptureAuthorityActors(
            local,
            _guest?.Actor,
            _guest != null ? _guest.ArchetypeId : default,
            host);
    }

    /// <summary>由权威适配器复制本帧命中，并从同帧快照补齐唯一 ActionId。</summary>
    ReplicatedHitEvent[] CopyHits()
    {
        SimulationHost host = _world != null ? _world.SimulationHost : null;
        return _authority.CopyHits(host?.FrameHits);
    }

    /// <summary>动作目录为空时，从本机与场景敌人配置一次性补齐动作及变体。</summary>
    void PrefillCatalogIfNeeded()
    {
        if (_catalog.Count > 0)
            return;
        ILocalPlayer local = SendQuery(new GetLocalPlayerQuery());
        if (local is PlayerController player)
            _catalog.Prefill(player.CharacterConfig);
        PrefillEnemyCatalog();
    }

    /// <summary>遍历场景刷怪定义，预填所有敌人身体配置引用的动作目录。</summary>
    void PrefillEnemyCatalog()
    {
        EnemySpawnController[] spawns = FindObjectsOfType<EnemySpawnController>();
        for (int i = 0; i < spawns.Length; i++)
        {
            _enemyDefinitions.Clear();
            spawns[i].CollectDefinitions(_enemyDefinitions);
            for (int c = 0; c < _enemyDefinitions.Count; c++)
                _catalog.Prefill(_enemyDefinitions[c].CharacterConfig);
        }
    }

    /// <summary>在房间启动时对称登记本机玩家与场景刷怪表声明的全部角色内容。</summary>
    void RegisterStaticReplicationContent()
    {
        ILocalPlayer local = SendQuery(new GetLocalPlayerQuery());
        if (local is PlayerController player && player.CharacterConfig != null)
            _content.RegisterPlayer(player.CharacterConfig);

        EnemySpawnController[] spawns = FindObjectsOfType<EnemySpawnController>();
        for (int i = 0; i < spawns.Length; i++)
        {
            _enemyDefinitions.Clear();
            spawns[i].CollectDefinitions(_enemyDefinitions);
            for (int c = 0; c < _enemyDefinitions.Count; c++)
                _content.RegisterEnemy(_enemyDefinitions[c]);
        }
    }

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

    /// <summary>单个远端玩家当前绑定的 ACT Gameplay 对象集合；连接生命周期归 Session。</summary>
    sealed class GuestSeat
    {
        public GuestSeat(
            NetConnectionId connectionId,
            RemotePlayerSeat seat,
            CharacterActor actor,
            SimActorRegistration registration,
            CharacterReactionService reactions,
            CharacterHurtboxTarget hurtbox,
            NetArchetypeId archetypeId)
        {
            ConnectionId = connectionId;
            Seat = seat;
            Actor = actor;
            Registration = registration;
            Reactions = reactions;
            Hurtbox = hurtbox;
            ArchetypeId = archetypeId;
        }

        /// <summary>Transport 本地作用域内的客机连接。</summary>
        public NetConnectionId ConnectionId { get; }
        public RemotePlayerSeat Seat { get; }
        public CharacterActor Actor { get; }
        public SimActorRegistration Registration { get; }
        public CharacterReactionService Reactions { get; }
        public CharacterHurtboxTarget Hurtbox { get; }
        /// <summary>Guest 复用 Host 玩家配置得到的稳定玩家网络原型。</summary>
        public NetArchetypeId ArchetypeId { get; }
        public long LastAppliedFrameHint { get; set; }

        /// <summary>本逻辑步真正灌入的最新 Hint；无新命令时下行 0，避免 CarryForward 用旧 Hint 错位纠偏。</summary>
        public long AppliedHintThisTick { get; set; }
    }

}
