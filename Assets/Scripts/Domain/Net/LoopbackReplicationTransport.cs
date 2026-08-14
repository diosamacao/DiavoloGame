using System;
using System.Collections.Generic;

/// <summary>
/// 同进程复制传输：内存队列模拟延迟；不丢包除非 Delay 未 Pump。
/// NS1 用延迟 0 验证 Tick 帧号单调。
/// </summary>
public sealed class LoopbackReplicationTransport : IReplicationTransport
{
    readonly Queue<ScheduledPayload> _toAuthority = new();
    readonly Queue<ScheduledPayload> _toClient = new();
    readonly Queue<byte[]> _authorityReady = new();
    readonly Queue<byte[]> _clientReady = new();
    long _nowMs;
    int _latencyMs;

    /// <summary>单向延迟（毫秒）；0 表示 Pump 立即投递。</summary>
    public int LatencyMs => _latencyMs;

    /// <summary>设置单向延迟；负值按 0。</summary>
    public void SetLatencyMs(int latencyMs) =>
        _latencyMs = latencyMs < 0 ? 0 : latencyMs;

    /// <summary>推进模拟时钟，不自动 Pump。</summary>
    public void AdvanceTimeMs(int deltaMs)
    {
        if (deltaMs < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaMs));
        _nowMs += deltaMs;
    }

    /// <inheritdoc />
    public void SendClientToAuthority(byte[] payload) =>
        _toAuthority.Enqueue(Schedule(payload));

    /// <inheritdoc />
    public void SendAuthorityToClients(byte[] payload) =>
        _toClient.Enqueue(Schedule(payload));

    /// <inheritdoc />
    public void Pump()
    {
        DeliverDue(_toAuthority, _authorityReady);
        DeliverDue(_toClient, _clientReady);
    }

    /// <inheritdoc />
    public bool TryDequeueAuthority(out byte[] payload) =>
        TryDequeue(_authorityReady, out payload);

    /// <inheritdoc />
    public bool TryDequeueClient(out byte[] payload) =>
        TryDequeue(_clientReady, out payload);

    ScheduledPayload Schedule(byte[] payload)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));

        var copy = new byte[payload.Length];
        Buffer.BlockCopy(payload, 0, copy, 0, payload.Length);
        return new ScheduledPayload(_nowMs + _latencyMs, copy);
    }

    void DeliverDue(Queue<ScheduledPayload> pending, Queue<byte[]> ready)
    {
        while (pending.Count > 0 && pending.Peek().DeliverAtMs <= _nowMs)
            ready.Enqueue(pending.Dequeue().Payload);
    }

    static bool TryDequeue(Queue<byte[]> ready, out byte[] payload)
    {
        if (ready.Count == 0)
        {
            payload = null;
            return false;
        }

        payload = ready.Dequeue();
        return true;
    }

    readonly struct ScheduledPayload
    {
        public ScheduledPayload(long deliverAtMs, byte[] payload)
        {
            DeliverAtMs = deliverAtMs;
            Payload = payload;
        }

        public long DeliverAtMs { get; }
        public byte[] Payload { get; }
    }
}
