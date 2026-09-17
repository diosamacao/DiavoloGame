using System;
using System.Collections.Generic;

/// <summary>Dedicated 复制发布器：独占逐连接 ReplicationServer、Prepare/Commit 队列与可靠命中事件。</summary>
public sealed class AuthorityReplicationPublisher : IDisposable
{
    readonly SimulationHost _host;
    readonly AuthorityGuestRegistry _guests;
    readonly ActAuthorityReplicationAdapter _authority;
    readonly Dictionary<NetConnectionId, ReplicationServer> _replicationByConnection = new();
    readonly List<ActGameGuest> _guestSnapshot = new();
    readonly List<DedicatedReplicationSend> _outbound = new();
    readonly List<DedicatedEventSend> _outboundEvents = new();
    readonly HashSet<NetConnectionId> _pendingJoinSnapshots = new();
    readonly HashSet<NetConnectionId> _pendingFullRecovery = new();
    readonly List<ReplicationEntityState> _relevantStates = new();
    int _bodyBudget = TransportMtuGate.DefaultMaxDatagramBytes
        - TransportMtuGate.HeaderBytes
        - SessionCodec.EnvelopeHeaderBytes;

    /// <summary>绑定权威 Host、Guest 注册表与角色 Capture 适配器。</summary>
    public AuthorityReplicationPublisher(
        SimulationHost host,
        AuthorityGuestRegistry guests,
        ActAuthorityReplicationAdapter authority)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _guests = guests ?? throw new ArgumentNullException(nameof(guests));
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
    }

    /// <summary>为新连接创建独立复制基线，并排队 Join 同拍全量 Spawn。</summary>
    public void RegisterConnection(NetConnectionId connectionId)
    {
        _replicationByConnection[connectionId] = new ReplicationServer();
        _pendingJoinSnapshots.Add(connectionId);
    }

    /// <summary>移除连接的复制基线、恢复请求与待发包。</summary>
    public void RemoveConnection(NetConnectionId connectionId)
    {
        RejectQueuedFor(connectionId);
        _replicationByConnection.Remove(connectionId);
        _pendingJoinSnapshots.Remove(connectionId);
        _pendingFullRecovery.Remove(connectionId);
    }

    /// <summary>为尚未下发 Spawn 的连接立即准备当前帧完整状态。</summary>
    public void PublishImmediate()
    {
        if (_pendingJoinSnapshots.Count == 0)
            return;
        PublishFrame(_host.CurrentFrame, _pendingJoinSnapshots);
        _pendingJoinSnapshots.Clear();
    }

    /// <summary>Capture 当前权威状态并为全部连接准备差分帧。</summary>
    public void PublishFrame(long authorityFrame) =>
        PublishFrame(authorityFrame, connections: null);

    /// <summary>取出本拍准备好的复制正文并清空内部队列。</summary>
    public void DrainReplication(List<DedicatedReplicationSend> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));
        results.Clear();
        for (int i = 0; i < _outbound.Count; i++)
            results.Add(_outbound[i]);
        _outbound.Clear();
    }

    /// <summary>取出本拍可靠命中事件并清空内部队列。</summary>
    public void DrainEvents(List<DedicatedEventSend> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));
        results.Clear();
        for (int i = 0; i < _outboundEvents.Count; i++)
            results.Add(_outboundEvents[i]);
        _outboundEvents.Clear();
    }

    /// <summary>配置已扣除 Transport 与 Session 外壳的 App 正文预算。</summary>
    public void ConfigureBodyBudget(int bodyBudgetBytes)
    {
        if (bodyBudgetBytes < ReplicationProtocolV2Codec.SnapshotHeaderBytes)
            throw new ArgumentOutOfRangeException(nameof(bodyBudgetBytes));
        _bodyBudget = bodyBudgetBytes;
    }

    /// <summary>发送成功后提交指定连接的复制基线票据。</summary>
    public void Commit(NetConnectionId connectionId, ReplicationPacketCommitToken token)
    {
        if (_replicationByConnection.TryGetValue(connectionId, out ReplicationServer server))
            server.Commit(token);
    }

    /// <summary>发送失败后拒绝指定连接票据，保留基线供后续重试。</summary>
    public void Reject(NetConnectionId connectionId, ReplicationPacketCommitToken token)
    {
        if (_replicationByConnection.TryGetValue(connectionId, out ReplicationServer server))
            server.Reject(token);
    }

    /// <summary>要求指定连接下一帧重置 baseline 并重新 Spawn。</summary>
    public void RequestFullRecovery(NetConnectionId connectionId)
    {
        if (_replicationByConnection.ContainsKey(connectionId))
            _pendingFullRecovery.Add(connectionId);
    }

    /// <summary>拒绝未发送票据并清空全部连接级复制状态。</summary>
    public void Dispose()
    {
        RejectSupersededReplication();
        _outboundEvents.Clear();
        _replicationByConnection.Clear();
        _pendingJoinSnapshots.Clear();
        _pendingFullRecovery.Clear();
    }

    /// <summary>为指定连接集合或全部 Guest 编帧；Join/恢复强制完整 Spawn。</summary>
    void PublishFrame(long authorityFrame, HashSet<NetConnectionId> connections)
    {
        // 一个 Poll 追赶多逻辑步时只保留最后一份未发送完整状态。
        RejectSupersededReplication();
        _guests.CopyTo(_guestSnapshot);
        _authority.CaptureAuthorityActors(_guestSnapshot, _host);
        ReplicatedHitEvent[] hits = _authority.CopyHits(_host.FrameHits);
        if (connections == null)
            EnqueueHitEvents(hits);
        long tick = authorityFrame < 0 ? 0 : authorityFrame;

        foreach (KeyValuePair<NetConnectionId, ActGameGuest> pair in _guests.Entries)
        {
            if (connections != null && !connections.Contains(pair.Key))
                continue;
            if (!_replicationByConnection.TryGetValue(pair.Key, out ReplicationServer replication)
                || pair.Value == null)
            {
                continue;
            }

            long appliedHint = pair.Value.AppliedHintThisTick;
            pair.Value.AppliedHintThisTick = 0;
            SimActorId[] partyIds = pair.Value.CopyPartyActorIds();
            byte[] metadata = ActReplicationSnapshotMetaCodec.Encode(
                new ActReplicationSnapshotMeta(
                    appliedHint,
                    pair.Value.LastAppliedFrameHint,
                    partyIds,
                    pair.Value.CopyPartyFlags(),
                    pair.Value.Coordinator.ActiveIndex,
                    pair.Value.Coordinator.IsPartyWiped));
            bool forceFull = connections != null
                || _pendingFullRecovery.Remove(pair.Key);
            SimActorId observerId = ResolveObserverId(pair.Value.Actor, partyIds);
            _authority.CopyRelevantStates(
                observerId,
                ReplicationInterest.DefaultRadiusMm,
                _relevantStates);
            ReplicationTickDelta delta = replication.PrepareTickDelta(
                new NetTick(tick),
                _relevantStates,
                metadata,
                _bodyBudget,
                new NetEntityId(observerId.Value),
                forceFull);
            for (int i = 0; i < delta.Packets.Length; i++)
            {
                PreparedReplicationPacket packet = delta.Packets[i];
                _outbound.Add(new DedicatedReplicationSend(
                    pair.Key,
                    packet.ReliableLifecycle,
                    packet.Body,
                    packet.Token));
            }
        }
    }

    /// <summary>拒绝本 Poll 内已被更新状态覆盖的全部准备包。</summary>
    void RejectSupersededReplication()
    {
        for (int i = 0; i < _outbound.Count; i++)
        {
            DedicatedReplicationSend send = _outbound[i];
            if (_replicationByConnection.TryGetValue(send.ConnectionId, out ReplicationServer server))
                server.Reject(send.Token);
        }
        _outbound.Clear();
    }

    /// <summary>移除连接前拒绝它仍在队列中的票据，并保留其他连接待发包。</summary>
    void RejectQueuedFor(NetConnectionId connectionId)
    {
        for (int i = _outbound.Count - 1; i >= 0; i--)
        {
            DedicatedReplicationSend send = _outbound[i];
            if (send.ConnectionId != connectionId)
                continue;
            if (_replicationByConnection.TryGetValue(connectionId, out ReplicationServer server))
                server.Reject(send.Token);
            _outbound.RemoveAt(i);
        }
        for (int i = _outboundEvents.Count - 1; i >= 0; i--)
            if (_outboundEvents[i].ConnectionId == connectionId)
                _outboundEvents.RemoveAt(i);
    }

    /// <summary>把本帧命中作为可靠事件逐连接排队，不携带历史窗口。</summary>
    void EnqueueHitEvents(ReplicatedHitEvent[] hits)
    {
        if (hits == null || hits.Length == 0)
            return;
        byte[] body = ActReplicationEventCodec.Encode(hits);
        foreach (KeyValuePair<NetConnectionId, ActGameGuest> pair in _guests.Entries)
            if (_replicationByConnection.ContainsKey(pair.Key))
                _outboundEvents.Add(new DedicatedEventSend(pair.Key, body));
    }

    /// <summary>队灭后 Active 为空时使用首个阵容实体维持兴趣与最终 Meta 下发。</summary>
    static SimActorId ResolveObserverId(CharacterActor active, SimActorId[] partyIds)
    {
        if (active != null && active.SimulationId.IsValid)
            return active.SimulationId;
        for (int i = 0; i < partyIds.Length; i++)
            if (partyIds[i].IsValid)
                return partyIds[i];
        throw new InvalidOperationException("连接阵容没有可用于复制兴趣的 ActorId。");
    }
}
