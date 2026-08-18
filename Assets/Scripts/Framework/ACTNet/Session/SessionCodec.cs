using System;

/// <summary>通用 Session 信封及 Join、Heartbeat、Kick 控制消息编解码。</summary>
public static class SessionCodec
{
    /// <summary>冻结既有房间信封版本。</summary>
    public const byte EnvelopeVersion = 1;

    /// <summary>判断消息类型是否由 Session 状态机保留。</summary>
    public static bool IsControlMessage(byte messageType) =>
        messageType == (byte)SessionMessageKind.JoinRequest
        || messageType == (byte)SessionMessageKind.JoinAccept
        || messageType == (byte)SessionMessageKind.JoinReject
        || messageType == (byte)SessionMessageKind.Heartbeat
        || messageType == (byte)SessionMessageKind.Kick;

    /// <summary>包装两字节 Session 头与正文，不改变既有 UDP 字节布局。</summary>
    public static byte[] WriteEnvelope(byte messageType, byte[] body)
    {
        if (messageType == 0)
            throw new ArgumentOutOfRangeException(nameof(messageType));
        body ??= Array.Empty<byte>();

        var writer = new NetBufferWriter();
        writer.WriteByte(EnvelopeVersion);
        writer.WriteByte(messageType);
        writer.WriteBytes(body, 0, body.Length);
        return writer.ToArray();
    }

    /// <summary>校验版本并拆出消息类型和正文。</summary>
    public static void ReadEnvelope(byte[] payload, out byte messageType, out byte[] body)
    {
        var reader = new NetBufferReader(payload);
        byte version = reader.ReadByte();
        if (version != EnvelopeVersion)
            throw new InvalidOperationException($"Session 信封版本不支持：{version}。");

        messageType = reader.ReadByte();
        if (messageType == 0)
            throw new InvalidOperationException("Session 消息类型 0 无效。");
        body = reader.ReadBytes(reader.Remaining);
        reader.EnsureComplete();
    }

    /// <summary>编码 Join 请求。</summary>
    public static byte[] WriteJoinRequest(in SessionJoinRequest request)
    {
        var writer = new NetBufferWriter(8);
        writer.WriteInt32(request.ContentVersion);
        writer.WriteInt32(request.ProtocolVersion.Value);
        return WriteEnvelope((byte)SessionMessageKind.JoinRequest, writer.ToArray());
    }

    /// <summary>解码 Join 请求正文。</summary>
    public static SessionJoinRequest ReadJoinRequest(byte[] body)
    {
        var reader = new NetBufferReader(body);
        int contentVersion = reader.ReadInt32();
        var protocolVersion = new NetworkProtocolVersion(reader.ReadInt32());
        reader.EnsureComplete();
        return new SessionJoinRequest(contentVersion, protocolVersion);
    }

    /// <summary>编码 Join 成功消息。</summary>
    public static byte[] WriteJoinAccept(in SessionJoinAccept accept)
    {
        var writer = new NetBufferWriter(24);
        writer.WriteInt32(accept.PlayerId.Value);
        writer.WriteInt32(accept.EntityId.Value);
        writer.WriteInt32(accept.AuthorityEntityId.Value);
        writer.WriteInt32(accept.ContentVersion);
        writer.WriteInt64(accept.AuthorityTick.Value);
        return WriteEnvelope((byte)SessionMessageKind.JoinAccept, writer.ToArray());
    }

    /// <summary>解码 Join 成功正文。</summary>
    public static SessionJoinAccept ReadJoinAccept(byte[] body)
    {
        var reader = new NetBufferReader(body);
        var playerId = new NetPlayerId(reader.ReadInt32());
        var entityId = new NetEntityId(reader.ReadInt32());
        // Dedicated 无房主实体，线格式 0 表示 Invalid，不得 new NetEntityId(0)。
        NetEntityId authorityEntityId = ReadOptionalEntityId(reader);
        int contentVersion = reader.ReadInt32();
        var authorityTick = new NetTick(reader.ReadInt64());
        reader.EnsureComplete();
        return new SessionJoinAccept(
            playerId,
            entityId,
            authorityEntityId,
            contentVersion,
            authorityTick);
    }

    /// <summary>读取可空实体 Id；0 还原为 Invalid。</summary>
    static NetEntityId ReadOptionalEntityId(NetBufferReader reader)
    {
        int value = reader.ReadInt32();
        return value <= 0 ? NetEntityId.Invalid : new NetEntityId(value);
    }

    /// <summary>编码一字节 Join 拒绝原因。</summary>
    public static byte[] WriteJoinReject(SessionRejectReason reason) =>
        WriteEnvelope((byte)SessionMessageKind.JoinReject, new[] { (byte)reason });

    /// <summary>解码 Join 拒绝正文。</summary>
    public static SessionRejectReason ReadJoinReject(byte[] body)
    {
        var reader = new NetBufferReader(body);
        var reason = (SessionRejectReason)reader.ReadByte();
        reader.EnsureComplete();
        return reason;
    }

    /// <summary>编码心跳请求或回显。</summary>
    public static byte[] WriteHeartbeat(in SessionHeartbeat heartbeat)
    {
        var writer = new NetBufferWriter(16);
        writer.WriteInt64(heartbeat.SendTimeMs);
        writer.WriteInt64(heartbeat.EchoTimeMs);
        return WriteEnvelope((byte)SessionMessageKind.Heartbeat, writer.ToArray());
    }

    /// <summary>解码心跳正文。</summary>
    public static SessionHeartbeat ReadHeartbeat(byte[] body)
    {
        var reader = new NetBufferReader(body);
        long sendTimeMs = reader.ReadInt64();
        long echoTimeMs = reader.ReadInt64();
        reader.EnsureComplete();
        return new SessionHeartbeat(sendTimeMs, echoTimeMs);
    }

    /// <summary>编码一字节服务端 Kick 原因。</summary>
    public static byte[] WriteKick(SessionKickReason reason) =>
        WriteEnvelope((byte)SessionMessageKind.Kick, new[] { (byte)reason });

    /// <summary>解码服务端 Kick 正文。</summary>
    public static SessionKickReason ReadKick(byte[] body)
    {
        var reader = new NetBufferReader(body);
        var reason = (SessionKickReason)reader.ReadByte();
        reader.EnsureComplete();
        return reason;
    }
}
