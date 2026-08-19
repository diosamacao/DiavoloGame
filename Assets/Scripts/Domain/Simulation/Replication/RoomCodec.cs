using System;
using System.Collections.Generic;

/// <summary>
/// ACT 上行 ClientCommand 批编解码；下行由 ACTNet.ReplicationFrameCodec 独占。
/// </summary>
public static class RoomCodec
{
    /// <summary>把最近若干条命令编码为应用正文，供 Session 冗余上行。</summary>
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

        return writer.ToArray();
    }

    /// <summary>编码 MatchEnd：reason 一字节 + tick int64。</summary>
    public static byte[] WriteMatchEnd(in MatchEndMessage message)
    {
        var writer = new NetBufferWriter();
        writer.WriteByte((byte)message.Reason);
        writer.WriteInt64(message.Tick);
        return writer.ToArray();
    }

    /// <summary>解码 MatchEnd；拒绝空正文、未知原因与尾随字节。</summary>
    public static MatchEndMessage ReadMatchEnd(byte[] body)
    {
        if (body == null || body.Length == 0)
            throw new InvalidOperationException("MatchEnd 正文不能为空。");

        var reader = new NetBufferReader(body);
        byte reasonByte = reader.ReadByte();
        if (reasonByte < (byte)MatchEndReason.EmptyRoom
            || reasonByte > (byte)MatchEndReason.ServerShutdown)
        {
            throw new InvalidOperationException($"未知 MatchEnd 原因：{reasonByte}。");
        }

        long tick = reader.ReadInt64();
        reader.EnsureComplete();
        return new MatchEndMessage((MatchEndReason)reasonByte, tick);
    }

}
