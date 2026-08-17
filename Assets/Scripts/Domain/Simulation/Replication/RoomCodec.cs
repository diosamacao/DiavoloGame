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
        var writer = new NetBufferWriter();
        writer.WriteByte(ReplicationRoomProtocol.RoomCodecVersion);
        writer.WriteByte((byte)kind);
        writer.WriteBytes(body, 0, body.Length);
        return writer.ToArray();
    }

    /// <summary>拆信封；版本不匹配时抛错。</summary>
    public static void ReadEnvelope(byte[] payload, out RoomMessageKind kind, out byte[] body)
    {
        var reader = new NetBufferReader(payload);
        byte version = reader.ReadByte();
        if (version != ReplicationRoomProtocol.RoomCodecVersion)
            throw new InvalidOperationException($"房间协议版本不支持：{version}。");

        kind = (RoomMessageKind)reader.ReadByte();
        body = reader.ReadBytes(reader.Remaining);
        reader.EnsureComplete();
    }

    /// <summary>编码入房请求。</summary>
    public static byte[] WriteJoinRequest(in RoomJoinRequest request)
    {
        var writer = new NetBufferWriter(8);
        writer.WriteInt32(request.ContentVersion);
        writer.WriteInt32(request.ProtocolVersion);
        return WriteEnvelope(RoomMessageKind.JoinRequest, writer.ToArray());
    }

    /// <summary>解码入房请求。</summary>
    public static RoomJoinRequest ReadJoinRequest(byte[] body)
    {
        var reader = new NetBufferReader(body);
        int content = reader.ReadInt32();
        int protocol = reader.ReadInt32();
        reader.EnsureComplete();
        return new RoomJoinRequest(content, protocol);
    }

    /// <summary>编码入房成功。</summary>
    public static byte[] WriteJoinAccept(in RoomJoinAccept accept)
    {
        var writer = new NetBufferWriter(24);
        writer.WriteInt32(accept.AssignedPlayerId);
        writer.WriteInt32(accept.AssignedActorId);
        writer.WriteInt32(accept.HostActorId);
        writer.WriteInt32(accept.ContentVersion);
        writer.WriteInt64(accept.AuthorityFrame);
        return WriteEnvelope(RoomMessageKind.JoinAccept, writer.ToArray());
    }

    /// <summary>解码入房成功。</summary>
    public static RoomJoinAccept ReadJoinAccept(byte[] body)
    {
        var reader = new NetBufferReader(body);
        int playerId = reader.ReadInt32();
        int actorId = reader.ReadInt32();
        int hostActorId = reader.ReadInt32();
        int content = reader.ReadInt32();
        long frame = reader.ReadInt64();
        reader.EnsureComplete();
        return new RoomJoinAccept(playerId, actorId, hostActorId, content, frame);
    }

    /// <summary>编码入房拒绝。</summary>
    public static byte[] WriteJoinReject(in RoomJoinReject reject) =>
        WriteEnvelope(RoomMessageKind.JoinReject, new[] { (byte)reject.Reason });

    /// <summary>解码入房拒绝。</summary>
    public static RoomJoinReject ReadJoinReject(byte[] body)
    {
        var reader = new NetBufferReader(body);
        var reject = new RoomJoinReject((RoomRejectReason)reader.ReadByte());
        reader.EnsureComplete();
        return reject;
    }

    /// <summary>编码心跳。</summary>
    public static byte[] WriteHeartbeat(in RoomHeartbeat heartbeat)
    {
        var writer = new NetBufferWriter(16);
        writer.WriteInt64(heartbeat.SendTimeMs);
        writer.WriteInt64(heartbeat.EchoTimeMs);
        return WriteEnvelope(RoomMessageKind.Heartbeat, writer.ToArray());
    }

    /// <summary>解码心跳。</summary>
    public static RoomHeartbeat ReadHeartbeat(byte[] body)
    {
        var reader = new NetBufferReader(body);
        long send = reader.ReadInt64();
        long echo = reader.ReadInt64();
        reader.EnsureComplete();
        return new RoomHeartbeat(send, echo);
    }

    /// <summary>编码踢出。</summary>
    public static byte[] WriteKick(in RoomKick kick) =>
        WriteEnvelope(RoomMessageKind.Kick, new[] { (byte)kick.Reason });

    /// <summary>解码踢出。</summary>
    public static RoomKick ReadKick(byte[] body)
    {
        var reader = new NetBufferReader(body);
        var kick = new RoomKick((RoomKickReason)reader.ReadByte());
        reader.EnsureComplete();
        return kick;
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
        var reader = new NetBufferReader(body);
        int count = reader.ReadLength(8);
        if (count < 1 || count > 8)
            throw new InvalidOperationException($"房间命令批数量非法：{count}。");

        var commands = new ClientCommand[count];
        for (int i = 0; i < count; i++)
        {
            int length = reader.ReadLength(NetBufferWriter.DefaultMaxPayloadBytes);
            if (length < 1)
                throw new InvalidOperationException("房间命令批条目长度非法。");

            byte[] payload = reader.ReadBytes(length);
            commands[i] = ReplicationCodec.ReadClientCommand(payload);
        }

        reader.EnsureComplete();
        return commands;
    }

    /// <summary>正文：int32 count + 每条 int32 长度与 ReplicationCodec 命令字节。</summary>
    static byte[] WriteClientCommandPayloads(byte[][] payloads)
    {
        var writer = new NetBufferWriter();
        writer.WriteInt32(payloads.Length);
        for (int i = 0; i < payloads.Length; i++)
        {
            byte[] payload = payloads[i] ?? Array.Empty<byte>();
            writer.WriteInt32(payload.Length);
            writer.WriteBytes(payload, 0, payload.Length);
        }

        return WriteEnvelope(RoomMessageKind.ClientCommand, writer.ToArray());
    }

    /// <summary>
    /// 包装权威 Tick：先写客机本步被采用的 FrameHint，再跟 ReplicationCodec Tick 字节。
    /// appliedClientFrameHint=0 表示本步无新命令（CarryForward）。
    /// </summary>
    public static byte[] WriteAuthorityTickEnvelope(long appliedClientFrameHint, byte[] tickBytes)
    {
        tickBytes ??= Array.Empty<byte>();
        var writer = new NetBufferWriter();
        writer.WriteInt64(appliedClientFrameHint);
        writer.WriteBytes(tickBytes, 0, tickBytes.Length);
        return WriteEnvelope(RoomMessageKind.AuthorityTick, writer.ToArray());
    }

    /// <summary>拆出 appliedFrameHint 与 Tick 正文。</summary>
    public static void ReadAuthorityTickEnvelope(
        byte[] body,
        out long appliedClientFrameHint,
        out byte[] tickBytes)
    {
        var reader = new NetBufferReader(body);
        appliedClientFrameHint = reader.ReadInt64();
        tickBytes = reader.ReadBytes(reader.Remaining);
        reader.EnsureComplete();
    }
}
