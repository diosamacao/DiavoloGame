using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 客机房间：渲染帧合并输入、命令批上行、本机 Autonomous CharacterActor.Step、他人走 RemoteProxy。
/// 本机 Actor 由 PlayerController 装配；不进 World，不 Collect。卡肉由本机几何预测。
/// 他人/敌人 Proxy 登记进 TargetSystem，供本机 Targeting / CameraLock 只读选敌。
/// </summary>
[DefaultExecutionOrder(-150)]
[DisallowMultipleComponent]
public sealed class ReplicationRoomClient : AppControllerBase
{
    CombatWorldController _world;
    UdpReplicationTransport _transport;
    ActionReplicationCatalog _catalog;
    PlayerController _localPlayer;
    PredictedLocomotionDriver _driver;
    readonly PredictedActionAckQueue _actionAck = new();
    RoomIdleTracker _hostIdle;
    RoomJoinAccept _accept;
    bool _joined;
    bool _ended;
    long _predictFrame;
    long _lastAuthorityFrame = -1;
    long _nextHeartbeatMs;
    int _rttMs = -1;
    int _selfHealthMilli = -1;
    int _lastTickBytes = -1;
    int _lastCommandBytes = -1;
    ActorReplicationSnapshot _lastSelfSnapshot;
    bool _hasSelfSnapshot;
    InputFrameBuffer _inputFrames;

    readonly List<ClientCommand> _recentCommands = new();
    readonly HashSet<SimHitKey> _playedHits = new();
    readonly List<SimHitKey> _playedHitOrder = new();
    readonly Dictionary<int, RemoteCharacterProxy> _proxies = new();
    readonly HashSet<int> _seenIds = new();
    readonly List<int> _staleIds = new();
    readonly List<CharacterConfig> _enemyConfigs = new();
    readonly List<RemoteCharacterProxy> _softBlockers = new();
    SimVec2[] _softBlockerPosMm = Array.Empty<SimVec2>();
    int[] _softBlockerRadiiMm = Array.Empty<int>();
    int _lastPredictedActionId;
    int _lastPresentedHitStopFrames;

    /// <summary>由战斗世界在 Awake 注入。</summary>
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
        _hostIdle = new RoomIdleTracker();
        CollectEnemyConfigs();
        TryConnectAndJoin();
        RefreshHud("Connecting");
    }

    void Update()
    {
        // 传输未就绪或房间已结束：本渲染帧不再收包/采样
        if (_transport == null || _ended)
            return;

        // 把套接字收包泵进客机收件箱
        _transport.Pump();
        // 按消息类型处理 Accept/Reject/Tick/Kick
        DrainClientInbox();
        // 已入房：每渲染帧合并本机按键边沿，避免无逻辑步时丢掉 WasPressed
        if (_joined && !_ended)
            SampleRenderInput();
        // Host 心跳超时则关房
        if (_joined && _hostIdle.IsTimedOut(NowMs()))
            EndRoom("HostIdle");
    }

    void LateUpdate()
    {
        // 尚无战斗世界：本机与幽灵都还不能插值
        if (_world?.SimulationHost == null)
            return;

        // 与权威世界同一插值比例，避免本机与他人相位错开
        float alpha = _world.SimulationHost.InterpolationAlpha;
        // 本机预测体：逻辑 Pose → 表现锚点
        _localPlayer?.Actor?.Render(alpha);
        // 他人/敌人幽灵：快照 Pose → 表现锚点
        foreach (RemoteCharacterProxy proxy in _proxies.Values)
            proxy.Render(alpha);
    }

    void OnDisable() => UnsubscribeHost();

    void OnDestroy()
    {
        UnsubscribeHost();
        DisposeViews();
        _transport?.Dispose();
        _transport = null;
    }

    /// <summary>
    /// 用本机逻辑钟推进一帧预测并上行。按键边沿已在 Update 里合并进本帧，这里只 Resolve。
    /// </summary>
    void OnAfterLogicStep(long _)
    {
        if (!_joined || _ended || _localPlayer?.InputSampler == null)
            return;

        // Host 空闲检测用：定期上行心跳
        MaybeSendHeartbeat();
        EnsureInputBuffer();
        // 本机预测钟 +1；边沿已在 Update.SampleRenderInput 合并
        _predictFrame++;
        var actorId = new SimActorId(_accept.AssignedActorId);
        InputFrame input = _inputFrames.ResolveLocal(_predictFrame, actorId);
        _inputFrames.TrimBefore(_predictFrame - 32);

        // 最近 N 条命令冗余上行，供 Host 丢包补齐
        var command = new ClientCommand(_predictFrame, _accept.AssignedPlayerId, in input);
        RememberCommand(in command);
        byte[] payload = RoomCodec.WriteClientCommandBatch(_recentCommands);
        _lastCommandBytes = payload.Length;
        _transport.SendClientToAuthority(payload);

        CharacterActor actor = _localPlayer != null ? _localPlayer.Actor : null;
        if (!_hasSelfSnapshot || _driver == null || actor == null)
            return;

        // 本机 Autonomous：同一套 Actor.Step，不进权威 World
        float dt = _world.SimulationHost.FixedDeltaSeconds;
        actor.Step(_predictFrame, dt, in input);
        actor.ResolvePostCombat(_predictFrame);
        PresentPredictedHitStop(actor);
        ResolveAutonomousSoftBody(actor);

        int actionId = ResolveLocalActionId(actor);
        if (actionId != 0)
            _lastPredictedActionId = actionId;
        _actionAck.Record(_predictFrame, actionId);

        // 记下 SavedMove，权威包到达时 Replay
        _driver.RecordAutonomous(in input);
        RefreshHud("Joined");
    }

    /// <summary>每个渲染帧把 WasPressed 合并进下一逻辑帧，避免逻辑步之间的边沿永远丢失。</summary>
    void SampleRenderInput()
    {
        if (_localPlayer?.InputSampler == null || _accept.AssignedActorId <= 0)
            return;

        EnsureInputBuffer();
        var actorId = new SimActorId(_accept.AssignedActorId);
        // 写入「下一预测帧」槽；Pressed 与已有样本做 OR
        InputFrame sample = _localPlayer.InputSampler.Sample(_predictFrame + 1, actorId);
        _inputFrames.MergeLocalSample(in sample);
    }

    /// <summary>保留最近几条命令供下一包冗余重发。</summary>
    void RememberCommand(in ClientCommand command)
    {
        _recentCommands.Add(command);
        int max = ReplicationRoomProtocol.InputRedundancyCount;
        while (_recentCommands.Count > max)
            _recentCommands.RemoveAt(0);
    }

    void EnsureInputBuffer() => _inputFrames ??= new InputFrameBuffer();

    void TryConnectAndJoin()
    {
        string host = _world != null ? _world.JoinHost : "127.0.0.1";
        int port = _world != null ? _world.ListenPort : ReplicationRoomProtocol.DefaultPort;
        _transport = new UdpReplicationTransport();
        try
        {
            _transport.Connect(host, port);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ReplicationRoomClient: 连接 {host}:{port} 失败。{ex.Message}", this);
            _transport.Dispose();
            _transport = null;
            RefreshHud("ConnectFailed");
            return;
        }

        _localPlayer = SendQuery(new GetLocalPlayerQuery()) as PlayerController;
        if (_localPlayer != null)
            _catalog.Prefill(_localPlayer.CharacterConfig);
        for (int i = 0; i < _enemyConfigs.Count; i++)
            _catalog.Prefill(_enemyConfigs[i]);

        int contentVersion = _world != null ? _world.ContentVersion : 1;
        _transport.SendClientToAuthority(
            RoomCodec.WriteJoinRequest(
                new RoomJoinRequest(contentVersion, ReplicationRoomProtocol.ProtocolVersion)));
        Debug.Log($"ReplicationRoomClient: 已请求加入 {host}:{port}。", this);
    }

    void DrainClientInbox()
    {
        while (_transport.TryDequeueClient(out byte[] payload))
        {
            try
            {
                RoomCodec.ReadEnvelope(payload, out RoomMessageKind kind, out byte[] body);
                HandleMessage(kind, body);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ReplicationRoomClient: 丢弃非法包。{ex.Message}", this);
            }
        }
    }

    void HandleMessage(RoomMessageKind kind, byte[] body)
    {
        _hostIdle.Touch(NowMs());
        switch (kind)
        {
            case RoomMessageKind.JoinAccept:
                OnJoinAccept(RoomCodec.ReadJoinAccept(body));
                break;
            case RoomMessageKind.JoinReject:
                OnJoinReject(RoomCodec.ReadJoinReject(body));
                break;
            case RoomMessageKind.Heartbeat:
                OnHeartbeatEcho(RoomCodec.ReadHeartbeat(body));
                break;
            case RoomMessageKind.AuthorityTick:
                OnAuthorityTick(body);
                break;
            case RoomMessageKind.Kick:
                EndRoom($"Kicked:{RoomCodec.ReadKick(body).Reason}");
                break;
        }
    }

    void OnJoinAccept(in RoomJoinAccept accept)
    {
        if (_joined)
            return;

        _accept = accept;
        _joined = true;
        _lastPredictedActionId = 0;
        _predictFrame = accept.AuthorityFrame;
        _lastAuthorityFrame = accept.AuthorityFrame;
        EnsureInputBuffer();
        _recentCommands.Clear();
        _localPlayer = SendQuery(new GetLocalPlayerQuery()) as PlayerController;
        Debug.Log(
            $"ReplicationRoomClient: 入房成功 player={accept.AssignedPlayerId} actor={accept.AssignedActorId}。",
            this);
        RefreshHud("Joined");
    }

    void OnJoinReject(in RoomJoinReject reject)
    {
        Debug.LogWarning($"ReplicationRoomClient: 入房被拒 {reject.Reason}。", this);
        EndRoom($"Rejected:{reject.Reason}");
    }

    void OnHeartbeatEcho(in RoomHeartbeat heartbeat)
    {
        if (heartbeat.EchoTimeMs <= 0)
            return;
        int rtt = (int)Math.Max(0L, NowMs() - heartbeat.EchoTimeMs);
        _rttMs = rtt;
    }

    void OnAuthorityTick(byte[] body)
    {
        if (!_joined)
            return;

        // body 已由 RoomCodec 去掉两字节信封头；基线记录完整 UDP payload。
        _lastTickBytes = body != null ? body.Length + 2 : -1;
        RoomCodec.ReadAuthorityTickEnvelope(body, out long appliedHint, out byte[] tickBytes);
        AuthorityTick tick = ReplicationCodec.ReadAuthorityTick(tickBytes);
        _lastAuthorityFrame = tick.AuthorityFrame;
        ApplyRemoteActors(tick);

        if (TryFindSelf(tick, out ActorReplicationSnapshot self))
        {
            _lastSelfSnapshot = self;
            _hasSelfSnapshot = true;
            _selfHealthMilli = self.HealthMilli;
            EnsurePredictedDriver(in self);
            CharacterActor actor = _localPlayer != null ? _localPlayer.Actor : null;
            if (actor != null)
                actor.Vitality.ApplyAuthorityHealthMilli(self.HealthMilli);

            // 仅本步真正灌入的 Hint 才纠偏；CarryForward 下行 0，避免用旧预测位姿对当前权威
            if (appliedHint > 0 && actor != null && _driver != null)
            {
                PredictedActionReconcileResult actionResult = _actionAck.Reconcile(appliedHint, in self);
                if (PredictedActionAckQueue.ShouldStopAutonomousAction(actionResult, in self))
                    actor.StopAutonomousAction();

                // 穿敌吸附/关碰撞窗与权威卡肉：只 Ack，禁止 2m 硬吸把本机从敌后拽回。
                _catalog.TryGet(self.ActionId, out ActionDefinition authorityAction);
                PredictedReconcileResult loco = _driver.Reconcile(
                    appliedHint,
                    in self,
                    actor,
                    ActionMotionReconcileGate.ResolveSnapThresholdMm(
                        actor,
                        in self,
                        authorityAction));
                if (loco.Snapped)
                    actor.SnapPresentationToSimulation();
            }

            if (IsAuthorityHitOrDeath(in self) && actor != null)
            {
                ApplyAuthorityVitalityEdge(actor, in self);
                _driver?.SnapToSnapshot(in self);
                actor.SnapPresentationToSimulation();
            }

        }

        // 预测体就绪后再播火花，Owner 才能挂到攻击者根
        PlayReplicatedHits(tick);
        RefreshHud("Joined");
    }

    void ApplyRemoteActors(AuthorityTick tick)
    {
        _seenIds.Clear();
        for (int i = 0; i < tick.Actors.Length; i++)
        {
            ActorReplicationSnapshot snapshot = tick.Actors[i];
            if (!snapshot.ActorId.IsValid)
                continue;

            int id = snapshot.ActorId.Value;
            if (id == _accept.AssignedActorId)
                continue;

            _seenIds.Add(id);
            if (!TryGetOrCreateProxy(in snapshot, out RemoteCharacterProxy proxy))
                continue;
            proxy.ApplySnapshot(in snapshot);
        }

        DisposeMissingProxies();
    }

    bool TryGetOrCreateProxy(in ActorReplicationSnapshot snapshot, out RemoteCharacterProxy proxy)
    {
        int id = snapshot.ActorId.Value;
        if (_proxies.TryGetValue(id, out proxy))
            return true;

        CharacterConfig config = ResolveConfig(in snapshot);
        SimulationHost host = _world != null ? _world.SimulationHost : null;
        if (config == null || config.ModelPrefab == null || host == null)
        {
            proxy = null;
            return false;
        }

        proxy = RemoteCharacterProxyFactory.Create(
            config,
            _catalog,
            host.CollisionWorld,
            Vector3.zero,
            host.FixedDeltaSeconds,
            transform);
        _proxies[id] = proxy;
        GetSystem<TargetSystem>()?.Register(proxy);
        return true;
    }

    CharacterConfig ResolveConfig(in ActorReplicationSnapshot snapshot)
    {
        if (snapshot.Kind == ReplicationActorKind.Player)
            return _localPlayer != null ? _localPlayer.CharacterConfig : null;
        return _enemyConfigs.Count > 0 ? _enemyConfigs[0] : null;
    }

    /// <summary>首份 self 快照：绑定 ActorId、对齐位姿、建纠偏 Driver。不另建 Proxy。</summary>
    void EnsurePredictedDriver(in ActorReplicationSnapshot self)
    {
        if (_driver != null)
            return;
        if (_localPlayer == null || _localPlayer.CharacterConfig == null || _localPlayer.Actor == null)
            return;

        CharacterActor actor = _localPlayer.Actor;
        CharacterConfig config = _localPlayer.CharacterConfig;
        actor.BindSimulationInput(new SimActorId(_accept.AssignedActorId), _inputFrames);
        actor.MotorSim.TeleportMm(self.PosXMm, self.PosYMm, self.PosZMm);
        actor.MotorSim.SetFacingMilliDeg(self.FacingMilliDeg);
        // MotorSim 与 Transform 必须同帧对齐；只 Snap 插值会把 +2m 客机出生点拖进第一击位移
        actor.AlignSimulationRootToMotor();
        actor.SnapPresentationToSimulation();

        var predictConfig = new PredictedLocomotionConfig(
            MotionQuantization.MetersToMm(config.Motor.WalkSpeed),
            MotionQuantization.MetersToMm(config.Motor.RunSpeed),
            Mathf.RoundToInt(config.Motor.RunThreshold * 1000f),
            SimulationConfig.DefaultLogicHz,
            PredictedLocomotionConfig.Default.ReconcileThresholdMm,
            config.Motor.RotationSmoothTime,
            MotionQuantization.MetersToMm(config.Motor.SprintSpeed));
        _driver = new PredictedLocomotionDriver(actor.MotorSim, predictConfig);
    }

    /// <summary>
    /// 客机本机对只读幽灵做软弹开，避免走进敌人后再被权威 2m 纠偏拉回。
    /// 不进 World、不推幽灵。
    /// </summary>
    void ResolveAutonomousSoftBody(CharacterActor actor)
    {
        if (actor == null || !actor.ParticipatesInSoftBodySeparation)
            return;

        _softBlockers.Clear();
        foreach (RemoteCharacterProxy proxy in _proxies.Values)
        {
            if (proxy == null || !proxy.IsAlive || proxy.MotorSim == null)
                continue;
            _softBlockers.Add(proxy);
        }

        if (_softBlockers.Count == 0)
            return;

        _softBlockers.Sort(CompareProxyId);
        if (_softBlockerPosMm.Length < _softBlockers.Count)
        {
            _softBlockerPosMm = new SimVec2[_softBlockers.Count];
            _softBlockerRadiiMm = new int[_softBlockers.Count];
        }

        for (int i = 0; i < _softBlockers.Count; i++)
        {
            CharacterMotorSim motor = _softBlockers[i].MotorSim;
            _softBlockerPosMm[i] = motor.PositionMm;
            _softBlockerRadiiMm[i] = motor.RadiusMm;
        }

        if (AutonomousSoftBodySolver.TrySeparateLocal(
            actor.MotorSim,
            _softBlockerPosMm,
            _softBlockerRadiiMm,
            _softBlockers.Count))
        {
            actor.OnSoftBodySeparationApplied();
        }
    }

    static int CompareProxyId(RemoteCharacterProxy left, RemoteCharacterProxy right) =>
        left.SimulationId.Value.CompareTo(right.SimulationId.Value);

    /// <summary>本机 FreezeFrames 升高时同步刀光 VFX 卡肉；不发 AttackHitEvent。</summary>
    void PresentPredictedHitStop(CharacterActor actor)
    {
        if (actor?.ActionSim == null)
            return;

        int freeze = actor.ActionSim.FreezeFrames;
        if (freeze <= _lastPresentedHitStopFrames)
        {
            _lastPresentedHitStopFrames = freeze;
            return;
        }

        Transform attackerRoot = _localPlayer != null ? _localPlayer.Root : null;
        HitStopController hitStop = _world != null
            ? _world.GetComponent<HitStopController>()
            : null;
        hitStop?.PresentPredicted(attackerRoot, freeze);
        _lastPresentedHitStopFrames = freeze;
    }

    /// <summary>本机预测招 Catalog Id；无招为 0。</summary>
    int ResolveLocalActionId(CharacterActor actor)
    {
        if (actor?.ActionSim == null || !actor.ActionSim.IsActive)
            return 0;
        if (actor.ActionSim.Snapshot.Content is ActionDefinition definition)
            return _catalog.GetOrAdd(definition);
        return 0;
    }

    /// <summary>权威 Hit/Death 边沿写入本机状态机；不经 Pipeline。</summary>
    void ApplyAuthorityVitalityEdge(CharacterActor actor, in ActorReplicationSnapshot self)
    {
        CharacterConfig config = _localPlayer != null ? _localPlayer.CharacterConfig : null;
        var resolver = new CharacterReactionResolver(config != null ? config.Combat.Reactions : null);
        if (self.VitalityEdge == VitalityReplicationEdge.Death)
            actor.EnterDeath(resolver.ResolveDeath(default));
        else if (self.VitalityEdge == VitalityReplicationEdge.Hit)
            actor.EnterHit(resolver.ResolveHit(default));
    }

    /// <summary>受击/死亡才硬切位姿；普通出招不得走这条。</summary>
    static bool IsAuthorityHitOrDeath(in ActorReplicationSnapshot snapshot) =>
        snapshot.VitalityEdge == VitalityReplicationEdge.Hit
        || snapshot.VitalityEdge == VitalityReplicationEdge.Death;

    /// <summary>按复制落点播受击 Cue；同一 SimHitKey 只播一次。</summary>
    void PlayReplicatedHits(AuthorityTick tick)
    {
        if (tick.Hits == null || tick.Hits.Length == 0)
            return;

        for (int i = 0; i < tick.Hits.Length; i++)
        {
            ReplicatedHitEvent hit = tick.Hits[i];
            if (!RememberHit(hit.Key))
                continue;

            int actionId = hit.ActionId > 0
                ? hit.ActionId
                : ResolveActorActionId(tick, hit.Key.AttackerId);
            Transform attackerRoot = ResolveProxyRoot(hit.Key.AttackerId);
            HitImpactCuePlayer.TryPlay(
                _catalog,
                actionId,
                hit.Key.HitboxIndex,
                new Vector3(
                    MotionQuantization.MmToMeters(hit.HitXMm),
                    MotionQuantization.MmToMeters(hit.HitYMm),
                    MotionQuantization.MmToMeters(hit.HitZMm)),
                new Vector3(
                    MotionQuantization.MmToMeters(hit.DirXMm),
                    0f,
                    MotionQuantization.MmToMeters(hit.DirZMm)),
                attackerRoot);
        }
    }

    /// <summary>记下已播命中；超出窗口丢掉最旧键，避免 HashSet 无限涨。</summary>
    bool RememberHit(SimHitKey key)
    {
        if (!_playedHits.Add(key))
            return false;

        _playedHitOrder.Add(key);
        while (_playedHitOrder.Count > 128)
        {
            _playedHits.Remove(_playedHitOrder[0]);
            _playedHitOrder.RemoveAt(0);
        }

        return true;
    }

    static int ResolveActorActionId(AuthorityTick tick, SimActorId actorId)
    {
        if (!actorId.IsValid)
            return 0;
        for (int i = 0; i < tick.Actors.Length; i++)
        {
            if (tick.Actors[i].ActorId.Value == actorId.Value)
                return tick.Actors[i].ActionId;
        }

        return 0;
    }

    /// <summary>攻击者表现根，供火花卡肉挂 Owner；自己用预测体。</summary>
    Transform ResolveProxyRoot(SimActorId actorId)
    {
        if (!actorId.IsValid)
            return null;
        if (actorId.Value == _accept.AssignedActorId)
            return _localPlayer != null && _localPlayer.Actor != null
                ? _localPlayer.Actor.PresentationRoot
                : _localPlayer != null ? _localPlayer.Root : null;
        return _proxies.TryGetValue(actorId.Value, out RemoteCharacterProxy proxy)
            ? proxy.Root
            : null;
    }

    bool TryFindSelf(AuthorityTick tick, out ActorReplicationSnapshot self)
    {
        int id = _accept.AssignedActorId;
        for (int i = 0; i < tick.Actors.Length; i++)
        {
            if (tick.Actors[i].ActorId.Value == id)
            {
                self = tick.Actors[i];
                return true;
            }
        }

        self = default;
        return false;
    }

    void MaybeSendHeartbeat()
    {
        long now = NowMs();
        if (now < _nextHeartbeatMs)
            return;
        _nextHeartbeatMs = now + ReplicationRoomProtocol.HeartbeatIntervalMs;
        _transport.SendClientToAuthority(RoomCodec.WriteHeartbeat(new RoomHeartbeat(now, 0)));
    }

    void CollectEnemyConfigs()
    {
        _enemyConfigs.Clear();
        EnemySpawnController[] spawns = FindObjectsOfType<EnemySpawnController>();
        for (int i = 0; i < spawns.Length; i++)
            spawns[i].CollectCharacterConfigs(_enemyConfigs);
    }

    void DisposeMissingProxies()
    {
        _staleIds.Clear();
        foreach (int id in _proxies.Keys)
        {
            if (!_seenIds.Contains(id))
                _staleIds.Add(id);
        }

        for (int i = 0; i < _staleIds.Count; i++)
        {
            int id = _staleIds[i];
            if (_proxies.TryGetValue(id, out RemoteCharacterProxy proxy))
            {
                GetSystem<TargetSystem>()?.Unregister(proxy);
                proxy.Dispose();
            }
            _proxies.Remove(id);
        }
    }

    void DisposeViews()
    {
        _driver = null;
        TargetSystem targets = GetSystem<TargetSystem>();
        foreach (RemoteCharacterProxy proxy in _proxies.Values)
        {
            targets?.Unregister(proxy);
            proxy.Dispose();
        }
        _proxies.Clear();
    }

    void EndRoom(string status)
    {
        if (_ended)
            return;
        _ended = true;
        _joined = false;
        Debug.Log($"ReplicationRoomClient: 房间结束 {status}。", this);
        RefreshHud(status);
    }

    void RefreshHud(string status)
    {
        if (_world == null)
            return;
        _world.RoomHud = new ReplicationRoomHudInfo(
            true,
            ReplicationRole.Client,
            status,
            _lastAuthorityFrame,
            _rttMs,
            _selfHealthMilli,
            _lastTickBytes,
            _lastCommandBytes,
            _proxies.Count,
            (_driver?.PendingCount ?? 0) + _actionAck.PendingCount);
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
}
