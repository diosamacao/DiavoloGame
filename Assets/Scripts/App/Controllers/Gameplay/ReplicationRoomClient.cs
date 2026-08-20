using System;
using UnityEngine;

/// <summary>远端 Client 网络薄 Facade：驱动 LocalClientRuntime，逻辑步后发送命令。</summary>
[DefaultExecutionOrder(-150)]
[DisallowMultipleComponent]
public sealed class ReplicationRoomClient : AppControllerBase
{
    CombatWorldController _world;
    ClientSession _session;
    LocalClientRuntime _runtime;

    /// <summary>由 CombatWorldController 注入战斗世界与已启动的客户端 Session。</summary>
    public void Configure(CombatWorldController world, ClientSession session)
    {
        UnsubscribeHost();
        ShutdownRuntime();
        _world = world;
        _session = session;
        if (isActiveAndEnabled)
            SubscribeHost();
    }

    void OnEnable() => SubscribeHost();

    void Start() => EnsureRuntime();

    void Update()
    {
        EnsureRuntime();
        if (_runtime == null || _runtime.IsEnded)
            return;

        _runtime.PollAndApply(NowMs());
        _runtime.SampleRenderInput();
    }

    void LateUpdate() => _runtime?.Render();

    void OnDisable() => UnsubscribeHost();

    void OnDestroy()
    {
        UnsubscribeHost();
        ShutdownRuntime();
    }

    /// <summary>固定逻辑步后让 Runtime 发送命令并推进 Owner 预测。</summary>
    void OnAfterLogicStep(long _)
    {
        _runtime?.SendCommandAndPredict();
    }

    /// <summary>Start 或首帧 Update 再创建 Runtime，保证场景 PlayerController 已登记。</summary>
    void EnsureRuntime()
    {
        if (_runtime != null || _world == null || _session == null)
            return;

        _runtime = new LocalClientRuntime(
            _world,
            _session,
            GetArchitecture(),
            transform,
            ReplicationRole.Client);
    }

    void ShutdownRuntime()
    {
        _runtime?.Dispose();
        _runtime = null;
        _session = null;
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
