using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NS2/NS4 同机幽灵预览：打包本机玩家与敌人 Snapshot + 命中边沿，经 Loopback 应用到 RemoteProxy。
/// 不注册第二份命中、不进玩家花名册。
/// </summary>
[DefaultExecutionOrder(-40)]
[DisallowMultipleComponent]
public sealed class RemoteGhostViewController : AppControllerBase
{
    [SerializeField] Vector3 worldOffset = new Vector3(2f, 0f, 0f);
    [SerializeField] int latencyMs = 100;
    [Tooltip("Play 时在幽灵脚底画 wish（黄）与模型朝向（品红），与本体同一套箭头。")]
    [SerializeField] bool drawFacingDebugArrows = true;

    SimulationHost _host;
    CharacterConfig _config;
    LoopbackReplicationTransport _transport;
    ActionReplicationCatalog _catalog;
    CharacterFacingDebugVisualizer _facingDebugVisualizer;
    SimActorId _playerGhostId;

    readonly Dictionary<int, RemoteCharacterProxy> _proxies = new();
    readonly List<EnemyController> _enemies = new();
    readonly List<ActorReplicationSnapshot> _snapshots = new();
    readonly HashSet<int> _seenIds = new();
    readonly List<int> _staleIds = new();

    /// <summary>由战斗世界注入 Host、配置与预览参数；可在 AddComponent 后立即调用。</summary>
    public void Configure(
        SimulationHost host,
        CharacterConfig config,
        Vector3 remoteWorldOffset,
        int remoteLatencyMs)
    {
        UnsubscribeHost();
        _host = host;
        _config = config;
        worldOffset = remoteWorldOffset;
        latencyMs = remoteLatencyMs < 0 ? 0 : remoteLatencyMs;
        if (isActiveAndEnabled)
            SubscribeHost();
    }

    void OnEnable() => SubscribeHost();

    void OnDisable() => UnsubscribeHost();

    void LateUpdate()
    {
        if (_host == null)
            return;

        float alpha = _host.InterpolationAlpha;
        foreach (RemoteCharacterProxy proxy in _proxies.Values)
            proxy.Render(alpha);

        if (_facingDebugVisualizer != null)
            _facingDebugVisualizer.SetDrawEnabled(drawFacingDebugArrows);
    }

    void OnDestroy()
    {
        UnsubscribeHost();
        DisposeAllProxies();
    }

    /// <summary>每个逻辑步捕获玩家与敌人并经 Loopback 投递；含本帧权威命中键。</summary>
    void OnAfterLogicStep(long authorityFrame)
    {
        if (!TryEnsureTransport())
            return;

        CaptureAuthorityActors();
        if (_snapshots.Count == 0)
            return;

        ReplicatedHitEvent[] hits = _host.FrameHits.Count == 0
            ? null
            : ToHitArray(_host.FrameHits);
        var tick = new AuthorityTick(authorityFrame, _snapshots.ToArray(), hits);
        _transport.SendAuthorityToClients(ReplicationCodec.WriteAuthorityTick(tick));

        int stepMs = Mathf.Max(1, Mathf.RoundToInt(_host.FixedDeltaSeconds * 1000f));
        _transport.AdvanceTimeMs(stepMs);
        _transport.Pump();

        while (_transport.TryDequeueClient(out byte[] payload))
            ApplyTick(ReplicationCodec.ReadAuthorityTick(payload));
    }

    /// <summary>创建共享 Catalog 与 Loopback；不在此时强制生成幽灵。</summary>
    bool TryEnsureTransport()
    {
        if (_transport != null && _catalog != null)
            return true;
        if (_host == null)
            return false;

        _catalog = new ActionReplicationCatalog();
        _transport = new LoopbackReplicationTransport();
        _transport.SetLatencyMs(latencyMs);
        return true;
    }

    /// <summary>收集本机玩家与仍存活的敌人权威快照。</summary>
    void CaptureAuthorityActors()
    {
        _snapshots.Clear();

        ILocalPlayer local = SendQuery(new GetLocalPlayerQuery());
        CharacterActor player = local?.Actor;
        if (player != null)
        {
            if (_config == null && local is PlayerController playerController)
                _config = playerController.CharacterConfig;

            _snapshots.Add(CharacterReplicationCapture.FromActor(
                player,
                _catalog,
                ReplicationActorKind.Player));
        }

        if (_host == null)
            return;

        _host.CopyEnemyControllers(_enemies);
        for (int i = 0; i < _enemies.Count; i++)
        {
            CharacterActor enemy = _enemies[i].Actor;
            if (enemy == null)
                continue;

            _snapshots.Add(CharacterReplicationCapture.FromActor(
                enemy,
                _catalog,
                ReplicationActorKind.Enemy));
        }
    }

    /// <summary>按 ActorId 应用快照；缺配置的新 Actor 本步跳过，已消失的幽灵释放。</summary>
    void ApplyTick(AuthorityTick received)
    {
        if (received == null || received.Actors.Length == 0)
            return;

        _seenIds.Clear();
        for (int i = 0; i < received.Actors.Length; i++)
        {
            ActorReplicationSnapshot snapshot = received.Actors[i];
            if (!snapshot.ActorId.IsValid)
                continue;

            int id = snapshot.ActorId.Value;
            _seenIds.Add(id);
            if (!TryGetOrCreateProxy(in snapshot, out RemoteCharacterProxy proxy))
                continue;

            proxy.ApplySnapshot(in snapshot);
        }

        DisposeMissingProxies();
    }

    /// <summary>按 Id 复用或新建幽灵；缺模型配置则跳过，避免整视图失败。</summary>
    bool TryGetOrCreateProxy(in ActorReplicationSnapshot snapshot, out RemoteCharacterProxy proxy)
    {
        int id = snapshot.ActorId.Value;
        if (_proxies.TryGetValue(id, out proxy))
            return true;

        CharacterConfig config = ResolveConfig(in snapshot);
        if (config == null || config.ModelPrefab == null || _host == null)
        {
            proxy = null;
            return false;
        }

        proxy = RemoteCharacterProxyFactory.Create(
            config,
            _catalog,
            _host.CollisionWorld,
            worldOffset,
            _host.FixedDeltaSeconds,
            transform);
        _proxies[id] = proxy;

        if (snapshot.Kind == ReplicationActorKind.Player)
        {
            _playerGhostId = snapshot.ActorId;
            EnsureFacingDebugVisualizer(proxy);
        }

        return true;
    }

    /// <summary>玩家用本机配置；敌人用当前仍注册的 Controller 配置。</summary>
    CharacterConfig ResolveConfig(in ActorReplicationSnapshot snapshot)
    {
        if (snapshot.Kind == ReplicationActorKind.Player)
            return _config;

        for (int i = 0; i < _enemies.Count; i++)
        {
            CharacterActor actor = _enemies[i].Actor;
            if (actor != null && actor.SimulationId == snapshot.ActorId)
                return _enemies[i].CharacterConfig;
        }

        return null;
    }

    /// <summary>本 Tick 未出现的 Actor 视为 despawn，释放对应幽灵。</summary>
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
            if (_playerGhostId.Value == id)
            {
                _facingDebugVisualizer = null;
                _playerGhostId = default;
            }
        }
    }

    void DisposeAllProxies()
    {
        foreach (RemoteCharacterProxy proxy in _proxies.Values)
            proxy.Dispose();
        _proxies.Clear();
        _facingDebugVisualizer = null;
        _playerGhostId = default;
    }

    static ReplicatedHitEvent[] ToHitArray(IReadOnlyList<ReplicatedHitEvent> hits)
    {
        var copy = new ReplicatedHitEvent[hits.Count];
        for (int i = 0; i < hits.Count; i++)
            copy[i] = hits[i];
        return copy;
    }

    /// <summary>开发构建下给玩家幽灵挂与本体相同的脚底朝向箭头。</summary>
    void EnsureFacingDebugVisualizer(RemoteCharacterProxy proxy)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (proxy?.Root == null)
            return;

        _facingDebugVisualizer = proxy.Root.GetComponent<CharacterFacingDebugVisualizer>();
        if (_facingDebugVisualizer == null)
            _facingDebugVisualizer = proxy.Root.gameObject.AddComponent<CharacterFacingDebugVisualizer>();
        _facingDebugVisualizer.Bind(proxy, "Ghost ");
        _facingDebugVisualizer.SetDrawEnabled(drawFacingDebugArrows);
#endif
    }

    void SubscribeHost()
    {
        if (_host != null)
            _host.AfterLogicStep += OnAfterLogicStep;
    }

    void UnsubscribeHost()
    {
        if (_host != null)
            _host.AfterLogicStep -= OnAfterLogicStep;
    }
}
