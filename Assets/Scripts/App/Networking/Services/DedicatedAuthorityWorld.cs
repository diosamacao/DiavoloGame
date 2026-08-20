using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Dedicated 权威世界：Headless Actor、命令合并进下一权威帧、外部时钟步进与每连接构帧。</summary>
public sealed class DedicatedAuthorityWorld : IDedicatedAuthorityWorld
{
    readonly SimulationHost _host;
    readonly ActContentRegistry _content;
    readonly ActGameSessionHandler _gameSession;
    readonly ActAuthorityReplicationAdapter _authority;
    readonly ServerSimulationRunner _runner;
    readonly Dictionary<NetConnectionId, ActGameGuest> _guests = new();
    readonly Dictionary<NetConnectionId, ReplicationServer> _replicationByConnection = new();
    readonly List<ActGameGuest> _guestSnapshot = new();
    readonly List<DedicatedReplicationSend> _outbound = new();
    readonly List<DedicatedEventSend> _outboundEvents = new();
    readonly HashSet<NetConnectionId> _pendingJoinSnapshots = new();
    bool _disposed;

    /// <summary>绑定已关闭自动 Tick 的 SimulationHost 与场景内容 Registry。</summary>
    public DedicatedAuthorityWorld(
        SimulationHost host,
        ACTGameArchitecture architecture,
        ActContentRegistry content)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _content = content ?? throw new ArgumentNullException(nameof(content));
        if (architecture == null)
            throw new ArgumentNullException(nameof(architecture));

        _host.DriveFromExternalClock = true;
        _host.AfterLogicStep += OnAfterLogicStep;
        _gameSession = new ActGameSessionHandler(content, CreateServices(architecture));
        _authority = new ActAuthorityReplicationAdapter(content);
        var kernel = new SimulationStepKernel();
        _runner = new ServerSimulationRunner(kernel, StepOnce);
    }

    /// <inheritdoc />
    public long CurrentFrame => _host.CurrentFrame;

    /// <summary>最近一次 Runner 指标，供测试读取 overrun。</summary>
    public SimulationTickMetrics TickMetrics => _runner.Metrics;

    /// <inheritdoc />
    public bool TryAcceptPlayer(in MatchPlayerSlot slot, out NetEntityId entityId)
    {
        entityId = NetEntityId.Invalid;
        if (!_content.TryGetAnyPlayerConfig(out CharacterConfig config)
            && slot.ArchetypeId.IsValid)
        {
            try
            {
                config = _content.ResolveCharacterConfig(slot.ArchetypeId);
            }
            catch (KeyNotFoundException)
            {
                config = null;
            }
        }

        MatchSpawnPose spawn = slot.Spawn;
        if (config == null
            || !_gameSession.TryCreateGuest(
                config,
                spawn,
                _host,
                slot.ConnectionId,
                prefillEnemyCatalog: null,
                out ActGameGuest guest,
                CharacterPresentationMode.AuthorityHeadless)
            || guest.Actor == null
            || !guest.Actor.SimulationId.IsValid)
        {
            return false;
        }

        // 新连接必须从完整 Spawn 开始，禁止继承上一连接 Registry。
        _replicationByConnection[slot.ConnectionId] = new ReplicationServer();
        _guests[slot.ConnectionId] = guest;
        _pendingJoinSnapshots.Add(slot.ConnectionId);
        entityId = new NetEntityId(guest.Actor.SimulationId.Value);
        return true;
    }

    /// <inheritdoc />
    public void ApplyCommands(NetConnectionId connectionId, ClientCommand[] commands)
    {
        if (!_guests.TryGetValue(connectionId, out ActGameGuest guest)
            || guest.Actor == null
            || _host.World == null)
        {
            return;
        }

        ActAuthorityInputApplyResult result = _authority.ApplyGuestCommands(
            _host.World.InputFrames,
            _host.CurrentFrame,
            guest.Actor.SimulationId,
            commands,
            guest.LastAppliedFrameHint);
        if (!result.Applied)
            return;

        guest.LastAppliedFrameHint = result.NewestHint;
        guest.AppliedHintThisTick = result.FirstAppliedHint;
    }

    /// <inheritdoc />
    public void RemovePlayer(NetConnectionId connectionId)
    {
        if (_guests.TryGetValue(connectionId, out ActGameGuest guest))
        {
            _guests.Remove(connectionId);
            _gameSession.DestroyGuest(guest, _host);
        }

        _replicationByConnection.Remove(connectionId);
        _pendingJoinSnapshots.Remove(connectionId);
    }

    /// <inheritdoc />
    public void Advance(long nowMs)
    {
        _host.SampleRenderInputs();
        _runner.Advance(nowMs);
        _host.PublishExternalInterpolationAlpha(_runner.InterpolationAlpha);
    }

    /// <inheritdoc />
    public int PeekAdvanceSteps(long nowMs) => _runner.PeekAdvanceSteps(nowMs);

    /// <inheritdoc />
    public float InterpolationAlpha => _runner.InterpolationAlpha;

    /// <inheritdoc />
    public void PublishImmediateReplication()
    {
        if (_pendingJoinSnapshots.Count == 0)
            return;

        EnqueueFrames(_host.CurrentFrame, _pendingJoinSnapshots);
        _pendingJoinSnapshots.Clear();
    }

    /// <inheritdoc />
    public void DrainOutboundReplication(List<DedicatedReplicationSend> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));

        results.Clear();
        for (int i = 0; i < _outbound.Count; i++)
            results.Add(_outbound[i]);
        _outbound.Clear();
    }

    /// <inheritdoc />
    public void DrainOutboundEvents(List<DedicatedEventSend> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));

        results.Clear();
        for (int i = 0; i < _outboundEvents.Count; i++)
            results.Add(_outboundEvents[i]);
        _outboundEvents.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _host.AfterLogicStep -= OnAfterLogicStep;
        var ids = new List<NetConnectionId>(_guests.Keys);
        for (int i = 0; i < ids.Count; i++)
            RemovePlayer(ids[i]);
        _outbound.Clear();
        _outboundEvents.Clear();
    }

    /// <summary>与 Listen Host 同一帧序：BeginFrame → Step → 结算 → PostCombat → 构帧。</summary>
    void StepOnce() => _host.StepOnce();

    /// <summary>在 FrameHits 清空前 Capture 并按连接差分；命中改走可靠事件单轨。</summary>
    void OnAfterLogicStep(long authorityFrame)
    {
        if (_guests.Count == 0)
            return;
        EnqueueFrames(authorityFrame, connections: null);
    }

    /// <summary>
    /// 为指定连接或全部 Guest 编一帧。尚未步进时 Tick 用 0，保证 Join 同拍有 Spawn。
    /// </summary>
    void EnqueueFrames(long authorityFrame, HashSet<NetConnectionId> connections)
    {
        CopyGuests(_guestSnapshot);
        _authority.CaptureAuthorityActors(_guestSnapshot, _host);
        ReplicatedHitEvent[] hits = _authority.CopyHits(_host.FrameHits);
        if (connections == null)
            EnqueueHitEvents(hits);
        long tick = authorityFrame < 0 ? 0 : authorityFrame;

        foreach (KeyValuePair<NetConnectionId, ActGameGuest> pair in _guests)
        {
            if (connections != null && !connections.Contains(pair.Key))
                continue;
            if (!_replicationByConnection.TryGetValue(pair.Key, out ReplicationServer replication)
                || pair.Value?.Actor == null
                || !pair.Value.Actor.SimulationId.IsValid)
            {
                continue;
            }

            long appliedHint = pair.Value.AppliedHintThisTick;
            pair.Value.AppliedHintThisTick = 0;
            byte[] applicationBytes = ActReplicationApplicationPayloadCodec.Encode(
                new ActReplicationApplicationPayload(appliedHint, null));
            ReplicationFrame frame = replication.BuildFrame(
                new NetTick(tick),
                _authority.EntityStates,
                applicationBytes);
            _outbound.Add(new DedicatedReplicationSend(
                pair.Key,
                ReplicationFrameCodec.Encode(frame)));
            if (frame.Sequence.Value == 0)
            {
                Debug.Log(
                    $"DedicatedAuthorityWorld: 首帧 Spawn connection={pair.Key} "
                    + $"entity={pair.Value.Actor.SimulationId.Value} tick={tick} "
                    + $"entities={_authority.EntityStates.Count}。");
            }
        }
    }

    /// <summary>本帧命中按连接各发一份可靠事件；不含历史窗口。</summary>
    void EnqueueHitEvents(ReplicatedHitEvent[] hits)
    {
        if (hits == null || hits.Length == 0)
            return;

        byte[] body = ActReplicationEventCodec.Encode(hits);
        foreach (NetConnectionId connectionId in _guests.Keys)
        {
            if (!_replicationByConnection.ContainsKey(connectionId))
                continue;
            _outboundEvents.Add(new DedicatedEventSend(connectionId, body));
        }
    }

    void CopyGuests(List<ActGameGuest> results)
    {
        results.Clear();
        foreach (ActGameGuest guest in _guests.Values)
            results.Add(guest);
    }

    static ActGameSessionServices CreateServices(ACTGameArchitecture architecture)
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
