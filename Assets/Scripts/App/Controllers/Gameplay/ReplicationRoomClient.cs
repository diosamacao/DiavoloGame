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
    ClientSession _session;
    ActionReplicationCatalog _catalog;
    ReplicationSchemaRegistry _schemaRegistry;
    ReplicationClient _replicationClient;
    CharacterSnapshotSchemaV1 _characterSchema;
    CharacterReplicationContentRegistry _content;
    PlayerController _localPlayer;
    PredictedLocomotionDriver _driver;
    readonly PredictedActionAckQueue _actionAck = new();
    SessionJoinAccept _accept;
    bool _joined;
    bool _ended;
    long _predictFrame;
    long _lastAuthorityFrame = -1;
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
    readonly List<EnemyDefinition> _enemyDefinitions = new();
    readonly List<RemoteCharacterProxy> _softBlockers = new();
    SimVec2[] _softBlockerPosMm = Array.Empty<SimVec2>();
    int[] _softBlockerRadiiMm = Array.Empty<int>();
    int _lastPredictedActionId;
    int _lastPresentedHitStopFrames;

    /// <summary>由 Composition Root 注入战斗世界与已启动的客户端 Session。</summary>
    public void Configure(CombatWorldController world, ClientSession session)
    {
        UnsubscribeHost();
        _world = world;
        _session = session;
        if (isActiveAndEnabled)
            SubscribeHost();
    }

    void OnEnable() => SubscribeHost();

    void Start()
    {
        _catalog = new ActionReplicationCatalog();
        _characterSchema = new CharacterSnapshotSchemaV1();
        _schemaRegistry = new ReplicationSchemaRegistry();
        _schemaRegistry.Register(_characterSchema);
        _replicationClient = new ReplicationClient(_schemaRegistry);
        _content = new CharacterReplicationContentRegistry();
        PrepareContentCatalog();
        RefreshHud("Connecting");
    }

    void Update()
    {
        // Session 未就绪或房间已结束：本渲染帧不再收包/采样
        if (_session == null || _ended)
            return;

        _session.Poll(NowMs());
        SyncSessionState();
        DrainApplicationMessages();
        // 已入房：每渲染帧合并本机按键边沿，避免无逻辑步时丢掉 WasPressed
        if (_joined && !_ended)
            SampleRenderInput();
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
        ResetReplicationState();
        _session?.Dispose();
        _session = null;
    }

    /// <summary>
    /// 用本机逻辑钟推进一帧预测并上行。按键边沿已在 Update 里合并进本帧，这里只 Resolve。
    /// </summary>
    void OnAfterLogicStep(long _)
    {
        if (!_joined || _ended || _localPlayer?.InputSampler == null)
            return;

        EnsureInputBuffer();
        // 本机预测钟 +1；边沿已在 Update.SampleRenderInput 合并
        _predictFrame++;
        var actorId = new SimActorId(_accept.EntityId.Value);
        InputFrame input = _inputFrames.ResolveLocal(_predictFrame, actorId);
        _inputFrames.TrimBefore(_predictFrame - 32);

        // 最近 N 条命令冗余上行，供 Host 丢包补齐
        var command = new ClientCommand(_predictFrame, _accept.PlayerId.Value, in input);
        RememberCommand(in command);
        byte[] body = RoomCodec.WriteClientCommandBatch(_recentCommands);
        _lastCommandBytes = body.Length + 2;
        _session.SendApplication(
            (byte)RoomMessageKind.ClientCommand,
            NetChannel.CommandUnreliableRedundant,
            body);

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
        if (_localPlayer?.InputSampler == null || !_accept.EntityId.IsValid)
            return;

        EnsureInputBuffer();
        var actorId = new SimActorId(_accept.EntityId.Value);
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

    /// <summary>对称登记本机与场景敌人内容，并预填动作目录。</summary>
    void PrepareContentCatalog()
    {
        _localPlayer = SendQuery(new GetLocalPlayerQuery()) as PlayerController;
        if (_localPlayer != null)
        {
            _catalog.Prefill(_localPlayer.CharacterConfig);
            _content.RegisterPlayer(_localPlayer.CharacterConfig);
        }

        EnemySpawnController[] spawns = FindObjectsOfType<EnemySpawnController>();
        for (int i = 0; i < spawns.Length; i++)
        {
            _enemyDefinitions.Clear();
            spawns[i].CollectDefinitions(_enemyDefinitions);
            for (int d = 0; d < _enemyDefinitions.Count; d++)
            {
                EnemyDefinition definition = _enemyDefinitions[d];
                _content.RegisterEnemy(definition);
                _catalog.Prefill(definition.CharacterConfig);
            }
        }
    }

    /// <summary>把纯 C# ClientSession 状态映射到本机预测体初始化与 HUD。</summary>
    void SyncSessionState()
    {
        if (!_joined && _session.State == ClientSessionState.Joined)
        {
            OnSessionJoined(_session.JoinAccept);
            return;
        }

        if (_session.State == ClientSessionState.Ended && !_ended)
            EndRoom($"SessionEnded:{_session.LastDisconnectReason}");
    }

    /// <summary>只消费 ClientSession 已鉴权并拆信封的 ReplicationFrame 应用消息。</summary>
    void DrainApplicationMessages()
    {
        while (_session.TryDequeueApplication(out SessionApplicationPacket packet))
        {
            if (packet.MessageType == (byte)RoomMessageKind.ReplicationFrame)
                OnReplicationFrame(packet.Payload);
        }
    }

    /// <summary>Session Join 成功后初始化预测时钟和本地玩家身份。</summary>
    void OnSessionJoined(in SessionJoinAccept accept)
    {
        if (_joined)
            return;

        _accept = accept;
        _joined = true;
        _lastPredictedActionId = 0;
        _predictFrame = accept.AuthorityTick.Value;
        _lastAuthorityFrame = accept.AuthorityTick.Value;
        EnsureInputBuffer();
        _recentCommands.Clear();
        _localPlayer = SendQuery(new GetLocalPlayerQuery()) as PlayerController;
        Debug.Log(
            $"ReplicationRoomClient: 入房成功 player={accept.PlayerId.Value} actor={accept.EntityId.Value}。",
            this);
        RefreshHud("Joined");
    }

    /// <summary>严格解码并原子应用一帧；任何协议或内容错误都会安全结束房间。</summary>
    void OnReplicationFrame(byte[] body)
    {
        if (!_joined)
            return;

        try
        {
            // body 已由 Session 去掉两字节信封头；HUD 仍记录完整应用消息字节。
            _lastTickBytes = body != null ? body.Length + 2 : -1;
            ReplicationFrame frame = ReplicationFrameCodec.Decode(body);
            ReplicationClientApplyResult result = _replicationClient.ApplyFrame(frame);
            if (result.Status == ReplicationClientApplyStatus.StaleSequence)
                return;
            if (result.Status == ReplicationClientApplyStatus.Rejected)
            {
                Debug.LogWarning($"ReplicationRoomClient: 复制帧被拒绝。{result.Message}", this);
                EndRoom("ReplicationRejected");
                return;
            }

            // 旧 Sequence 已在上方整帧丢弃；只为真正提交的帧解析并执行 ACT 业务副载荷。
            ActReplicationApplicationPayload application =
                ActReplicationApplicationPayloadCodec.Decode(frame.ApplicationPayload);
            ActorReplicationSnapshot self = default;
            bool hasSelf = false;
            ApplySpawns(result.Spawns, ref self, ref hasSelf);
            ApplyUpdates(result.Updates, ref self, ref hasSelf);
            if (!ApplyDespawns(result.Despawns))
                return;

            _lastAuthorityFrame = frame.Tick.Value;
            if (hasSelf)
                ApplyOwnerSnapshot(in self, application.AppliedClientFrameHint);
            PlayReplicatedHits(application.Hits);
            RefreshHud("Joined");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"ReplicationRoomClient: 非法复制帧。{ex.Message}", this);
            EndRoom("ReplicationInvalid");
        }
    }

    /// <summary>按顺序处理 Spawn；远端必须精准解析 Archetype，本机只记录最后一条快照。</summary>
    void ApplySpawns(
        SpawnRecord[] records,
        ref ActorReplicationSnapshot self,
        ref bool hasSelf)
    {
        for (int i = 0; i < records.Length; i++)
        {
            SpawnRecord record = records[i];
            ActorReplicationSnapshot snapshot = DecodeRecord(
                record.EntityId,
                record.SchemaId,
                record.Payload);
            ReplicationActorKind archetypeKind = _content.ResolveKind(record.ArchetypeId);
            if (archetypeKind != snapshot.Kind)
            {
                throw new InvalidOperationException(
                    $"Spawn {record.EntityId.Value} 的 Archetype Kind={archetypeKind} "
                    + $"与 Snapshot Kind={snapshot.Kind} 不一致。");
            }

            if (record.EntityId.Value == _accept.EntityId.Value)
            {
                if (snapshot.Kind != ReplicationActorKind.Player)
                    throw new InvalidOperationException("Owner Spawn 必须是 Player 原型。");
                self = snapshot;
                hasSelf = true;
                continue;
            }

            if (_proxies.ContainsKey(record.EntityId.Value))
                throw new InvalidOperationException($"远端实体 {record.EntityId.Value} 重复创建 Proxy。");

            CharacterConfig config = _content.ResolveCharacterConfig(record.ArchetypeId);
            RemoteCharacterProxy proxy = CreateProxy(config);
            _proxies.Add(record.EntityId.Value, proxy);
            GetSystem<TargetSystem>()?.Register(proxy);
            proxy.ApplySnapshot(in snapshot);
        }
    }

    /// <summary>按顺序处理 Update；未知远端实体明确失败，禁止静默创建默认 Proxy。</summary>
    void ApplyUpdates(
        EntityRecord[] records,
        ref ActorReplicationSnapshot self,
        ref bool hasSelf)
    {
        for (int i = 0; i < records.Length; i++)
        {
            EntityRecord record = records[i];
            ActorReplicationSnapshot snapshot = DecodeRecord(
                record.EntityId,
                record.SchemaId,
                record.Payload);
            if (record.EntityId.Value == _accept.EntityId.Value)
            {
                self = snapshot;
                hasSelf = true;
                continue;
            }

            if (!_proxies.TryGetValue(record.EntityId.Value, out RemoteCharacterProxy proxy))
            {
                throw new InvalidOperationException(
                    $"远端 Update {record.EntityId.Value} 没有已存在的 Proxy。");
            }
            proxy.ApplySnapshot(in snapshot);
        }
    }

    /// <summary>按显式 Despawn 精确释放单个 Proxy；Owner 被移除时立即结束房间。</summary>
    bool ApplyDespawns(DespawnRecord[] records)
    {
        for (int i = 0; i < records.Length; i++)
        {
            int id = records[i].EntityId.Value;
            if (id == _accept.EntityId.Value)
            {
                EndRoom("OwnerDespawned");
                return false;
            }

            if (!_proxies.TryGetValue(id, out RemoteCharacterProxy proxy))
                throw new InvalidOperationException($"远端 Despawn {id} 没有已存在的 Proxy。");

            GetSystem<TargetSystem>()?.Unregister(proxy);
            proxy.Dispose();
            _proxies.Remove(id);
        }

        return true;
    }

    /// <summary>校验 Schema 与 EntityId 后解码唯一角色快照布局。</summary>
    ActorReplicationSnapshot DecodeRecord(
        NetEntityId entityId,
        ushort schemaId,
        byte[] payload)
    {
        if (schemaId != CharacterSnapshotSchemaV1.Id)
            throw new InvalidOperationException($"角色实体 {entityId.Value} 使用未知 Schema {schemaId}。");

        ActorReplicationSnapshot snapshot = _characterSchema.DecodeSnapshot(payload);
        if (!snapshot.ActorId.IsValid || snapshot.ActorId.Value != entityId.Value)
        {
            throw new InvalidOperationException(
                $"角色记录 EntityId={entityId.Value} 与 Snapshot ActorId={snapshot.ActorId.Value} 不一致。");
        }
        return snapshot;
    }

    /// <summary>按精准 CharacterConfig 创建远端 Proxy；缺模型或战斗世界时明确失败。</summary>
    RemoteCharacterProxy CreateProxy(CharacterConfig config)
    {
        SimulationHost host = _world != null ? _world.SimulationHost : null;
        if (config == null || config.ModelPrefab == null || host == null)
            throw new InvalidOperationException("远端原型缺少 CharacterConfig、ModelPrefab 或 SimulationHost。");

        return RemoteCharacterProxyFactory.Create(
            config,
            _catalog,
            host.CollisionWorld,
            Vector3.zero,
            host.FixedDeltaSeconds,
            transform);
    }

    /// <summary>应用本帧最后一条 Owner 快照，并保持预测和解与受击硬吸语义。</summary>
    void ApplyOwnerSnapshot(
        in ActorReplicationSnapshot self,
        long appliedHint)
    {
        if (self.Kind != ReplicationActorKind.Player)
            throw new InvalidOperationException("Owner Snapshot Kind 必须为 Player。");

        _lastSelfSnapshot = self;
        _hasSelfSnapshot = true;
        _selfHealthMilli = self.HealthMilli;
        EnsurePredictedDriver(in self);
        CharacterActor actor = _localPlayer != null ? _localPlayer.Actor : null;
        if (actor != null)
            actor.Vitality.ApplyAuthorityHealthMilli(self.HealthMilli);

        // CarryForward hint=0 只更新状态，不拿旧预测位姿对当前权威帧做和解。
        if (appliedHint > 0 && actor != null && _driver != null)
        {
            PredictedActionReconcileResult actionResult = _actionAck.Reconcile(appliedHint, in self);
            if (PredictedActionAckQueue.ShouldStopAutonomousAction(actionResult, in self))
                actor.StopAutonomousAction();

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

    /// <summary>首份 self 快照：绑定 ActorId、对齐位姿、建纠偏 Driver。不另建 Proxy。</summary>
    void EnsurePredictedDriver(in ActorReplicationSnapshot self)
    {
        if (_driver != null)
            return;
        if (_localPlayer == null || _localPlayer.CharacterConfig == null || _localPlayer.Actor == null)
            return;

        CharacterActor actor = _localPlayer.Actor;
        CharacterConfig config = _localPlayer.CharacterConfig;
        actor.BindSimulationInput(new SimActorId(_accept.EntityId.Value), _inputFrames);
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
    void PlayReplicatedHits(ReplicatedHitEvent[] hits)
    {
        if (hits == null || hits.Length == 0)
            return;

        for (int i = 0; i < hits.Length; i++)
        {
            ReplicatedHitEvent hit = hits[i];
            if (!RememberHit(hit.Key))
                continue;

            Transform attackerRoot = ResolveProxyRoot(hit.Key.AttackerId);
            HitImpactCuePlayer.TryPlay(
                _catalog,
                hit.ActionId,
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

    /// <summary>攻击者表现根，供火花卡肉挂 Owner；自己用预测体。</summary>
    Transform ResolveProxyRoot(SimActorId actorId)
    {
        if (!actorId.IsValid)
            return null;
        if (actorId.Value == _accept.EntityId.Value)
            return _localPlayer != null && _localPlayer.Actor != null
                ? _localPlayer.Actor.PresentationRoot
                : _localPlayer != null ? _localPlayer.Root : null;
        return _proxies.TryGetValue(actorId.Value, out RemoteCharacterProxy proxy)
            ? proxy.Root
            : null;
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
        DisposeViews();
        ResetReplicationState();
        Debug.Log($"ReplicationRoomClient: 房间结束 {status}。", this);
        RefreshHud(status);
    }

    /// <summary>丢弃客户端实体注册表与 Schema 状态，避免结束房间后残留生命周期。</summary>
    void ResetReplicationState()
    {
        _replicationClient = null;
        _schemaRegistry = null;
        _characterSchema = null;
        _content = null;
        _hasSelfSnapshot = false;
        _lastSelfSnapshot = default;
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
            _session?.RttMs ?? -1,
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
