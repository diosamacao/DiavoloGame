using System;
using UnityEngine;

/// <summary>本机或远端 Client 的 Session/Gameplay 运行时：Join、Drain、上行命令与 Owner 预测。</summary>
public sealed class LocalClientRuntime : IDisposable
{
    readonly CombatWorldController _world;
    readonly ReplicationRole _hudRole;
    ClientSession _session;
    ActClientRoomGameplay _gameplay;
    bool _joined;
    bool _ended;
    bool _disposed;

    /// <summary>绑定已启动的 ClientSession；Gameplay 在构造时创建，Join 后才开预测。</summary>
    public LocalClientRuntime(
        CombatWorldController world,
        ClientSession session,
        ACTGameArchitecture architecture,
        Transform proxyParent,
        ReplicationRole hudRole)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _hudRole = hudRole;
        if (architecture == null)
            throw new ArgumentNullException(nameof(architecture));
        if (proxyParent == null)
            throw new ArgumentNullException(nameof(proxyParent));

        _gameplay = new ActClientRoomGameplay(world, architecture, proxyParent);
        Status = "Connecting";
        RefreshHud();
    }

    /// <summary>Session Join 已完成且尚未结束。</summary>
    public bool IsJoined => _joined && !_ended;

    /// <summary>Kick / MatchEnd / 拒绝后为 true。</summary>
    public bool IsEnded => _ended;

    /// <summary>HUD 状态字；未入房为 Connecting。</summary>
    public string Status { get; private set; }

    /// <summary>底层 Session，供 Facade 读取 RTT。</summary>
    public ClientSession Session => _session;

    /// <summary>最近成功应用的权威帧；尚未入房时为 -1。</summary>
    public long LastAuthorityFrame => _gameplay != null ? _gameplay.LastAuthorityFrame : -1;

    /// <summary>最近完整下行应用消息字节；尚未收到时为 -1。</summary>
    public int LastTickBytes => _gameplay != null ? _gameplay.LastTickBytes : -1;

    /// <summary>最近完整上行命令消息字节；尚未发送时为 -1。</summary>
    public int LastCommandBytes => _gameplay != null ? _gameplay.LastCommandBytes : -1;

    /// <summary>Owner 最近权威生命值。</summary>
    public int SelfHealthMilli => _gameplay != null ? _gameplay.SelfHealthMilli : -1;

    /// <summary>当前 Observer Proxy 数量。</summary>
    public int ProxyCount => _gameplay != null ? _gameplay.ProxyCount : 0;

    /// <summary>按权威 Id 取本机 Observer Proxy。</summary>
    public bool TryGetObserverProxy(SimActorId actorId, out RemoteCharacterProxy proxy)
    {
        proxy = null;
        return _gameplay != null && _gameplay.TryGetProxy(actorId, out proxy);
    }

    /// <summary>Owner 尚未确认的动作与位移预测总数。</summary>
    public int PredictionPendingCount => _gameplay != null ? _gameplay.PredictionPendingCount : 0;

    /// <summary>泵 Session、先 Accept Join 再 Drain 复制帧；同拍 MatchEnd 优先结束。</summary>
    public void PollAndApply(long nowMs)
    {
        if (_disposed || _session == null || _ended)
            return;

        _session.Poll(nowMs);
        AcceptJoinIfReady();
        DrainApplicationMessages();
        EndIfSessionEnded();
        RefreshHud();
    }

    /// <summary>渲染帧采样下一预测帧输入边沿。</summary>
    public void SampleRenderInput()
    {
        if (_joined && !_ended)
            _gameplay?.SampleRenderInput();
    }

    /// <summary>先构命令并发送，再推进 Autonomous 预测。未入房时为空操作。</summary>
    public void SendCommandAndPredict()
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
        Status = "Joined";
        RefreshHud();
    }

    /// <summary>Listen 同拍：先采样再发送预测，避免命令落在权威步之后。</summary>
    public void SampleSendPredict()
    {
        SampleRenderInput();
        SendCommandAndPredict();
    }

    /// <summary>按 SimulationHost 插值比例渲染 Owner 与 Observer。</summary>
    public void Render() => _gameplay?.Render();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _ended = true;
        _joined = false;
        _gameplay?.Shutdown();
        _gameplay = null;
        _session?.Dispose();
        _session = null;
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
            if (packet.MessageType == (byte)RoomMessageKind.MatchEnd)
            {
                EndRoom("MatchEnded");
                return;
            }

            if (packet.MessageType == (byte)RoomMessageKind.ReplicationEvent)
            {
                try
                {
                    _gameplay.ApplyReplicationEvents(packet.Payload);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"LocalClientRuntime: 非法命中事件。{ex.Message}");
                }

                continue;
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
                        $"LocalClientRuntime: 复制帧被拒绝，请求全量恢复。{_gameplay.LastRejectMessage}");
                    _gameplay.ResetReplicationForRecovery();
                    _session.SendApplication(
                        (byte)RoomMessageKind.ReplicationRecover,
                        NetChannel.EventReliableOrdered,
                        Array.Empty<byte>());
                    continue;
                }

                if (status == ActClientFrameApplyStatus.OwnerDespawned)
                {
                    EndRoom("OwnerDespawned");
                    return;
                }

                Status = "Joined";
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"LocalClientRuntime: 非法复制帧。{ex.Message}");
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
            $"LocalClientRuntime: 入房成功 player={accept.PlayerId.Value} "
            + $"actor={accept.EntityId.Value}。");
        Status = "Joined";
    }

    void EndRoom(string status)
    {
        if (_ended)
            return;

        _ended = true;
        _joined = false;
        _gameplay?.Shutdown();
        Debug.Log($"LocalClientRuntime: 房间结束 {status}。");
        Status = status;
        RefreshHud();
    }

    void RefreshHud()
    {
        if (_world == null)
            return;

        if (_gameplay != null && _session != null)
            _gameplay.ObserveNetworkSample(_session.RttMs);

        NetMetricsSnapshot metrics = _session != null ? _session.TransportMetrics : default;
        int lossPermille = metrics.PacketsReceived > 0
            ? (int)(metrics.PacketsDropped * 1000L / metrics.PacketsReceived)
            : -1;
        _world.RoomHud = new ReplicationRoomHudInfo(
            true,
            _hudRole,
            Status,
            LastAuthorityFrame,
            _session?.RttMs ?? -1,
            SelfHealthMilli,
            LastTickBytes,
            LastCommandBytes,
            ProxyCount,
            PredictionPendingCount,
            _session?.JitterMs ?? -1,
            lossPermille,
            _gameplay != null ? _gameplay.InterpolationDelayMs : -1,
            _gameplay != null ? _gameplay.PredictionSnapCount : 0,
            _gameplay != null ? _gameplay.PredictionReplayCount : 0);
    }
}
