using System;
using UnityEngine;

/// <summary>Client 网络薄 Facade：驱动 Session、上行命令，并转发 ReplicationFrame / MatchEnd 给 ACT Gameplay。</summary>
[DefaultExecutionOrder(-150)]
[DisallowMultipleComponent]
public sealed class ReplicationRoomClient : AppControllerBase
{
    CombatWorldController _world;
    ClientSession _session;
    ActClientRoomGameplay _gameplay;
    bool _joined;
    bool _ended;

    /// <summary>由 CombatWorldController 注入战斗世界与已启动的客户端 Session。</summary>
    public void Configure(CombatWorldController world, ClientSession session)
    {
        UnsubscribeHost();
        _world = world;
        _session = session;
        if (isActiveAndEnabled)
            SubscribeHost();
    }

    void OnEnable() => SubscribeHost();

    void Start()
    {
        if (_world != null)
            _gameplay = new ActClientRoomGameplay(_world, GetArchitecture(), transform);
        RefreshHud("Connecting");
    }

    void Update()
    {
        if (_session == null || _ended)
            return;

        _session.Poll(NowMs());
        // Join 必须先于 Drain：Dedicated 同拍会带首帧 Spawn，Owner 身份未绑定会把自己建成 Proxy。
        AcceptJoinIfReady();
        DrainApplicationMessages();
        EndIfSessionEnded();
        if (_joined && !_ended)
            _gameplay?.SampleRenderInput();
    }

    void LateUpdate() => _gameplay?.Render();

    void OnDisable() => UnsubscribeHost();

    void OnDestroy()
    {
        UnsubscribeHost();
        _gameplay?.Shutdown();
        _gameplay = null;
        _session?.Dispose();
        _session = null;
    }

    /// <summary>固定逻辑步后让 Gameplay 推进 Owner 预测，再由 Room 发送命令正文。</summary>
    void OnAfterLogicStep(long _)
    {
        if (!_joined
            || _ended
            || _session == null
            || _gameplay == null
            || !_gameplay.TryBuildCommand(out byte[] body))
        {
            return;
        }

        _session.SendApplication(
            (byte)RoomMessageKind.ClientCommand,
            NetChannel.CommandUnreliableRedundant,
            body);
        _gameplay.StepPrediction();
        RefreshHud("Joined");
    }

    /// <summary>JoinAccept 到达后立刻绑定 Owner，再允许 Drain 复制帧。</summary>
    void AcceptJoinIfReady()
    {
        if (!_joined && _session.State == ClientSessionState.Joined)
            OnSessionJoined(_session.JoinAccept);
    }

    /// <summary>Kick / 超时在 Drain 之后收口，避免同拍 MatchEnd 被提前 Shutdown。</summary>
    void EndIfSessionEnded()
    {
        if (_session.State == ClientSessionState.Ended && !_ended)
            EndRoom($"SessionEnded:{_session.LastDisconnectReason}");
    }

    /// <summary>消费 ReplicationFrame；MatchEnd 立即结束房间 Gameplay。</summary>
    void DrainApplicationMessages()
    {
        while (_session.TryDequeueApplication(out SessionApplicationPacket packet))
        {
            // Dedicated W7：可靠 MatchEnd 优先于复制帧，避免结束后仍 Apply。
            if (packet.MessageType == (byte)RoomMessageKind.MatchEnd)
            {
                EndRoom("MatchEnded");
                return;
            }

            if (packet.MessageType != (byte)RoomMessageKind.ReplicationFrame)
                continue;

            try
            {
                ActClientFrameApplyStatus status =
                    _gameplay.ApplyReplicationFrame(packet.Payload);
                if (status == ActClientFrameApplyStatus.StaleSequence)
                    continue;
                if (status == ActClientFrameApplyStatus.Rejected)
                {
                    Debug.LogWarning(
                        $"ReplicationRoomClient: 复制帧被拒绝。{_gameplay.LastRejectMessage}",
                        this);
                    EndRoom("ReplicationRejected");
                    return;
                }
                if (status == ActClientFrameApplyStatus.OwnerDespawned)
                {
                    EndRoom("OwnerDespawned");
                    return;
                }
                RefreshHud("Joined");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ReplicationRoomClient: 非法复制帧。{ex.Message}", this);
                EndRoom("ReplicationInvalid");
                return;
            }
        }
    }

    /// <summary>Session Join 成功后把身份与预测时钟交给 Client Gameplay。</summary>
    void OnSessionJoined(in SessionJoinAccept accept)
    {
        if (_joined)
            return;

        _joined = true;
        _gameplay?.BeginSession(in accept);
        Debug.Log(
            $"ReplicationRoomClient: 入房成功 player={accept.PlayerId.Value} "
            + $"actor={accept.EntityId.Value}。",
            this);
        RefreshHud("Joined");
    }

    void EndRoom(string status)
    {
        if (_ended)
            return;
        _ended = true;
        _joined = false;
        _gameplay?.Shutdown();
        Debug.Log($"ReplicationRoomClient: 房间结束 {status}。", this);
        RefreshHud(status);
    }

    void RefreshHud(string status)
    {
        if (_world == null)
            return;

        _world.RoomHud = new ReplicationRoomHudInfo(
            true,
            ReplicationRole.Client,
            status,
            _gameplay?.LastAuthorityFrame ?? -1,
            _session?.RttMs ?? -1,
            _gameplay?.SelfHealthMilli ?? -1,
            _gameplay?.LastTickBytes ?? -1,
            _gameplay?.LastCommandBytes ?? -1,
            _gameplay?.ProxyCount ?? 0,
            _gameplay?.PredictionPendingCount ?? 0);
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
