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
        SimActorId ownerActorId,
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

            if (snapshot.ActorId == ownerActorId)
            {
                if (snapshot.Kind != ReplicationActorKind.Player)
                    throw new InvalidOperationException("Owner Spawn 必须是 Player 原型。");
                ownerSnapshot = snapshot;
                hasOwnerSnapshot = true;
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
        }
    }

    /// <summary>处理显式 Update；未知 Observer 实体明确失败，禁止静默创建默认 Proxy。</summary>
    public void ApplyUpdates(
        EntityRecord[] records,
        SimActorId ownerActorId,
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
            if (snapshot.ActorId == ownerActorId)
            {
                ownerSnapshot = snapshot;
                hasOwnerSnapshot = true;
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

            // 旧 Tick 不回滚 Proxy；首份仍立即提交以免实体停在出生原点。
            if (!timeline.TryPush(authorityTick, in snapshot))
                continue;
            if (!_appliedTicks.ContainsKey(id))
            {
                proxy.ApplySnapshot(in snapshot);
                _appliedTicks[id] = authorityTick;
            }
        }
    }

    /// <summary>处理显式 Despawn；返回 false 表示 Owner 已被移除，调用方应结束房间。</summary>
    public bool ApplyDespawns(DespawnRecord[] records, SimActorId ownerActorId)
    {
        if (records == null)
            return true;

        for (int i = 0; i < records.Length; i++)
        {
            int id = records[i].EntityId.Value;
            if (id == ownerActorId.Value)
                return false;

            if (!_proxies.TryGetValue(id, out RemoteCharacterProxy proxy))
                throw new InvalidOperationException($"远端 Despawn {id} 没有已存在的 Proxy。");

            _unregisterTarget?.Invoke(proxy);
            proxy.Dispose();
            _proxies.Remove(id);
            _timelines.Remove(id);
            _appliedTicks.Remove(id);
        }

        return true;
    }

    /// <summary>按 SimActorId 查找 Observer Proxy，供 Hit Cue 与本地交互表现定位。</summary>
    public bool TryGetProxy(SimActorId actorId, out RemoteCharacterProxy proxy)
    {
        if (!actorId.IsValid)
        {
            proxy = null;
            return false;
        }
        return _proxies.TryGetValue(actorId.Value, out proxy);
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
    }

    /// <summary>按插值延迟取样远端时间线，再用取样 alpha 渲染；不再只用本地 InterpolationAlpha。</summary>
    public void Render(int interpolationDelayTicks)
    {
        foreach (KeyValuePair<int, RemoteCharacterProxy> pair in _proxies)
        {
            RemoteCharacterProxy proxy = pair.Value;
            if (proxy == null)
                continue;
            if (!_timelines.TryGetValue(pair.Key, out SnapshotTimeline<ActorReplicationSnapshot> timeline)
                || !timeline.TrySample(
                    interpolationDelayTicks,
                    out _,
                    out long toTick,
                    out _,
                    out ActorReplicationSnapshot to,
                    out float alpha))
            {
                proxy.Render(0f);
                continue;
            }

            if (!_appliedTicks.TryGetValue(pair.Key, out long applied) || applied != toTick)
            {
                proxy.ApplySnapshot(in to);
                _appliedTicks[pair.Key] = toTick;
            }

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
