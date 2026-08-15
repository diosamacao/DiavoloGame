using System;
using System.Collections.Generic;

/// <summary>
/// 房间信封编解码。不改 ReplicationCodec 布局：Tick/Command 作为正文嵌入。
/// ClientCommand 正文为命令批（count + 若干条已编码命令）。
/// </summary>
public static class RoomCodec
{
    /// <summary>包装 kind + 正文。</summary>
    public static byte[] WriteEnvelope(RoomMessageKind kind, byte[] body)
    {
        body ??= Array.Empty<byte>();
        var payload = new byte[2 + body.Length];
        payload[0] = ReplicationRoomProtocol.RoomCodecVersion;
        payload[1] = (byte)kind;
        if (body.Length > 0)
            Buffer.BlockCopy(body, 0, payload, 2, body.Length);
        return payload;
    }

    /// <summary>拆信封；版本不匹配时抛错。</summary>
    public static void ReadEnvelope(byte[] payload, out RoomMessageKind kind, out byte[] body)
    {
        if (payload == null || payload.Length < 2)
            throw new InvalidOperationException("房间载荷长度不足。");
        if (payload[0] != ReplicationRoomProtocol.RoomCodecVersion)
            throw new InvalidOperationException($"房间协议版本不支持：{payload[0]}。");

        kind = (RoomMessageKind)payload[1];
        int length = payload.Length - 2;
        body = new byte[length];
        if (length > 0)
            Buffer.BlockCopy(payload, 2, body, 0, length);
    }

    /// <summary>编码入房请求。</summary>
    public static byte[] WriteJoinRequest(in RoomJoinRequest request)
    {
        var body = new byte[8];
        int offset = 0;
        WriteInt32(body, ref offset, request.ContentVersion);
        WriteInt32(body, ref offset, request.ProtocolVersion);
        return WriteEnvelope(RoomMessageKind.JoinRequest, body);
    }

    /// <summary>解码入房请求。</summary>
    public static RoomJoinRequest ReadJoinRequest(byte[] body)
    {
        int offset = 0;
        int content = ReadInt32(body, ref offset);
        int protocol = ReadInt32(body, ref offset);
        return new RoomJoinRequest(content, protocol);
    }

    /// <summary>编码入房成功。</summary>
    public static byte[] WriteJoinAccept(in RoomJoinAccept accept)
    {
        var body = new byte[24];
        int offset = 0;
        WriteInt32(body, ref offset, accept.AssignedPlayerId);
        WriteInt32(body, ref offset, accept.AssignedActorId);
        WriteInt32(body, ref offset, accept.HostActorId);
        WriteInt32(body, ref offset, accept.ContentVersion);
        WriteInt64(body, ref offset, accept.AuthorityFrame);
        return WriteEnvelope(RoomMessageKind.JoinAccept, body);
    }

    /// <summary>解码入房成功。</summary>
    public static RoomJoinAccept ReadJoinAccept(byte[] body)
    {
        int offset = 0;
        int playerId = ReadInt32(body, ref offset);
        int actorId = ReadInt32(body, ref offset);
        int hostActorId = ReadInt32(body, ref offset);
        int content = ReadInt32(body, ref offset);
        long frame = ReadInt64(body, ref offset);
        return new RoomJoinAccept(playerId, actorId, hostActorId, content, frame);
    }

    /// <summary>编码入房拒绝。</summary>
    public static byte[] WriteJoinReject(in RoomJoinReject reject) =>
        WriteEnvelope(RoomMessageKind.JoinReject, new[] { (byte)reject.Reason });

    /// <summary>解码入房拒绝。</summary>
    public static RoomJoinReject ReadJoinReject(byte[] body)
    {
        if (body == null || body.Length < 1)
            throw new InvalidOperationException("房间拒绝载荷长度不足。");
        return new RoomJoinReject((RoomRejectReason)body[0]);
    }

    /// <summary>编码心跳。</summary>
    public static byte[] WriteHeartbeat(in RoomHeartbeat heartbeat)
    {
        var body = new byte[16];
        int offset = 0;
        WriteInt64(body, ref offset, heartbeat.SendTimeMs);
        WriteInt64(body, ref offset, heartbeat.EchoTimeMs);
        return WriteEnvelope(RoomMessageKind.Heartbeat, body);
    }

    /// <summary>解码心跳。</summary>
    public static RoomHeartbeat ReadHeartbeat(byte[] body)
    {
        int offset = 0;
        long send = ReadInt64(body, ref offset);
        long echo = ReadInt64(body, ref offset);
        return new RoomHeartbeat(send, echo);
    }

    /// <summary>编码踢出。</summary>
    public static byte[] WriteKick(in RoomKick kick) =>
        WriteEnvelope(RoomMessageKind.Kick, new[] { (byte)kick.Reason });

    /// <summary>解码踢出。</summary>
    public static RoomKick ReadKick(byte[] body)
    {
        if (body == null || body.Length < 1)
            throw new InvalidOperationException("房间踢出载荷长度不足。");
        return new RoomKick((RoomKickReason)body[0]);
    }

    /// <summary>包装已编码的单条 ClientCommand，正文仍是命令批（count=1）。</summary>
    public static byte[] WriteClientCommandEnvelope(byte[] commandBytes)
    {
        commandBytes ??= Array.Empty<byte>();
        return WriteClientCommandPayloads(new[] { commandBytes });
    }

    /// <summary>把最近若干条命令打成一批，供 UDP 冗余重发。</summary>
    public static byte[] WriteClientCommandBatch(IReadOnlyList<ClientCommand> commands)
    {
        if (commands == null || commands.Count == 0)
            throw new InvalidOperationException("房间命令批不能为空。");
        if (commands.Count > 8)
            throw new InvalidOperationException("房间命令批条数超过上限。");

        var payloads = new byte[commands.Count][];
        for (int i = 0; i < commands.Count; i++)
        {
            ClientCommand command = commands[i];
            payloads[i] = ReplicationCodec.WriteClientCommand(in command);
        }

        return WriteClientCommandPayloads(payloads);
    }

    /// <summary>解码命令批；按 FrameHint 原序返回，Host 再过滤已应用项。</summary>
    public static ClientCommand[] ReadClientCommandBatch(byte[] body)
    {
        int offset = 0;
        int count = ReadInt32(body, ref offset);
        if (count < 1 || count > 8)
            throw new InvalidOperationException($"房间命令批数量非法：{count}。");

        var commands = new ClientCommand[count];
        for (int i = 0; i < count; i++)
        {
            int length = ReadInt32(body, ref offset);
            Ensure(body, offset, length);
            if (length < 1)
                throw new InvalidOperationException("房间命令批条目长度非法。");

            var payload = new byte[length];
            Buffer.BlockCopy(body, offset, payload, 0, length);
            offset += length;
            commands[i] = ReplicationCodec.ReadClientCommand(payload);
        }

        return commands;
    }

    /// <summary>正文：int32 count + 每条 int32 长度与 ReplicationCodec 命令字节。</summary>
    static byte[] WriteClientCommandPayloads(byte[][] payloads)
    {
        int size = 4;
        for (int i = 0; i < payloads.Length; i++)
            size += 4 + (payloads[i] != null ? payloads[i].Length : 0);

        var body = new byte[size];
        int offset = 0;
        WriteInt32(body, ref offset, payloads.Length);
        for (int i = 0; i < payloads.Length; i++)
        {
            byte[] payload = payloads[i] ?? Array.Empty<byte>();
            WriteInt32(body, ref offset, payload.Length);
            if (payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, body, offset, payload.Length);
                offset += payload.Length;
            }
        }

        return WriteEnvelope(RoomMessageKind.ClientCommand, body);
    }

    /// <summary>
    /// 包装权威 Tick：先写客机本步被采用的 FrameHint，再跟 ReplicationCodec Tick 字节。
    /// appliedClientFrameHint=0 表示本步无新命令（CarryForward）。
    /// </summary>
    public static byte[] WriteAuthorityTickEnvelope(long appliedClientFrameHint, byte[] tickBytes)
    {
        tickBytes ??= Array.Empty<byte>();
        var body = new byte[8 + tickBytes.Length];
        int offset = 0;
        WriteInt64(body, ref offset, appliedClientFrameHint);
        if (tickBytes.Length > 0)
            Buffer.BlockCopy(tickBytes, 0, body, 8, tickBytes.Length);
        return WriteEnvelope(RoomMessageKind.AuthorityTick, body);
    }

    /// <summary>拆出 appliedFrameHint 与 Tick 正文。</summary>
    public static void ReadAuthorityTickEnvelope(
        byte[] body,
        out long appliedClientFrameHint,
        out byte[] tickBytes)
    {
        if (body == null || body.Length < 8)
            throw new InvalidOperationException("房间 Tick 载荷长度不足。");

        int offset = 0;
        appliedClientFrameHint = ReadInt64(body, ref offset);
        int tickLength = body.Length - 8;
        tickBytes = new byte[tickLength];
        if (tickLength > 0)
            Buffer.BlockCopy(body, 8, tickBytes, 0, tickLength);
    }

    static void WriteInt32(byte[] buffer, ref int offset, int value)
    {
        Ensure(buffer, offset, 4);
        unchecked
        {
            buffer[offset++] = (byte)value;
            buffer[offset++] = (byte)(value >> 8);
            buffer[offset++] = (byte)(value >> 16);
            buffer[offset++] = (byte)(value >> 24);
        }
    }

    static void WriteInt64(byte[] buffer, ref int offset, long value)
    {
        Ensure(buffer, offset, 8);
        unchecked
        {
            ulong u = (ulong)value;
            for (int i = 0; i < 8; i++)
                buffer[offset++] = (byte)(u >> (i * 8));
        }
    }

    static int ReadInt32(byte[] buffer, ref int offset)
    {
        Ensure(buffer, offset, 4);
        unchecked
        {
            int value = buffer[offset]
                | (buffer[offset + 1] << 8)
                | (buffer[offset + 2] << 16)
                | (buffer[offset + 3] << 24);
            offset += 4;
            return value;
        }
    }

    static long ReadInt64(byte[] buffer, ref int offset)
    {
        Ensure(buffer, offset, 8);
        ulong u = 0;
        for (int i = 0; i < 8; i++)
            u |= (ulong)buffer[offset++] << (i * 8);
        return unchecked((long)u);
    }

    static void Ensure(byte[] buffer, int offset, int additional)
    {
        if (buffer == null || offset + additional > buffer.Length)
            throw new InvalidOperationException("房间载荷长度不足。");
    }
}
