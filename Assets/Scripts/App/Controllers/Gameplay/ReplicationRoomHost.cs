using System;
using UnityEngine;

/// <summary>Listen Host 网络薄 Facade：驱动 Session，并转发 ACT Gameplay 构帧结果。</summary>
[DefaultExecutionOrder(-150)]
[DisallowMultipleComponent]
public sealed class ReplicationRoomHost : AppControllerBase
{
    CombatWorldController _world;
    ServerSession _session;
    ActHostRoomGameplay _gameplay;
    bool _bindFailed;
    int _lastTickBytes = -1;

    /// <summary>由 CombatWorldController 注入战斗世界与已启动的服务端 Session。</summary>
    public void Configure(CombatWorldController world, ServerSession session)
    {
        UnsubscribeHost();
        if (_session != null)
            _session.Disconnected -= OnSessionDisconnected;
        _world = world;
        _session = session;
        _bindFailed = session == null;
        if (_session != null)
            _session.Disconnected += OnSessionDisconnected;
        if (isActiveAndEnabled)
            SubscribeHost();
    }

    void OnEnable() => SubscribeHost();

    void Start()
    {
        if (_world != null)
            _gameplay = new ActHostRoomGameplay(_world, GetArchitecture());
        RefreshHud("Listening");
    }

    void Update()
    {
        if (_session == null)
            return;

        _session.Poll(NowMs());
        _gameplay?.DrainPlayerRequests(_session);
        _gameplay?.DrainApplicationMessages(_session);
    }

    void OnDisable() => UnsubscribeHost();

    void OnDestroy()
    {
        UnsubscribeHost();
        if (_session != null)
            _session.Disconnected -= OnSessionDisconnected;
        _gameplay?.Shutdown();
        _gameplay = null;
        _session?.Dispose();
        _session = null;
    }

    /// <summary>权威逻辑步后发送 Gameplay Facade 构建的唯一 ReplicationFrame。</summary>
    void OnAfterLogicStep(long authorityFrame)
    {
        if (_session == null
            || _gameplay == null
            || !_gameplay.TryBuildReplicationFrame(
                authorityFrame,
                out NetConnectionId connectionId,
                out byte[] body))
        {
            RefreshHud(_gameplay?.HasGuest == true ? "ClientJoined" : "Listening");
            return;
        }

        _lastTickBytes = body.Length + 2;
        _session.SendApplication(
            connectionId,
            (byte)RoomMessageKind.ReplicationFrame,
            NetChannel.SnapshotUnreliableSequenced,
            body);
        RefreshHud("ClientJoined");
    }

    /// <summary>把 Session 断开事件转发给 ACT Guest 生命周期。</summary>
    void OnSessionDisconnected(SessionDisconnected disconnected)
    {
        _gameplay?.OnSessionDisconnected(disconnected);
        RefreshHud("Listening");
    }

    void RefreshHud(string status)
    {
        if (_world == null)
            return;

        long frame = _world.SimulationHost != null
            ? _world.SimulationHost.CurrentFrame
            : -1;
        _world.RoomHud = new ReplicationRoomHudInfo(
            true,
            ReplicationRole.ListenHost,
            _bindFailed ? "BindFailed" : status,
            frame,
            rttMs: -1,
            _gameplay?.HostHealthMilli ?? -1,
            _lastTickBytes,
            _gameplay?.LastCommandBytes ?? -1,
            proxyCount: -1,
            predictionPendingCount: -1);
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
