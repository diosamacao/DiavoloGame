using UnityEngine;

/// <summary>
/// NS2 同机幽灵预览：每个权威逻辑步打包本机玩家 Tick，经 Loopback 延迟后应用到 RemoteProxy。
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
    RemoteCharacterProxy _proxy;
    CharacterFacingDebugVisualizer _facingDebugVisualizer;

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
        if (_proxy != null && _host != null)
            _proxy.Render(_host.InterpolationAlpha);
        if (_facingDebugVisualizer != null)
            _facingDebugVisualizer.SetDrawEnabled(drawFacingDebugArrows);
    }

    void OnDestroy()
    {
        UnsubscribeHost();
        _proxy?.Dispose();
        _proxy = null;
    }

    /// <summary>每个逻辑步捕获本机玩家并经 Loopback 投递；追帧时每步都会走，避免漏 Tick。</summary>
    void OnAfterLogicStep(long authorityFrame)
    {
        ILocalPlayer local = SendQuery(new GetLocalPlayerQuery());
        CharacterActor actor = local?.Actor;
        if (actor == null)
            return;

        if (!TryEnsureProxy(local))
            return;

        ActorReplicationSnapshot snapshot = CharacterReplicationCapture.FromActor(actor, _catalog);
        var tick = new AuthorityTick(authorityFrame, new[] { snapshot });
        _transport.SendAuthorityToClients(ReplicationCodec.WriteAuthorityTick(tick));

        int stepMs = Mathf.Max(1, Mathf.RoundToInt(_host.FixedDeltaSeconds * 1000f));
        _transport.AdvanceTimeMs(stepMs);
        _transport.Pump();

        while (_transport.TryDequeueClient(out byte[] payload))
        {
            AuthorityTick received = ReplicationCodec.ReadAuthorityTick(payload);
            if (received.Actors.Length == 0)
                continue;
            _proxy.ApplySnapshot(in received.Actors[0]);
        }
    }

    /// <summary>首次有本机 Actor 时创建共享 Catalog、Loopback 与幽灵；失败则本步跳过。</summary>
    bool TryEnsureProxy(ILocalPlayer local)
    {
        if (_proxy != null)
            return true;

        CharacterConfig config = _config;
        if (config == null && local is PlayerController player)
            config = player.CharacterConfig;
        if (config == null || _host == null)
            return false;

        _config = config;
        _catalog = new ActionReplicationCatalog();
        _transport = new LoopbackReplicationTransport();
        _transport.SetLatencyMs(latencyMs);
        _proxy = RemoteCharacterProxyFactory.Create(
            config,
            _catalog,
            _host.CollisionWorld,
            worldOffset,
            _host.FixedDeltaSeconds,
            transform);
        EnsureFacingDebugVisualizer();
        return true;
    }

    /// <summary>开发构建下给幽灵挂与本体相同的脚底朝向箭头。</summary>
    void EnsureFacingDebugVisualizer()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_proxy?.Root == null)
            return;

        _facingDebugVisualizer = _proxy.Root.GetComponent<CharacterFacingDebugVisualizer>();
        if (_facingDebugVisualizer == null)
            _facingDebugVisualizer = _proxy.Root.gameObject.AddComponent<CharacterFacingDebugVisualizer>();
        _facingDebugVisualizer.Bind(_proxy, "Ghost ");
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
