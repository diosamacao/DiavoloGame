using System;
using System.Collections.Generic;

/// <summary>Dedicated 权威世界门面：组合 Guest 注册、固定帧协调与逐连接复制发布。</summary>
public sealed class DedicatedAuthorityWorld : IDedicatedAuthorityWorld
{
    readonly AuthorityGuestRegistry _guests;
    readonly AuthorityReplicationPublisher _publisher;
    readonly AuthorityStepCoordinator _steps;
    bool _disposed;

    /// <summary>绑定 SimulationHost 与内容后创建唯一 Authority 运行组合。</summary>
    public DedicatedAuthorityWorld(
        SimulationHost host,
        ACTGameArchitecture architecture,
        GameContentCatalog content)
    {
        if (host == null)
            throw new ArgumentNullException(nameof(host));
        if (architecture == null)
            throw new ArgumentNullException(nameof(architecture));
        if (content == null)
            throw new ArgumentNullException(nameof(content));

        _guests = new AuthorityGuestRegistry(host, architecture, content);
        var authority = new ActAuthorityReplicationAdapter(content);
        _publisher = new AuthorityReplicationPublisher(host, _guests, authority);
        _steps = new AuthorityStepCoordinator(host, _guests, authority, _publisher);
    }

    /// <inheritdoc />
    public long CurrentFrame => _steps.CurrentFrame;

    /// <summary>最近一次 Runner 指标，供测试与诊断读取 overrun。</summary>
    public SimulationTickMetrics TickMetrics => _steps.TickMetrics;

    /// <inheritdoc />
    public float InterpolationAlpha => _steps.InterpolationAlpha;

    /// <inheritdoc />
    public bool TryAcceptPlayer(in MatchPlayerSlot slot, out NetEntityId entityId)
    {
        if (!_guests.TryAcceptPlayer(in slot, out entityId))
            return false;
        _publisher.RegisterConnection(slot.ConnectionId);
        return true;
    }

    /// <inheritdoc />
    public void ApplyCommands(NetConnectionId connectionId, ClientCommand[] commands) =>
        _steps.ApplyCommands(connectionId, commands);

    /// <inheritdoc />
    public void RemovePlayer(NetConnectionId connectionId)
    {
        _publisher.RemoveConnection(connectionId);
        _guests.Remove(connectionId);
    }

    /// <inheritdoc />
    public void RequestFullRecovery(NetConnectionId connectionId) =>
        _publisher.RequestFullRecovery(connectionId);

    /// <inheritdoc />
    public void Advance(long nowMs) => _steps.Advance(nowMs);

    /// <inheritdoc />
    public int PeekAdvanceSteps(long nowMs) => _steps.PeekAdvanceSteps(nowMs);

    /// <inheritdoc />
    public void PublishImmediateReplication() => _publisher.PublishImmediate();

    /// <inheritdoc />
    public void DrainOutboundReplication(List<DedicatedReplicationSend> results) =>
        _publisher.DrainReplication(results);

    /// <inheritdoc />
    public void ConfigureReplicationBodyBudget(int bodyBudgetBytes) =>
        _publisher.ConfigureBodyBudget(bodyBudgetBytes);

    /// <inheritdoc />
    public void CommitReplication(
        NetConnectionId connectionId,
        ReplicationPacketCommitToken token) =>
        _publisher.Commit(connectionId, token);

    /// <inheritdoc />
    public void RejectReplication(
        NetConnectionId connectionId,
        ReplicationPacketCommitToken token) =>
        _publisher.Reject(connectionId, token);

    /// <inheritdoc />
    public void DrainOutboundEvents(List<DedicatedEventSend> results) =>
        _publisher.DrainEvents(results);

    /// <summary>按协调器→复制票据→Guest 的顺序释放，避免事件回调访问已销毁 Actor。</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _steps.Dispose();
        _publisher.Dispose();
        _guests.Dispose();
    }
}
