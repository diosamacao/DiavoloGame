using System;

/// <summary>Transport 接收队列中的连接、通道与不可变载荷快照。</summary>
public readonly struct NetPacket
{
    /// <summary>创建已完成边界复制的接收包。</summary>
    public NetPacket(NetConnectionId connectionId, NetChannel channel, byte[] payload)
    {
        if (!connectionId.IsValid)
            throw new ArgumentException("接收包必须关联有效连接。", nameof(connectionId));
        ConnectionId = connectionId;
        Channel = channel;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    /// <summary>该 Transport 本地作用域内的连接标识。</summary>
    public NetConnectionId ConnectionId { get; }

    /// <summary>发送语义；旧 UDP 数据报接收时为 Unspecified。</summary>
    public NetChannel Channel { get; }

    /// <summary>由 Transport 持有的独立载荷数组。</summary>
    public byte[] Payload { get; }
}
