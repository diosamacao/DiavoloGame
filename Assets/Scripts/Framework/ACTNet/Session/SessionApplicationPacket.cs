using System;

/// <summary>Session 向应用层交付的已拆信封消息。</summary>
public readonly struct SessionApplicationPacket
{
    /// <summary>创建应用消息。</summary>
    public SessionApplicationPacket(
        NetConnectionId connectionId,
        byte messageType,
        byte[] payload)
    {
        ConnectionId = connectionId;
        MessageType = messageType;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    /// <summary>消息所属连接。</summary>
    public NetConnectionId ConnectionId { get; }

    /// <summary>应用层自定义消息类型。</summary>
    public byte MessageType { get; }

    /// <summary>已移除 Session 信封的正文。</summary>
    public byte[] Payload { get; }
}
