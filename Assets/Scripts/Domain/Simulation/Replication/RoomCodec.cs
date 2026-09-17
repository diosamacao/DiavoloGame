using System;
using System.Collections.Generic;

/// <summary>
/// ACT 上行 ClientCommand V2 批编解码与 MatchEnd 控制消息。
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

        var writer = new NetBufferWriter();
        writer.WriteByte(2);
        writer.WriteByte((byte)commands.Count);
        for (int i = 0; i < commands.Count; i++)
            WriteClientCommand(writer, commands[i]);
        return writer.ToArray();
    }

    /// <summary>解码命令批；按 FrameHint 原序返回，Host 再过滤已应用项。</summary>
    public static ClientCommand[] ReadClientCommandBatch(byte[] body)
    {
        var reader = new NetBufferReader(body);
        byte version = reader.ReadByte();
        if (version != 2)
            throw new InvalidOperationException($"仅支持房间命令 V2，收到 {version}。");
        int count = reader.ReadByte();
        if (count < 1 || count > 8)
            throw new InvalidOperationException($"房间命令批数量非法：{count}。");

        var commands = new ClientCommand[count];
        for (int i = 0; i < count; i++)
        {
            commands[i] = ReadClientCommand(reader);
        }

        reader.EnsureComplete();
        return commands;
    }

    /// <summary>按 V2 固定字段写入单条量化命令。</summary>
    static void WriteClientCommand(NetBufferWriter writer, in ClientCommand command)
    {
        InputFrame input = command.Input;
        writer.WriteInt64(command.FrameHint);
        writer.WriteInt32(command.SenderPlayerId);
        writer.WriteInt64(input.Frame);
        writer.WriteInt32(input.ActorId.Value);
        writer.WriteSByte(input.MoveX);
        writer.WriteSByte(input.MoveY);
        writer.WriteUInt64(input.ButtonsPressed);
        writer.WriteUInt64(input.ButtonsHeld);
        writer.WriteUInt64(input.ButtonsReleased);
        writer.WriteUInt16(input.MoveReferenceYawQuantized);
    }

    /// <summary>按 V2 固定字段读取单条量化命令。</summary>
    static ClientCommand ReadClientCommand(NetBufferReader reader)
    {
        long hint = reader.ReadInt64();
        int sender = reader.ReadInt32();
        long frame = reader.ReadInt64();
        int actor = reader.ReadInt32();
        var input = new InputFrame(
            frame,
            actor > 0 ? new SimActorId(actor) : SimActorId.Invalid,
            reader.ReadSByte(),
            reader.ReadSByte(),
            reader.ReadUInt64(),
            reader.ReadUInt64(),
            reader.ReadUInt64(),
            reader.ReadUInt16());
        return new ClientCommand(hint, sender, in input);
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
