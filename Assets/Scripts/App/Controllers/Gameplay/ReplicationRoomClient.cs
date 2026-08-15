using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 客机房间：渲染帧合并输入、命令批上行、本机预测位移/出招、他人与敌人走 RemoteProxy。
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
    PredictedActionDriver _actionDriver;
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
    ActorReplicationSnapshot _lastSelfSnapshot;
    bool _hasSelfSnapshot;
    InputFrame _lastPredictedInput;
    InputFrameBuffer _inputFrames;
    LocomotionGaitPolicy _gaitPolicy;
    LocomotionGait _predictedGait = LocomotionGait.Walk;
    float _runHoldSeconds;

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

        if (!_hasSelfSnapshot || _driver == null)
            return;

        bool hasMove = HasMoveIntent(in input);
        TickPredictedGait(in input, hasMove);
        bool align = ReplicationPresentationAlign.ShouldAlignFromSnapshot(in _lastSelfSnapshot);
        if (align)
            _driver.PredictAlignedToSnapshot(in input, in _lastSelfSnapshot);
        else
        {
            PredictedLocomotionConfig predictConfig = _driver.Config;
            int speedMm = hasMove
                ? PredictedLocomotionVisual.SpeedMmForGait(_predictedGait, in predictConfig)
                : 0;
            _driver.Predict(in input, speedMm);
        }

        _lastPredictedInput = input;
        PredictLocalAction(in input, align);

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
                _driver.Reconcile(appliedHint, in self);
                _actionDriver.Reconcile(appliedHint, in self);
            }

            bool align = ReplicationPresentationAlign.ShouldAlignFromSnapshot(in self);
            if (align)
                _driver.SnapToSnapshot(in self);

            // 权威已换招（连招下一段）时立刻跟 Clip，不要卡在本地第一段
            if (self.ActionId != 0
                && (!_actionDriver.IsActive || self.ActionId != _actionDriver.ActionId))
                _actionDriver.Predict(_predictFrame, self.ActionId, self.ActionFrame);

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
        if (_predictedView != null && _driver != null)
            return;
        if (_localPlayer == null || _localPlayer.CharacterConfig == null || _world?.SimulationHost == null)
            return;

        CharacterConfig config = _localPlayer.CharacterConfig;
        SimulationHost host = _world.SimulationHost;
        var predictMotor = new CharacterMotorSim(
            host.CollisionWorld,
            MotionQuantization.MetersToMm(config.Motor.ControllerRadius),
            config.Motor.SoftBodyMass,
            config.Motor.SoftBodyImmovable,
            SimulationConfig.DefaultLogicHz,
            MotionQuantization.MetersToMm(config.Motor.Gravity),
            MotionQuantization.MetersToMm(config.Motor.GroundedGravity));
        predictMotor.TeleportMm(self.PosXMm, self.PosYMm, self.PosZMm);
        predictMotor.SetFacingMilliDeg(self.FacingMilliDeg);

        var predictConfig = new PredictedLocomotionConfig(
            MotionQuantization.MetersToMm(config.Motor.WalkSpeed),
            MotionQuantization.MetersToMm(config.Motor.RunSpeed),
            Mathf.RoundToInt(config.Motor.RunThreshold * 1000f),
            SimulationConfig.DefaultLogicHz,
            PredictedLocomotionConfig.Default.ReconcileThresholdMm,
            config.Motor.RotationSmoothTime,
            MotionQuantization.MetersToMm(config.Motor.SprintSpeed));
        _driver = new PredictedLocomotionDriver(predictMotor, predictConfig);
        _actionDriver = new PredictedActionDriver();
        _gaitPolicy = ResolveGaitPolicy(config);
        _predictedGait = LocomotionGait.Walk;
        _runHoldSeconds = 0f;
        _predictedView = RemoteCharacterProxyFactory.Create(
            config,
            _catalog,
            host.CollisionWorld,
            Vector3.zero,
            host.FixedDeltaSeconds,
            _world.transform);
        _localPlayer.BindPredictedView(_predictedView);
    }

    /// <summary>
    /// 本机出招用预测 ActionFrame；Locomotion 选片走 PredictedLocomotionVisual，
    /// 禁止再用摇杆硬切 Idle/Walk/Run 盖掉 Sprint/Stop。
    /// </summary>
    void ApplyPredictedVisual()
    {
        if (_predictedView == null || !_hasSelfSnapshot)
            return;

        ActorReplicationSnapshot visual = _lastSelfSnapshot.WithMotorPose(_driver.Motor);
        if (_actionDriver.IsActive)
        {
            visual = visual.WithAction(
                _actionDriver.ActionId,
                _actionDriver.ActionFrame,
                _lastSelfSnapshot.FreezeFrames);
            _predictedView.ApplySnapshot(in visual, leanRollDegrees: 0f, seekLocomotion: false);
            return;
        }

        if (_lastSelfSnapshot.ActionId != 0)
        {
            visual = visual.WithAction(
                _lastSelfSnapshot.ActionId,
                _lastSelfSnapshot.ActionFrame,
                _lastSelfSnapshot.FreezeFrames);
            _predictedView.ApplySnapshot(in visual, leanRollDegrees: 0f, seekLocomotion: true);
            return;
        }

        bool hasMove = HasMoveIntent(in _lastPredictedInput);
        AnimationKey key = PredictedLocomotionVisual.ResolveSelfKey(
            in _lastSelfSnapshot,
            hasMove,
            _predictedGait);
        bool seekTransition = PredictedLocomotionVisual.IsTransitionPhase(key);
        visual = visual.WithAction(0, 0).WithLocomotion(
            (byte)key,
            seekTransition ? _lastSelfSnapshot.LocomotionNormalizedMilli : (ushort)0);
        _predictedView.ApplySnapshot(in visual, leanRollDegrees: 0f, seekLocomotion: seekTransition);
    }

    /// <summary>用与权威相同的 GaitPolicy 累计 Run 保持，满秒进 Sprint。</summary>
    void TickPredictedGait(in InputFrame input, bool hasMove)
    {
        _gaitPolicy ??= new LocomotionGaitPolicy();
        if (!hasMove)
        {
            _runHoldSeconds = 0f;
            return;
        }

        double mag01 = Math.Min(
            1.0,
            Math.Sqrt((int)input.MoveX * input.MoveX + (int)input.MoveY * input.MoveY)
            / InputQuantizer.AxisScale);
        GaitPolicyResult result = _gaitPolicy.Evaluate(
            new GaitPolicyInput(
                _predictedGait,
                (float)mag01,
                _driver.Config.RunThresholdMilli / 1000f,
                1f / Math.Max(1, _driver.Config.LogicHz),
                _runHoldSeconds));
        _predictedGait = result.NextGait;
        _runHoldSeconds = result.RunHoldSeconds;
    }

    static LocomotionGaitPolicy ResolveGaitPolicy(CharacterConfig config)
    {
        if (config?.CombatProfile != null
            && config.CombatProfile.TryGetLocomotionProfile(
                config.CombatProfile.DefaultMode,
                out CharacterLocomotionProfile profile)
            && profile.GaitPolicy != null)
            return profile.GaitPolicy;
        return new LocomotionGaitPolicy();
    }

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

    /// <summary>本机出招预测：权威已有招则跟；否则 Attack 边沿用默认 Graph Entry。</summary>
    void PredictLocalAction(in InputFrame input, bool align)
    {
        if (align && _lastSelfSnapshot.ActionId != 0)
        {
            _actionDriver.Predict(
                _predictFrame,
                _lastSelfSnapshot.ActionId,
                _lastSelfSnapshot.ActionFrame);
            return;
        }

        if (_actionDriver.IsActive)
        {
            _actionDriver.TickUnconfirmed(_predictFrame);
            return;
        }

        if (input.WasPressed(InputButton.Attack) && TryResolvePredictedAttackId(out int actionId))
        {
            _actionDriver.Predict(_predictFrame, actionId, 0);
            return;
        }

        _actionDriver.Predict(_predictFrame, 0, 0);
    }

    /// <summary>取默认模式 Graph 上第一条 Attack Entry，供客机立刻播 Clip。</summary>
    bool TryResolvePredictedAttackId(out int actionId)
    {
        actionId = 0;
        CharacterConfig config = _localPlayer != null ? _localPlayer.CharacterConfig : null;
        if (config?.CombatProfile == null
            || !config.CombatProfile.TryGetActionGraph(
                config.CombatProfile.DefaultMode,
                out ActionGraph graph))
            return false;

        IReadOnlyList<ActionGraphNode> nodes = graph.Nodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            ActionGraphNode node = nodes[i];
            if (node == null || !node.IsEntry || node.Action == null)
                continue;
            if (node.Intent != GameplayIntentType.Attack)
                continue;

            actionId = _catalog.GetOrAdd(node.Action);
            return actionId > 0;
        }

        return false;
    }

    /// <summary>与预测位移同一套死区，判断本帧是否有移动意图。</summary>
    static bool HasMoveIntent(in InputFrame input)
    {
        int magSq = (int)input.MoveX * input.MoveX + (int)input.MoveY * input.MoveY;
        return magSq >= PredictedLocomotionMath.MoveIntentMagnitudeSqMin;
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
