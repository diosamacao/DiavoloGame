using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Client 房间的 ACT Gameplay 编排：Owner 输入预测、Observer 生命周期与本地命中表现。</summary>
public sealed class ActClientRoomGameplay
{
    readonly CombatWorldController _world;
    readonly ActContentRegistry _content;
    readonly ActContentPrefillService _contentPrefill;
    readonly ActOwnerReplicationAdapter _owner;
    readonly ActObserverReplicationAdapter _observer;
    readonly ReplicationClient _replicationClient;
    readonly List<ClientCommand> _recentCommands = new();
    readonly HashSet<SimHitKey> _playedHits = new();
    readonly List<SimHitKey> _playedHitOrder = new();
    readonly List<RemoteCharacterProxy> _softBlockers = new();
    readonly List<SpawnRecord> _appliedSpawns = new();
    readonly List<EntityRecord> _appliedUpdates = new();
    readonly List<DespawnRecord> _appliedDespawns = new();
    readonly NetworkTimeEstimator _clock = new();
    readonly ActReplicationEventCodec.OwnerAssistParryEventQueue _assistParryEvents = new();

    PlayerController _localPlayer;
    SessionJoinAccept _accept;
    InputFrameBuffer _inputFrames;
    SimActorId[] _ownerPartyActorIds = Array.Empty<SimActorId>();
    SimVec2[] _softBlockerPosMm = Array.Empty<SimVec2>();
    int[] _softBlockerRadiiMm = Array.Empty<int>();
    long _predictFrame;
    int _lastPresentedHitStopFrames;
    InputFrame _pendingPredictionInput;
    bool _hasPendingPredictionStep;
    bool _loggedOwnerPredict;
    ActReplicationSnapshotMeta _appliedMeta;
    bool _metadataPublishedThisCall;
    bool _authorityRosterReady;

    /// <summary>创建 Client Gameplay 唯一编排入口，并装配内容、Schema、Owner 与 Observer。</summary>
    public ActClientRoomGameplay(
        CombatWorldController world,
        ACTGameArchitecture architecture,
        Transform proxyParent)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        if (architecture == null)
            throw new ArgumentNullException(nameof(architecture));

        _content = new ActContentRegistry();
        _contentPrefill = new ActContentPrefillService(architecture, _content);
        var characterSchema = new ActCharacterSnapshotSchema(_content);
        var schemaRegistry = new ReplicationSchemaRegistry();
        schemaRegistry.Register(characterSchema);
        _replicationClient = new ReplicationClient(schemaRegistry);
        _replicationClient.Spawned += record => _appliedSpawns.Add(record);
        _replicationClient.Updated += (record, _) => _appliedUpdates.Add(record);
        _replicationClient.Despawned += record => _appliedDespawns.Add(record);
        _replicationClient.MetadataApplied += (bytes, _) =>
        {
            _appliedMeta = ActReplicationSnapshotMetaCodec.Decode(bytes);
            _metadataPublishedThisCall = true;
        };
        _replicationClient.RecoveryRequired += reason => LastRejectMessage = reason;
        _owner = new ActOwnerReplicationAdapter(_content);
        _observer = new ActObserverReplicationAdapter(
            _content,
            characterSchema,
            () => _world.SimulationHost,
            proxyParent,
            target => architecture.GetSystem<TargetSystem>()?.Register(target),
            target => architecture.GetSystem<TargetSystem>()?.Unregister(target));
        _contentPrefill.InitializeFromScene();
        _localPlayer = _contentPrefill.LocalPlayer;
    }

    /// <summary>最近成功应用的权威帧；尚未入房时为 -1。</summary>
    public long LastAuthorityFrame { get; private set; } = -1;

    /// <summary>最近完整下行应用消息字节；尚未收到时为 -1。</summary>
    public int LastTickBytes { get; private set; } = -1;

    /// <summary>最近完整上行命令消息字节；尚未发送时为 -1。</summary>
    public int LastCommandBytes { get; private set; } = -1;

    /// <summary>Owner 最近权威生命值。</summary>
    public int SelfHealthMilli => _owner.SelfHealthMilli;

    /// <summary>当前 Observer Proxy 数量。</summary>
    public int ProxyCount => _observer.Count;

    /// <summary>按权威 Id 取 Observer 可见体；Listen 无头敌人的 Playable 在这里。</summary>
    public bool TryGetProxy(SimActorId actorId, out RemoteCharacterProxy proxy) =>
        _observer.TryGetProxy(actorId, out proxy);

    /// <summary>Owner 尚未确认的动作与位移预测总数。</summary>
    public int PredictionPendingCount => _owner.PendingCount;

    /// <summary>走跑 Restore 次数。</summary>
    public int PredictionSnapCount => _owner.LocomotionSnapCount;

    /// <summary>走跑 Replay 命令累计。</summary>
    public int PredictionReplayCount => _owner.LocomotionReplayCount;

    /// <summary>远端网络插值延迟毫秒；Listen 无 RTT 报 0。播放头仍用 1 tick 做表现插值。</summary>
    public int InterpolationDelayMs =>
        _world.Role == ReplicationRole.ListenHost ? 0 : _clock.InterpolationDelayMs;

    /// <summary>最近一次 ReplicationClient 拒绝原因。</summary>
    public string LastRejectMessage { get; private set; }

    /// <summary>Session Join 后初始化 Owner 身份、输入历史与预测时钟。</summary>
    public void BeginSession(in SessionJoinAccept accept)
    {
        // PlayerController 可能晚于 Room Start 登记；Join 时再做一次幂等内容扫描。
        _contentPrefill.InitializeFromScene();
        _accept = accept;
        _predictFrame = accept.AuthorityTick.Value;
        LastAuthorityFrame = accept.AuthorityTick.Value;
        EnsureInputBuffer();
        _owner.BeginSession(new SimActorId(accept.EntityId.Value), _inputFrames);
        _ownerPartyActorIds = new[] { new SimActorId(accept.EntityId.Value) };
        _recentCommands.Clear();
        _appliedMeta = null;
        _assistParryEvents.Clear();
        _authorityRosterReady = false;
        _localPlayer = _contentPrefill.LocalPlayer;
        _loggedOwnerPredict = false;
    }

    /// <summary>渲染帧采样下一预测帧输入，并合并按钮边沿。</summary>
    public void SampleRenderInput()
    {
        if (_localPlayer?.InputSampler == null || !_accept.EntityId.IsValid)
            return;

        EnsureInputBuffer();
        var actorId = new SimActorId(_accept.EntityId.Value);
        InputFrame sample = _localPlayer.InputSampler.Sample(_predictFrame + 1, actorId);
        _inputFrames.MergeLocalSample(in sample);
    }

    /// <summary>解析下一预测帧输入并生成冗余 ClientCommand 正文；调用方必须先发送再调用 StepPrediction。</summary>
    public bool TryBuildCommand(out byte[] commandBody)
    {
        commandBody = null;
        _hasPendingPredictionStep = false;
        if (_localPlayer?.InputSampler == null)
            return false;

        EnsureInputBuffer();
        _predictFrame++;
        var actorId = new SimActorId(_accept.EntityId.Value);
        InputFrame input = _inputFrames.ResolveLocal(_predictFrame, actorId);
        _inputFrames.TrimBefore(_predictFrame - 32);

        var command = new ClientCommand(_predictFrame, _accept.PlayerId.Value, in input);
        RememberCommand(in command);
        commandBody = RoomCodec.WriteClientCommandBatch(_recentCommands);
        LastCommandBytes = commandBody.Length + 2;
        _pendingPredictionInput = input;
        _hasPendingPredictionStep = true;
        return true;
    }

    /// <summary>在命令正文已发送后推进本机 Autonomous Actor，并记录 ACK/Replay 历史。</summary>
    public void StepPrediction()
    {
        if (!_hasPendingPredictionStep)
            return;
        _hasPendingPredictionStep = false;
        CharacterActor actor = _localPlayer.Actor;
        if (!_owner.CanPredict || actor == null)
            return;

        float dt = _world.SimulationHost.FixedDeltaSeconds;
        _localPlayer.StepPartyPrediction(_predictFrame, dt, in _pendingPredictionInput);
        actor = _localPlayer.Actor;
        int activeSlot = _localPlayer.ActivePartySlot;
        if (activeSlot >= 0 && activeSlot < _ownerPartyActorIds.Length)
            _owner.SetActiveOwnerActor(_ownerPartyActorIds[activeSlot]);
        PresentPredictedHitStop(actor);
        ResolveAutonomousSoftBody(actor);
        _owner.RecordAutonomous(actor, _predictFrame, in _pendingPredictionInput);
    }

    /// <summary>应用可靠 V2 生命周期；重复 Spawn/Despawn 不重复创建或销毁。</summary>
    public ActClientReplicationApplyStatus ApplyReplicationLifecycle(byte[] body)
    {
        LastTickBytes = body != null ? body.Length + 2 : -1;
        _appliedSpawns.Clear();
        _appliedUpdates.Clear();
        _appliedDespawns.Clear();
        _metadataPublishedThisCall = false;
        ReplicationLifecycle lifecycle = ReplicationProtocolV2Codec.DecodeLifecycle(body);
        if (!_replicationClient.ApplyLifecycle(lifecycle))
            return ActClientReplicationApplyStatus.Rejected;
        SimActorId ownerId = ResolveCurrentOwnerId();
        ActorReplicationSnapshot self = default;
        bool hasSelf = false;
        _observer.ApplySpawns(
            _appliedSpawns.ToArray(),
            _ownerPartyActorIds,
            ownerId,
            lifecycle.Tick.Value,
            ApplyOwnerPartySnapshot,
            ref self,
            ref hasSelf);
        if (!_observer.ApplyDespawns(_appliedDespawns.ToArray(), _ownerPartyActorIds, ownerId))
            return ActClientReplicationApplyStatus.OwnerDespawned;
        // ApplyLifecycle 会同步释放已越过屏障的 Snapshot；必须在同次调用完成玩法侧纠正。
        if (_metadataPublishedThisCall)
            ApplyCollectedAuthorityState(_replicationClient.LatestSnapshotTick);
        return ActClientReplicationApplyStatus.Applied;
    }

    /// <summary>应用或缓冲 V2 快照，并在 Meta 后执行 Owner/Observer 权威纠正。</summary>
    public ActClientReplicationApplyStatus ApplyReplicationSnapshot(byte[] body)
    {
        LastTickBytes = body != null ? body.Length + 2 : -1;
        _appliedUpdates.Clear();
        _metadataPublishedThisCall = false;
        ReplicationSnapshot snapshot = ReplicationProtocolV2Codec.DecodeSnapshot(body);
        ReplicationSnapshotApplyResult applied = _replicationClient.ApplySnapshot(snapshot);
        if (_replicationClient.RecoveryRequested)
            return ActClientReplicationApplyStatus.Rejected;
        if (applied == ReplicationSnapshotApplyResult.Buffered)
            return ActClientReplicationApplyStatus.Buffered;
        // 同 Tick 多批只发布一次 Meta，后续批复用该 Tick 已验证的同一份 Meta。
        if (_appliedMeta == null)
            throw new InvalidOperationException("V2 Snapshot 缺少 ACT Meta。");

        ApplyCollectedAuthorityState(snapshot.Tick.Value);
        if (!_loggedOwnerPredict && _owner.CanPredict)
        {
            _loggedOwnerPredict = true;
            Debug.Log(
                $"ActClientRoomGameplay: Owner 预测已开闸 tick={LastAuthorityFrame} "
                + $"actor={_accept.EntityId.Value}。");
        }

        return ActClientReplicationApplyStatus.Applied;
    }

    /// <summary>把本次 ReplicationClient 已发布的 Meta/Update 原子落到 Owner 与 Observer。</summary>
    void ApplyCollectedAuthorityState(long authorityTick)
    {
        ApplyAuthorityMeta(_appliedMeta);
        SimActorId ownerId = ResolveCurrentOwnerId();
        ActorReplicationSnapshot self = default;
        bool hasSelf = false;
        _clock.ObserveAuthorityTick(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), authorityTick);
        _observer.ApplyUpdates(
            _appliedUpdates.ToArray(),
            _ownerPartyActorIds,
            ownerId,
            authorityTick,
            ApplyOwnerPartySnapshot,
            ref self,
            ref hasSelf);
        LastAuthorityFrame = authorityTick;
        if (hasSelf)
            // ACK 使用累计值；单拍 Meta 丢失后仍能在后续 Snapshot 收敛预测历史。
            _owner.ApplySnapshot(_localPlayer, in self, _appliedMeta.LastAppliedClientFrameHint);
    }

    /// <summary>应用 Party ids/flags、ActiveSlot 与 PartyWiped 的权威 Meta。</summary>
    void ApplyAuthorityMeta(ActReplicationSnapshotMeta meta)
    {
        SimActorId[] ids = meta.PartyActorIds;
        int[] flags = meta.PartyFlags;
        if (_localPlayer == null || ids.Length != _localPlayer.PartyActors.Count)
            throw new InvalidOperationException("V2 Meta 阵容与本机 Loadout 不一致。");
        _ownerPartyActorIds = ids;
        _localPlayer.BindPartySimulationInput(ids, _inputFrames);
        for (int i = 0; i < ids.Length; i++)
            _localPlayer.SynchronizeAuthorityPartyState(ids[i], flags[i]);
        if (meta.PartyWiped)
        {
            _localPlayer.SynchronizeAuthorityPartyWiped();
        }
        else
        {
            _localPlayer.SynchronizeAuthorityActiveSlot(meta.ActivePartySlot, meta.LastAppliedClientFrameHint);
            _owner.SetActiveOwnerActor(ids[meta.ActivePartySlot]);
        }

        _authorityRosterReady = true;
        // 可靠 Event 与 Snapshot 分通道到达；身份绑定完成后重试早到的 Owner 弹刀接触。
        _assistParryEvents.Retry(_ownerPartyActorIds, TryApplyOwnerAssistParry);
    }

    /// <summary>返回当前活动 Owner；队灭时退回 Session Owner 仅用于幂等 Despawn 判断。</summary>
    SimActorId ResolveCurrentOwnerId()
    {
        if (_appliedMeta != null && !_appliedMeta.PartyWiped)
            return _appliedMeta.PartyActorIds[_appliedMeta.ActivePartySlot];
        return _accept.EntityId.IsValid ? new SimActorId(_accept.EntityId.Value) : SimActorId.Invalid;
    }

    /// <summary>把每个 Owner 槽快照中的 FlagsPacked 阵容状态同步到本地预测镜像。</summary>
    void ApplyOwnerPartySnapshot(ActorReplicationSnapshot snapshot)
    {
        _localPlayer?.SynchronizeAuthorityPartyState(
            snapshot.ActorId,
            snapshot.FlagsPacked);
    }

    /// <summary>应用可靠命中事件；按 SimHitKey 只播一次。</summary>
    public void ApplyReplicationEvents(byte[] body)
    {
        ReplicatedHitEvent[] hits = ActReplicationEventCodec.Decode(body);
        PlayReplicatedHits(hits);
    }

    /// <summary>用最近心跳 RTT 刷新插值延迟。</summary>
    public void ObserveNetworkSample(int rttMs)
    {
        if (rttMs >= 0)
            _clock.ObserveRtt(rttMs);
    }

    /// <summary>
    /// Owner 跟本地固定步 alpha。
    /// Observer 一律 RemotePlaybackClock：Listen 只留 1 tick 表现插值（delay=0 会贴死最新快照）。
    /// </summary>
    public void Render()
    {
        SimulationHost host = _world.SimulationHost;
        if (host == null)
            return;

        float alpha = host.InterpolationAlpha;
        _localPlayer?.RenderParty(alpha);
        int delayTicks = _world.Role == ReplicationRole.ListenHost
            ? 1
            : _clock.InterpolationDelayTicks;
        _observer.Render(delayTicks, Time.deltaTime);
    }

    /// <summary>丢掉 Observer / Registry，保留 Session，等待权威下一帧全量 Spawn。</summary>
    public void ResetReplicationForRecovery()
    {
        _observer.DisposeViews();
        _owner.Reset();
        _replicationClient.ResetForRecovery();
        _assistParryEvents.Clear();
        _authorityRosterReady = false;
        _loggedOwnerPredict = false;
    }

    /// <summary>注销并释放全部 Observer View 与 Owner 预测状态。</summary>
    public void Shutdown()
    {
        _observer.DisposeViews();
        _owner.Reset();
        _recentCommands.Clear();
        _playedHits.Clear();
        _playedHitOrder.Clear();
        _assistParryEvents.Clear();
        _authorityRosterReady = false;
    }

    /// <summary>保留最近若干命令，供下一应用包冗余重发。</summary>
    void RememberCommand(in ClientCommand command)
    {
        _recentCommands.Add(command);
        int max = ReplicationRoomProtocol.InputRedundancyCount;
        while (_recentCommands.Count > max)
            _recentCommands.RemoveAt(0);
    }

    void EnsureInputBuffer() => _inputFrames ??= new InputFrameBuffer();

    /// <summary>本机对只读 Observer 做单向软弹开，不把 Proxy 推回权威世界。</summary>
    void ResolveAutonomousSoftBody(CharacterActor actor)
    {
        if (actor == null || !actor.ParticipatesInSoftBodySeparation)
            return;

        _softBlockers.Clear();
        foreach (RemoteCharacterProxy proxy in _observer.Proxies)
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

    /// <summary>本机 FreezeFrames 升高时同步 VFX 卡肉；不发布 AttackHitEvent。</summary>
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

        HitStopController hitStop = _world.GetComponent<HitStopController>();
        hitStop?.PresentPredicted(_localPlayer != null ? _localPlayer.Root : null, freeze);
        _lastPresentedHitStopFrames = freeze;
    }

    /// <summary>按权威落点播 Cue；Flinch 时对 Proxy 叠 Additive，不写 ActionSim。</summary>
    void PlayReplicatedHits(ReplicatedHitEvent[] hits)
    {
        if (hits == null || hits.Length == 0)
            return;

        for (int i = 0; i < hits.Length; i++)
        {
            ReplicatedHitEvent hit = hits[i];
            if (hit.AbsorbedByAssistParry)
            {
                // 该事件没有足够的 Observer Action 身份；只做 Owner 接触，不编造第二套动作状态。
                _assistParryEvents.ApplyOrPend(
                    in hit,
                    _ownerPartyActorIds,
                    _authorityRosterReady,
                    TryApplyOwnerAssistParry);
                continue;
            }

            if (!RememberHit(hit.Key))
                continue;

            Transform attackerRoot = ResolveProxyRoot(hit.Key.AttackerId);
            HitImpactCuePlayer.TryPlay(
                _content.Actions,
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

            if (_observer.TryGetProxy(hit.Key.TargetId, out RemoteCharacterProxy proxy))
            {
                proxy.NotifyReplicatedReaction(hit.ReactionKind);
                if (hit.ReactionKind == HitReactionKind.Flinch)
                {
                    HitFlinchPlaybackController flinchPlayback =
                        _world.GetComponent<HitFlinchPlaybackController>();
                    if (flinchPlayback != null)
                        flinchPlayback.TryPlayOnProxy(proxy, AnimationKey.HitShake);
                    else
                        HitFlinchPresentation.TryPlayOnProxy(
                            proxy,
                            AnimationKey.HitShake,
                            fallbackClip: null,
                            fallbackMask: null,
                            fadeDuration: 0.05f);
                }
            }
        }
    }

    /// <summary>按 PartyActors 稳定 SimulationId 应用弹刀；支持本体招架与切人后的非当前槽。</summary>
    bool TryApplyOwnerAssistParry(ReplicatedHitEvent hit)
    {
        IReadOnlyList<CharacterActor> party = _localPlayer?.PartyActors;
        if (party == null)
            return false;
        for (int i = 0; i < party.Count; i++)
        {
            CharacterActor actor = party[i];
            if (actor == null || actor.SimulationId != hit.Key.TargetId)
                continue;

            actor.NotifyAssistParryContact();
            actor.ArmAssistParryHitStopCarry(hit.HitStopFrames);
            return true;
        }
        return false;
    }

    /// <summary>记录已播放命中并限制去重窗口大小。</summary>
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

    /// <summary>解析攻击者表现根：Owner 使用预测体，Observer 使用 Proxy 根。</summary>
    Transform ResolveProxyRoot(SimActorId actorId)
    {
        if (!actorId.IsValid)
            return null;
        for (int i = 0; i < _ownerPartyActorIds.Length; i++)
        {
            if (_ownerPartyActorIds[i] != actorId)
                continue;
            CharacterActor member = _localPlayer != null
                && i < _localPlayer.PartyActors.Count
                    ? _localPlayer.PartyActors[i]
                    : null;
            return member?.PresentationRoot != null
                ? member.PresentationRoot
                : _localPlayer != null ? _localPlayer.Root : null;
        }
        return _observer.TryGetProxy(actorId, out RemoteCharacterProxy proxy)
            ? proxy.Root
            : null;
    }
}
