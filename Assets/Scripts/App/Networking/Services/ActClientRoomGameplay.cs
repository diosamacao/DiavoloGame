using System;
using UnityEngine;

/// <summary>Client ACT Gameplay 门面：组合 Owner 预测、Observer 复制与可靠反馈协调器。</summary>
public sealed class ActClientRoomGameplay
{
    readonly ACTGameArchitecture _architecture;
    readonly OwnerPredictionCoordinator _owner;
    readonly ObserverReplicationCoordinator _observer;
    readonly ReplicatedFeedbackCoordinator _feedback;
    PlayerController _localPlayer;

    /// <summary>创建 Client Gameplay 组合根，并装配三个唯一协调器。</summary>
    public ActClientRoomGameplay(
        CombatWorldController world,
        ACTGameArchitecture architecture,
        Transform proxyParent,
        GameContentCatalog content)
    {
        if (world == null)
            throw new ArgumentNullException(nameof(world));
        if (architecture == null)
            throw new ArgumentNullException(nameof(architecture));
        if (content == null)
            throw new ArgumentNullException(nameof(content));
        _architecture = architecture;

        var characterSchema = new ActCharacterSnapshotSchema(content);
        var observerAdapter = new ActObserverReplicationAdapter(
            content,
            characterSchema,
            () => world.SimulationHost,
            proxyParent,
            target => architecture.GetSystem<TargetSystem>()?.Register(target),
            target => architecture.GetSystem<TargetSystem>()?.Unregister(target));
        _feedback = new ReplicatedFeedbackCoordinator(world, content, observerAdapter);
        _owner = new OwnerPredictionCoordinator(
            world,
            new ActOwnerReplicationAdapter(content),
            _feedback);
        _observer = new ObserverReplicationCoordinator(
            world,
            _owner,
            observerAdapter,
            characterSchema);
        _localPlayer = architecture.SendQuery(new GetLocalPlayerQuery()) as PlayerController;
    }

    /// <summary>最近成功应用的权威帧；尚未入房时为 -1。</summary>
    public long LastAuthorityFrame => _observer.LastAuthorityFrame;

    /// <summary>最近完整下行应用消息字节；尚未收到时为 -1。</summary>
    public int LastTickBytes => _observer.LastTickBytes;

    /// <summary>最近完整上行命令消息字节；尚未发送时为 -1。</summary>
    public int LastCommandBytes => _owner.LastCommandBytes;

    /// <summary>Owner 最近权威生命值。</summary>
    public int SelfHealthMilli => _owner.SelfHealthMilli;

    /// <summary>当前 Observer Proxy 数量。</summary>
    public int ProxyCount => _observer.ProxyCount;

    /// <summary>Owner 尚未确认的动作与位移预测总数。</summary>
    public int PredictionPendingCount => _owner.PendingCount;

    /// <summary>走跑 Restore 次数。</summary>
    public int PredictionSnapCount => _owner.LocomotionSnapCount;

    /// <summary>走跑 Replay 命令累计。</summary>
    public int PredictionReplayCount => _owner.LocomotionReplayCount;

    /// <summary>远端网络插值延迟毫秒；Listen 对外报告 0。</summary>
    public int InterpolationDelayMs => _observer.InterpolationDelayMs;

    /// <summary>最近一次复制应用拒绝原因。</summary>
    public string LastRejectMessage => _observer.LastRejectMessage;

    /// <summary>按权威 Id 取 Observer 可见体。</summary>
    public bool TryGetProxy(SimActorId actorId, out RemoteCharacterProxy proxy) =>
        _observer.TryGetProxy(actorId, out proxy);

    /// <summary>Session Join 后初始化内容、本机玩家及 Owner/Observer 时钟。</summary>
    public void BeginSession(in SessionJoinAccept accept)
    {
        // PlayerController 可能晚于 Room Start 登记；Join 时只重查运行时玩家，不再重建内容。
        _localPlayer = _localPlayer
            ?? _architecture.SendQuery(new GetLocalPlayerQuery()) as PlayerController;
        _owner.BeginSession(in accept, _localPlayer);
        _observer.BeginSession(in accept, _localPlayer);
    }

    /// <summary>渲染帧采样下一预测帧输入。</summary>
    public void SampleRenderInput() => _owner.SampleRenderInput();

    /// <summary>构建下一份冗余 ClientCommand 正文。</summary>
    public bool TryBuildCommand(out byte[] commandBody) =>
        _owner.TryBuildCommand(out commandBody);

    /// <summary>命令发送后推进本机 Owner 预测。</summary>
    public void StepPrediction() => _owner.StepPrediction();

    /// <summary>应用可靠 V2 生命周期。</summary>
    public ActClientReplicationApplyStatus ApplyReplicationLifecycle(byte[] body) =>
        _observer.ApplyLifecycle(body);

    /// <summary>应用或缓冲 V2 Snapshot。</summary>
    public ActClientReplicationApplyStatus ApplyReplicationSnapshot(byte[] body) =>
        _observer.ApplySnapshot(body);

    /// <summary>应用可靠命中事件。</summary>
    public void ApplyReplicationEvents(byte[] body) =>
        _feedback.ApplyReplicationEvents(body);

    /// <summary>用最近心跳 RTT 刷新 Observer 插值延迟。</summary>
    public void ObserveNetworkSample(int rttMs) =>
        _observer.ObserveNetworkSample(rttMs);

    /// <summary>渲染 Owner 阵容与 Observer Proxy。</summary>
    public void Render() => _observer.Render();

    /// <summary>清空复制与预测状态，等待权威完整恢复帧。</summary>
    public void ResetReplicationForRecovery()
    {
        // 保持旧单体顺序：Proxy 释放后重置 Owner，再清协议 Registry 与跨通道反馈。
        _observer.DisposeViewsForRecovery();
        _owner.ResetForRecovery();
        _observer.ResetClientForRecovery();
        _feedback.ResetForRecovery();
    }

    /// <summary>释放 Observer，并清空 Owner 与可靠反馈状态。</summary>
    public void Shutdown()
    {
        _observer.Shutdown();
        _owner.Shutdown();
        _feedback.Shutdown();
    }
}
