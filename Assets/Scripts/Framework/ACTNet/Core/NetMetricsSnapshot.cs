using System;

/// <summary>某一采样时刻的只读通用网络计数；不包含 Gameplay 字段。</summary>
public readonly struct NetMetricsSnapshot : IEquatable<NetMetricsSnapshot>
{
    /// <summary>创建连接、流量、丢包和时延指标快照。</summary>
    public NetMetricsSnapshot(
        int connectionCount,
        long bytesSent,
        long bytesReceived,
        long packetsSent,
        long packetsReceived,
        long packetsDropped,
        int rttMs,
        int jitterMs)
    {
        if (connectionCount < 0
            || bytesSent < 0
            || bytesReceived < 0
            || packetsSent < 0
            || packetsReceived < 0
            || packetsDropped < 0
            || rttMs < -1
            || jitterMs < -1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(connectionCount),
                "累计指标不能为负，RTT 与 jitter 仅允许 -1 表示未知。");
        }

        ConnectionCount = connectionCount;
        BytesSent = bytesSent;
        BytesReceived = bytesReceived;
        PacketsSent = packetsSent;
        PacketsReceived = packetsReceived;
        PacketsDropped = packetsDropped;
        RttMs = rttMs;
        JitterMs = jitterMs;
    }

    /// <summary>当前连接数。</summary>
    public int ConnectionCount { get; }

    /// <summary>累计发送字节。</summary>
    public long BytesSent { get; }

    /// <summary>累计接收字节。</summary>
    public long BytesReceived { get; }

    /// <summary>累计发送包数。</summary>
    public long PacketsSent { get; }

    /// <summary>累计接收包数。</summary>
    public long PacketsReceived { get; }

    /// <summary>累计检测到的丢弃包数。</summary>
    public long PacketsDropped { get; }

    /// <summary>最近 RTT 毫秒；未知为 -1。</summary>
    public int RttMs { get; }

    /// <summary>最近 jitter 毫秒；未知为 -1。</summary>
    public int JitterMs { get; }

    /// <inheritdoc />
    public bool Equals(NetMetricsSnapshot other) =>
        ConnectionCount == other.ConnectionCount
        && BytesSent == other.BytesSent
        && BytesReceived == other.BytesReceived
        && PacketsSent == other.PacketsSent
        && PacketsReceived == other.PacketsReceived
        && PacketsDropped == other.PacketsDropped
        && RttMs == other.RttMs
        && JitterMs == other.JitterMs;

    /// <inheritdoc />
    public override bool Equals(object obj) =>
        obj is NetMetricsSnapshot other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => BytesSent.GetHashCode() ^ BytesReceived.GetHashCode();
}
