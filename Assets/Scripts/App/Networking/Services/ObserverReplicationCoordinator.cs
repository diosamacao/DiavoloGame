using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>客户端 Observer 复制协调器：独占 V2 应用、Meta 原子提交、远端生命周期与播放时钟。</summary>
public sealed class ObserverReplicationCoordinator
{
    readonly CombatWorldController _world;
    readonly OwnerPredictionCoordinator _owner;
    readonly ActObserverReplicationAdapter _observer;
    readonly ReplicationClient _replicationClient;
    readonly List<SpawnRecord> _appliedSpawns = new();
    readonly List<EntityRecord> _appliedUpdates = new();
    readonly List<DespawnRecord> _appliedDespawns = new();
    readonly NetworkTimeEstimator _clock = new();
    PlayerController _localPlayer;
    ActReplicationSnapshotMeta _appliedMeta;
    bool _metadataPublishedThisCall;

    /// <summary>创建 V2 ReplicationClient，并把应用事件收集为同次原子提交批。</summary>
    public ObserverReplicationCoordinator(
        CombatWorldController world,
        OwnerPredictionCoordinator owner,
        ActObserverReplicationAdapter observer,
        IReplicationSchema characterSchema)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        if (characterSchema == null)
            throw new ArgumentNullException(nameof(characterSchema));

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
    }

    /// <summary>最近成功应用的权威帧；尚未入房时为 -1。</summary>
    public long LastAuthorityFrame { get; private set; } = -1;

    /// <summary>最近完整下行应用消息字节；尚未收到时为 -1。</summary>
    public int LastTickBytes { get; private set; } = -1;

    /// <summary>当前 Observer Proxy 数量。</summary>
    public int ProxyCount => _observer.Count;

    /// <summary>远端网络插值延迟毫秒；Listen 对外报告 0。</summary>
    public int InterpolationDelayMs =>
        _world.Role == ReplicationRole.ListenHost ? 0 : _clock.InterpolationDelayMs;

    /// <summary>最近一次 ReplicationClient 拒绝原因。</summary>
    public string LastRejectMessage { get; private set; }

    /// <summary>按权威 Id 获取 Observer 可见体。</summary>
    public bool TryGetProxy(SimActorId actorId, out RemoteCharacterProxy proxy) =>
        _observer.TryGetProxy(actorId, out proxy);

    /// <summary>Join 后重置 Meta，并以接受帧作为本地 Observer 时钟起点。</summary>
    public void BeginSession(in SessionJoinAccept accept, PlayerController localPlayer)
    {
        _localPlayer = localPlayer;
        _appliedMeta = null;
        LastAuthorityFrame = accept.AuthorityTick.Value;
    }

    /// <summary>应用可靠 V2 生命周期；屏障释放出的 Snapshot 在同次调用完成纠正。</summary>
    public ActClientReplicationApplyStatus ApplyLifecycle(byte[] body)
    {
        LastTickBytes = body != null ? body.Length + 2 : -1;
        _appliedSpawns.Clear();
        _appliedUpdates.Clear();
        _appliedDespawns.Clear();
        _metadataPublishedThisCall = false;
        ReplicationLifecycle lifecycle = ReplicationProtocolV2Codec.DecodeLifecycle(body);
        if (!_replicationClient.ApplyLifecycle(lifecycle))
            return ActClientReplicationApplyStatus.Rejected;

        SimActorId ownerId = _owner.ResolveCurrentOwnerId(_appliedMeta);
        ActorReplicationSnapshot self = default;
        bool hasSelf = false;
        _observer.ApplySpawns(
            _appliedSpawns.ToArray(),
            _owner.PartyActorIds,
            ownerId,
            lifecycle.Tick.Value,
            _owner.ApplyPartySnapshot,
            ref self,
            ref hasSelf);
        if (!_observer.ApplyDespawns(
                _appliedDespawns.ToArray(),
                _owner.PartyActorIds,
                ownerId))
        {
            return ActClientReplicationApplyStatus.OwnerDespawned;
        }
        if (_metadataPublishedThisCall)
            ApplyCollectedAuthorityState(_replicationClient.LatestSnapshotTick);
        return ActClientReplicationApplyStatus.Applied;
    }

    /// <summary>应用或缓冲 V2 Snapshot；仅本包 Rejected 才请求恢复，成功包必须过 Meta 屏障。</summary>
    public ActClientReplicationApplyStatus ApplySnapshot(byte[] body)
    {
        LastTickBytes = body != null ? body.Length + 2 : -1;
        _appliedUpdates.Clear();
        _metadataPublishedThisCall = false;
        ReplicationSnapshot snapshot = ReplicationProtocolV2Codec.DecodeSnapshot(body);
        ReplicationSnapshotApplyResult applied = _replicationClient.ApplySnapshot(snapshot);
        if (applied == ReplicationSnapshotApplyResult.Rejected)
            return ActClientReplicationApplyStatus.Rejected;
        if (applied == ReplicationSnapshotApplyResult.Buffered)
            return ActClientReplicationApplyStatus.Buffered;
        // 恢复后旧闩不得再把已成功快照当拒绝；缺 Meta 只请求恢复，禁止拆房间。
        if (_appliedMeta == null)
            return ActClientReplicationApplyStatus.Rejected;

        ApplyCollectedAuthorityState(snapshot.Tick.Value);
        _owner.LogPredictionOpenedOnce(LastAuthorityFrame);
        return ActClientReplicationApplyStatus.Applied;
    }

    /// <summary>用最近心跳 RTT 刷新 Observer 插值延迟。</summary>
    public void ObserveNetworkSample(int rttMs)
    {
        if (rttMs >= 0)
            _clock.ObserveRtt(rttMs);
    }

    /// <summary>Owner 跟 Host alpha；Observer 用独立播放时钟，Listen 固定保留一 tick。</summary>
    public void Render()
    {
        SimulationHost host = _world.SimulationHost;
        if (host == null)
            return;
        _localPlayer?.Party?.Render(host.InterpolationAlpha);
        int delayTicks = _world.Role == ReplicationRole.ListenHost
            ? 1
            : _clock.InterpolationDelayTicks;
        _observer.Render(delayTicks, Time.deltaTime);
    }

    /// <summary>Recovery 第一步先销毁 Observer View，避免旧 Proxy 穿过 Owner 重置边界。</summary>
    public void DisposeViewsForRecovery() => _observer.DisposeViews();

    /// <summary>Owner 重置完成后再清复制 Registry 与旧 Meta，等待权威完整恢复帧重新开闸。</summary>
    public void ResetClientForRecovery()
    {
        _appliedMeta = null;
        _replicationClient.ResetForRecovery();
    }

    /// <summary>注销并释放全部 Observer View。</summary>
    public void Shutdown() => _observer.DisposeViews();

    /// <summary>按 Meta→Observer Updates→Owner Snapshot 的顺序提交本批权威状态。</summary>
    void ApplyCollectedAuthorityState(long authorityTick)
    {
        _owner.ApplyAuthorityMeta(_appliedMeta);
        SimActorId ownerId = _owner.ResolveCurrentOwnerId(_appliedMeta);
        ActorReplicationSnapshot self = default;
        bool hasSelf = false;
        _clock.ObserveAuthorityTick(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), authorityTick);
        _observer.ApplyUpdates(
            _appliedUpdates.ToArray(),
            _owner.PartyActorIds,
            ownerId,
            authorityTick,
            _owner.ApplyPartySnapshot,
            ref self,
            ref hasSelf);
        LastAuthorityFrame = authorityTick;
        if (hasSelf)
            _owner.ApplySelfSnapshot(in self, _appliedMeta.LastAppliedClientFrameHint);
    }
}
