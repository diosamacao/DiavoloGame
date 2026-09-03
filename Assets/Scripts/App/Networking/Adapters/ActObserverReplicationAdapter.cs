using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>ACT Observer 复制适配器：解析角色生命周期记录并管理只读 Remote Proxy 与 TargetSystem 注册。</summary>
public sealed class ActObserverReplicationAdapter
{
    readonly ActContentRegistry _content;
    readonly ActCharacterSnapshotSchema _characterSchema;
    readonly Func<SimulationHost> _getSimulationHost;
    readonly Transform _parent;
    readonly Action<IHurtboxTarget> _registerTarget;
    readonly Action<IHurtboxTarget> _unregisterTarget;
    readonly Dictionary<int, RemoteCharacterProxy> _proxies = new();
    readonly Dictionary<int, SnapshotTimeline<ActorReplicationSnapshot>> _timelines = new();
    readonly Dictionary<int, long> _appliedTicks = new();
    readonly Dictionary<int, double> _playbackTicks = new();

    /// <summary>创建绑定内容目录、Schema、Proxy 父节点和 TargetSystem 接缝的 Observer 适配器。</summary>
    public ActObserverReplicationAdapter(
        ActContentRegistry content,
        ActCharacterSnapshotSchema characterSchema,
        Func<SimulationHost> getSimulationHost,
        Transform parent,
        Action<IHurtboxTarget> registerTarget,
        Action<IHurtboxTarget> unregisterTarget)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _characterSchema = characterSchema ?? throw new ArgumentNullException(nameof(characterSchema));
        _getSimulationHost = getSimulationHost
            ?? throw new ArgumentNullException(nameof(getSimulationHost));
        _parent = parent;
        _registerTarget = registerTarget;
        _unregisterTarget = unregisterTarget;
    }

    /// <summary>当前存活的远端 Proxy 数量，不包含 Owner。</summary>
    public int Count => _proxies.Count;

    /// <summary>枚举当前全部只读 Proxy，供渲染与 Owner 本地软碰撞读取。</summary>
    public IEnumerable<RemoteCharacterProxy> Proxies => _proxies.Values;

    /// <summary>处理显式 Spawn；Owner 只回传快照，Observer 创建并注册精确 Archetype Proxy。</summary>
    public void ApplySpawns(
        SpawnRecord[] records,
        IReadOnlyList<SimActorId> ownerActorIds,
        SimActorId activeOwnerActorId,
        long authorityTick,
        ref ActorReplicationSnapshot ownerSnapshot,
        ref bool hasOwnerSnapshot)
    {
        if (records == null)
            return;

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

            if (IsOwned(snapshot.ActorId, ownerActorIds))
            {
                if (snapshot.Kind != ReplicationActorKind.Player)
                    throw new InvalidOperationException("Owner 阵容 Spawn 必须是 Player 原型。");
                if (snapshot.ActorId == activeOwnerActorId)
                {
                    ownerSnapshot = snapshot;
                    hasOwnerSnapshot = true;
                }
                continue;
            }

            if (_proxies.ContainsKey(record.EntityId.Value))
                throw new InvalidOperationException($"远端实体 {record.EntityId.Value} 重复创建 Proxy。");

            CharacterConfig config = _content.ResolveCharacterConfig(record.ArchetypeId);
            RemoteCharacterProxy proxy = CreateProxy(config);
            int id = record.EntityId.Value;
            _proxies.Add(id, proxy);
            var timeline = new SnapshotTimeline<ActorReplicationSnapshot>();
            timeline.TryPush(authorityTick, in snapshot);
            _timelines.Add(id, timeline);
            _appliedTicks[id] = authorityTick;
            _registerTarget?.Invoke(proxy);
            proxy.ApplySnapshot(in snapshot);
            _playbackTicks[id] = authorityTick;
        }
    }

    /// <summary>处理显式 Update；未知 Observer 实体明确失败，禁止静默创建默认 Proxy。</summary>
    public void ApplyUpdates(
        EntityRecord[] records,
        IReadOnlyList<SimActorId> ownerActorIds,
        SimActorId activeOwnerActorId,
        long authorityTick,
        ref ActorReplicationSnapshot ownerSnapshot,
        ref bool hasOwnerSnapshot)
    {
        if (records == null)
            return;

        for (int i = 0; i < records.Length; i++)
        {
            EntityRecord record = records[i];
            ActorReplicationSnapshot snapshot = DecodeRecord(
                record.EntityId,
                record.SchemaId,
                record.Payload);
            if (IsOwned(snapshot.ActorId, ownerActorIds))
            {
                if (snapshot.ActorId == activeOwnerActorId)
                {
                    ownerSnapshot = snapshot;
                    hasOwnerSnapshot = true;
                }
                continue;
            }

            int id = record.EntityId.Value;
            if (!_proxies.TryGetValue(id, out RemoteCharacterProxy proxy))
            {
                throw new InvalidOperationException(
                    $"远端 Update {id} 没有已存在的 Proxy。");
            }

            if (!_timelines.TryGetValue(id, out SnapshotTimeline<ActorReplicationSnapshot> timeline))
            {
                timeline = new SnapshotTimeline<ActorReplicationSnapshot>();
                _timelines.Add(id, timeline);
            }

            bool becameVisible = !proxy.IsPartyVisible && IsPartyVisible(in snapshot);
            if (becameVisible)
            {
                // 先拒绝迟到旧包，再丢弃上次退场的插值历史；否则新角色会从旧退出点被拉到登场点。
                if (authorityTick <= timeline.LatestTick)
                    continue;
                timeline.Clear();
            }

            // 旧 Tick 不回滚。每份到达的快照立刻写判定/受击/Notify，禁止等播放头。
            if (!timeline.TryPush(authorityTick, in snapshot))
                continue;
            proxy.ApplySnapshot(in snapshot, simulationTicks: 0, updatePresentation: false);
            _appliedTicks[id] = authorityTick;
            if (becameVisible || !_playbackTicks.ContainsKey(id))
                _playbackTicks[id] = authorityTick;
        }
    }

    /// <summary>处理显式 Despawn；返回 false 表示 Owner 已被移除，调用方应结束房间。</summary>
    public bool ApplyDespawns(
        DespawnRecord[] records,
        IReadOnlyList<SimActorId> ownerActorIds,
        SimActorId activeOwnerActorId)
    {
        if (records == null)
            return true;

        for (int i = 0; i < records.Length; i++)
        {
            int id = records[i].EntityId.Value;
            var actorId = new SimActorId(id);
            if (IsOwned(actorId, ownerActorIds))
            {
                if (actorId == activeOwnerActorId)
                    return false;
                continue;
            }

            if (!_proxies.TryGetValue(id, out RemoteCharacterProxy proxy))
                throw new InvalidOperationException($"远端 Despawn {id} 没有已存在的 Proxy。");

            _unregisterTarget?.Invoke(proxy);
            proxy.Dispose();
            _proxies.Remove(id);
            _timelines.Remove(id);
            _appliedTicks.Remove(id);
            _playbackTicks.Remove(id);
        }

        return true;
    }

    /// <summary>按 SimActorId 查找 Observer Proxy，供 Hit Cue、本地交互与 Additive 探针定位。</summary>
    public bool TryGetProxy(SimActorId actorId, out RemoteCharacterProxy proxy)
    {
        if (!actorId.IsValid)
        {
            proxy = null;
            return false;
        }

        if (_proxies.TryGetValue(actorId.Value, out proxy) && proxy != null)
            return true;

        // EntityId 与 Snapshot.ActorId 偶发未同步时，按已绑定身份扫一遍。
        foreach (KeyValuePair<int, RemoteCharacterProxy> pair in _proxies)
        {
            if (pair.Value != null && pair.Value.SimulationId.Equals(actorId))
            {
                proxy = pair.Value;
                return true;
            }
        }

        proxy = null;
        return false;
    }

    /// <summary>注销并销毁全部 Observer View；结束房间与组件销毁共用。</summary>
    public void DisposeViews()
    {
        foreach (RemoteCharacterProxy proxy in _proxies.Values)
        {
            _unregisterTarget?.Invoke(proxy);
            proxy.Dispose();
        }
        _proxies.Clear();
        _timelines.Clear();
        _appliedTicks.Clear();
        _playbackTicks.Clear();
    }

    /// <summary>
    /// 播放头只驱动模型锚点插值。判定、受击和 Notify 在 ApplyUpdates 到达时已提交。
    /// </summary>
    public void Render(int interpolationDelayTicks, float deltaTimeSeconds)
    {
        foreach (KeyValuePair<int, RemoteCharacterProxy> pair in _proxies)
        {
            RemoteCharacterProxy proxy = pair.Value;
            if (proxy == null)
                continue;
            if (!_timelines.TryGetValue(pair.Key, out SnapshotTimeline<ActorReplicationSnapshot> timeline))
            {
                proxy.Render(0f);
                continue;
            }

            bool hasPlayback = _playbackTicks.TryGetValue(pair.Key, out double playback);
            playback = RemotePlaybackClock.Advance(
                playback,
                hasPlayback,
                timeline.FirstTick,
                timeline.LatestTick,
                interpolationDelayTicks,
                deltaTimeSeconds,
                SimulationConfig.DefaultLogicHz);
            _playbackTicks[pair.Key] = playback;

            if (!timeline.TrySampleAt(
                    playback,
                    out _,
                    out _,
                    out ActorReplicationSnapshot from,
                    out ActorReplicationSnapshot to,
                    out float alpha))
            {
                proxy.Render(0f);
                continue;
            }

            proxy.SetPresentationBracket(in from, in to);
            proxy.TickAnimation(deltaTimeSeconds);
            proxy.Render(alpha);
        }
    }

    /// <summary>校验角色 Schema 与 EntityId 后解码唯一 Snapshot 布局。</summary>
    ActorReplicationSnapshot DecodeRecord(
        NetEntityId entityId,
        ushort schemaId,
        byte[] payload)
    {
        if (schemaId != ActCharacterSnapshotSchema.Id)
            throw new InvalidOperationException($"角色实体 {entityId.Value} 使用未知 Schema {schemaId}。");

        ActorReplicationSnapshot snapshot = _characterSchema.DecodeSnapshot(payload);
        if (!snapshot.ActorId.IsValid || snapshot.ActorId.Value != entityId.Value)
        {
            throw new InvalidOperationException(
                $"角色记录 EntityId={entityId.Value} 与 Snapshot ActorId={snapshot.ActorId.Value} 不一致。");
        }
        return snapshot;
    }

    /// <summary>判断实体是否属于本机玩家的任一稳定阵容槽。</summary>
    static bool IsOwned(SimActorId actorId, IReadOnlyList<SimActorId> ownerActorIds)
    {
        if (ownerActorIds == null)
            return false;
        for (int i = 0; i < ownerActorIds.Count; i++)
        {
            if (ownerActorIds[i] == actorId)
                return true;
        }
        return false;
    }

    /// <summary>从快照阵容状态判断表现是否应存在；与 RemoteCharacterProxy 显隐规则保持一致。</summary>
    static bool IsPartyVisible(in ActorReplicationSnapshot snapshot)
    {
        PartyMemberState state = PartyReplicationPacking.ReadMemberState(snapshot.FlagsPacked);
        return state == PartyMemberState.Active || state == PartyMemberState.Exiting;
    }

    /// <summary>按精确 CharacterConfig 创建 Remote Proxy；缺模型或 SimulationHost 时明确失败。</summary>
    RemoteCharacterProxy CreateProxy(CharacterConfig config)
    {
        SimulationHost host = _getSimulationHost();
        if (config == null || config.ModelPrefab == null || host == null)
            throw new InvalidOperationException("远端原型缺少 CharacterConfig、ModelPrefab 或 SimulationHost。");

        return ActRemoteProxyFactory.Create(
            config,
            _content,
            host.CollisionWorld,
            Vector3.zero,
            host.FixedDeltaSeconds,
            _parent);
    }
}
