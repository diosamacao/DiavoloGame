using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 客机房间：渲染帧合并输入、命令批上行、本机 Autonomous 走跑 + 出招预测、他人与敌人走 RemoteProxy。
/// 不创建权威 CharacterActor，不刷怪，不 Collect。
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
    AutonomousActionRunner _actionRunner;
    AutonomousLocomotionRunner _locomotionRunner;
    RemoteCharacterProxy _predictedView;
    RoomIdleTracker _hostIdle;
    RoomJoinAccept _accept;
    bool _joined;
    bool _ended;
    long _predictFrame;
    long _lastAuthorityFrame = -1;
    long _nextHeartbeatMs;
    int _rttMs = -1;
    int _selfHealthMilli = -1;
    int _lastPresentedActionId;
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
        if (_transport == null || _ended)
            return;

        _transport.Pump();
        DrainClientInbox();
        if (_joined && !_ended)
            SampleRenderInput();
        if (_joined && _hostIdle.IsTimedOut(NowMs()))
            EndRoom("HostIdle");
    }

    void LateUpdate()
    {
        if (_world?.SimulationHost == null)
            return;

        float alpha = _world.SimulationHost.InterpolationAlpha;
        _predictedView?.Render(alpha);
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

        MaybeSendHeartbeat();
        EnsureInputBuffer();
        _predictFrame++;
        var actorId = new SimActorId(_accept.AssignedActorId);
        InputFrame input = _inputFrames.ResolveLocal(_predictFrame, actorId);
        _inputFrames.TrimBefore(_predictFrame - 32);

        var command = new ClientCommand(_predictFrame, _accept.AssignedPlayerId, in input);
        RememberCommand(in command);
        _transport.SendClientToAuthority(RoomCodec.WriteClientCommandBatch(_recentCommands));

        if (!_hasSelfSnapshot || _driver == null || _locomotionRunner == null || _actionRunner == null)
            return;

        // 权威卡肉时本机不推 ActionSim，避免 Clip 暂停、解冻一次派多段 VFX。
        _actionRunner.Tick(in input, _predictFrame, _lastSelfSnapshot.FreezeFrames > 0);
        if (_actionRunner.LastActionId != 0)
            _lastPresentedActionId = _actionRunner.LastActionId;
        if (!_actionRunner.IsActive && _lastSelfSnapshot.ActionId == 0)
            _actionRunner.NotifyAuthorityIdle();
        if (ShouldPresentAction())
        {
            _locomotionRunner.Exit();
            _driver.PredictAlignedToSnapshot(in input, in _lastSelfSnapshot);
        }
        else
        {
            LocomotionResumeRequest resume = default;
            if (!_locomotionRunner.IsActive)
                resume = ResolveResumeAfterAction();
            _locomotionRunner.Tick(in input, in resume);
            _driver.RecordAutonomous(in input);
        }

        ApplyPredictedVisual();
        RefreshHud("Joined");
    }

    /// <summary>每个渲染帧把 WasPressed 合并进下一逻辑帧，避免逻辑步之间的边沿永远丢失。</summary>
    void SampleRenderInput()
    {
        if (_localPlayer?.InputSampler == null || _accept.AssignedActorId <= 0)
            return;

        EnsureInputBuffer();
        var actorId = new SimActorId(_accept.AssignedActorId);
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

        RoomCodec.ReadAuthorityTickEnvelope(body, out long appliedHint, out byte[] tickBytes);
        AuthorityTick tick = ReplicationCodec.ReadAuthorityTick(tickBytes);
        _lastAuthorityFrame = tick.AuthorityFrame;
        ApplyRemoteActors(tick);

        if (TryFindSelf(tick, out ActorReplicationSnapshot self))
        {
            _lastSelfSnapshot = self;
            _hasSelfSnapshot = true;
            _selfHealthMilli = self.HealthMilli;
            EnsurePredictedView(in self);

            // 仅本步真正灌入的 Hint 才纠偏；CarryForward 下行 0，避免用旧预测位姿对当前权威
            if (appliedHint > 0)
            {
                // 走跑带 Runner：Driver 默认 2m 硬吸，不得再传 50mm。
                PredictedReconcileResult loco = _driver.Reconcile(appliedHint, in self, _locomotionRunner);
                _actionRunner?.Reconcile(appliedHint, in self);
                // 逻辑根已回拉时必须掐断表现插值，否则会把纠偏扫成一顿。
                if (loco.Snapped)
                    _predictedView?.SnapPresentationToSimulation();
            }

            // 出招中只停走跑，位姿交给 AfterLogicStep 插值；每包硬切会掐死闪避位移和相机。
            if (IsAuthorityHitOrDeath(in self))
            {
                _locomotionRunner?.Exit();
                _actionRunner?.Stop(followAuthority: true);
                _driver.SnapToSnapshot(in self);
                _predictedView?.SnapPresentationToSimulation();
            }
            else if (self.ActionId != 0)
                _locomotionRunner?.Exit();

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
        return true;
    }

    CharacterConfig ResolveConfig(in ActorReplicationSnapshot snapshot)
    {
        if (snapshot.Kind == ReplicationActorKind.Player)
            return _localPlayer != null ? _localPlayer.CharacterConfig : null;
        return _enemyConfigs.Count > 0 ? _enemyConfigs[0] : null;
    }

    void EnsurePredictedView(in ActorReplicationSnapshot self)
    {
        if (_predictedView != null && _driver != null && _locomotionRunner != null && _actionRunner != null)
            return;
        if (_localPlayer == null || _localPlayer.CharacterConfig == null || _world?.SimulationHost == null)
            return;

        CharacterConfig config = _localPlayer.CharacterConfig;
        SimulationHost host = _world.SimulationHost;
        AutonomousPredictedSeat seat = RemoteCharacterProxyFactory.CreateAutonomous(
            config,
            _catalog,
            host.CollisionWorld,
            Vector3.zero,
            host.FixedDeltaSeconds,
            _world.transform);
        _predictedView = seat.Proxy;
        _locomotionRunner = seat.Runner;
        _actionRunner = seat.Action;
        _predictedView.MotorSim.TeleportMm(self.PosXMm, self.PosYMm, self.PosZMm);
        _predictedView.MotorSim.SetFacingMilliDeg(self.FacingMilliDeg);

        var predictConfig = new PredictedLocomotionConfig(
            MotionQuantization.MetersToMm(config.Motor.WalkSpeed),
            MotionQuantization.MetersToMm(config.Motor.RunSpeed),
            Mathf.RoundToInt(config.Motor.RunThreshold * 1000f),
            SimulationConfig.DefaultLogicHz,
            PredictedLocomotionConfig.Default.ReconcileThresholdMm,
            config.Motor.RotationSmoothTime,
            MotionQuantization.MetersToMm(config.Motor.SprintSpeed));
        _driver = new PredictedLocomotionDriver(_predictedView.MotorSim, predictConfig);
        _localPlayer.BindPredictedView(_predictedView);
    }

    /// <summary>
    /// 出招/受击仍走 Proxy Seek；走跑只 Sync Motor/Lean，片子由 Runner 推进。
    /// 本机预测招自然结束后禁止用延迟快照再 Seek/派特效。
    /// </summary>
    void ApplyPredictedVisual()
    {
        if (_predictedView == null || !_hasSelfSnapshot)
            return;

        ActorReplicationSnapshot visual = _lastSelfSnapshot.WithMotorPose(_driver.Motor);
        if (_actionRunner != null && _actionRunner.IsActive)
        {
            visual = visual.WithAction(
                _actionRunner.ActionId,
                _actionRunner.ActionFrame,
                _lastSelfSnapshot.FreezeFrames);
            _predictedView.ApplySnapshot(in visual, leanRollDegrees: 0f, seekLocomotion: false);
            return;
        }

        if (ShouldApplyAuthorityAction())
        {
            visual = visual.WithAction(
                _lastSelfSnapshot.ActionId,
                _lastSelfSnapshot.ActionFrame,
                _lastSelfSnapshot.FreezeFrames);
            _predictedView.ApplySnapshot(in visual, leanRollDegrees: 0f, seekLocomotion: true);
            return;
        }

        _predictedView.SyncAutonomousLocomotion(
            _locomotionRunner != null ? _locomotionRunner.LeanRollDegrees : 0f,
            _locomotionRunner != null ? _locomotionRunner.DebugWishWorld : Vector3.zero);
    }

    /// <summary>本机应停走跑、改播出招或受击。自然结束的预测招不得被延迟权威招拖住。</summary>
    bool ShouldPresentAction() =>
        _actionRunner != null && _actionRunner.IsActive
        || ShouldApplyAuthorityAction();

    /// <summary>受击、从未预测、或和解真取消后才跟权威招；本机打完则忽略延迟 ActionId。</summary>
    bool ShouldApplyAuthorityAction() =>
        PredictedActionAckQueue.ShouldPresentAuthorityAction(
            _actionRunner != null && _actionRunner.IsActive,
            _actionRunner != null && _actionRunner.SuppressStaleAuthorityAction,
            IsAuthorityHitOrDeath(in _lastSelfSnapshot),
            _lastSelfSnapshot.ActionId);

    /// <summary>闪避结束与 Host 一样 SprintAfterDodge；其它招用权威步态跳过 Start。</summary>
    LocomotionResumeRequest ResolveResumeAfterAction()
    {
        CombatActionType lastAction = CombatActionType.Attack;
        if (_lastPresentedActionId != 0
            && _catalog != null
            && _catalog.TryGet(_lastPresentedActionId, out ActionDefinition definition)
            && definition != null)
            lastAction = definition.ActionType;

        LocomotionGait gait = LocomotionGait.Walk;
        if (_hasSelfSnapshot
            && System.Enum.IsDefined(typeof(LocomotionGait), (int)_lastSelfSnapshot.Gait))
            gait = (LocomotionGait)_lastSelfSnapshot.Gait;

        return LocomotionResumeRequest.AfterAction(lastAction, gait);
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
            return _predictedView?.Root;
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
                proxy.Dispose();
            _proxies.Remove(id);
        }
    }

    void DisposeViews()
    {
        if (_localPlayer != null)
            _localPlayer.BindPredictedView(null);
        _predictedView?.Dispose();
        _predictedView = null;
        _locomotionRunner = null;
        _driver = null;
        _actionRunner = null;
        foreach (RemoteCharacterProxy proxy in _proxies.Values)
            proxy.Dispose();
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
            _selfHealthMilli);
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
