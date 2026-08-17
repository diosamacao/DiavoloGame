using System;
using System.Collections.Generic;

/// <summary>
/// ACT 复制应用正文编解码；Session 信封与控制消息由 ACTNet.Session 独占。
/// ClientCommand 正文为命令批，AuthorityTick 正文额外携带 appliedFrameHint。
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
        return writer.ToArray();
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
