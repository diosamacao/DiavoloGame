using System;

/// <summary>
/// ClientCommand 的无引擎小端编解码；首字节为协议版本。
/// </summary>
public static class ReplicationCodec
{
    const byte Version = 1;

    /// <summary>编码上行命令。</summary>
    public static byte[] WriteClientCommand(in ClientCommand command)
    {
        var writer = new NetBufferWriter(64);
        writer.WriteByte(Version);
        writer.WriteInt64(command.FrameHint);
        writer.WriteInt32(command.SenderPlayerId);
        WriteInputFrame(writer, command.Input);
        return writer.ToArray();
    }

    /// <summary>解码上行命令；版本不匹配时抛错。</summary>
    public static ClientCommand ReadClientCommand(byte[] payload)
    {
        var reader = new NetBufferReader(payload);
        ReadVersion(reader);
        long frameHint = reader.ReadInt64();
        int sender = reader.ReadInt32();
        InputFrame input = ReadInputFrame(reader);
        reader.EnsureComplete();
        return new ClientCommand(frameHint, sender, in input);
    }

    /// <summary>验证复制正文的独立 Codec 版本。</summary>
    static void ReadVersion(NetBufferReader reader)
    {
        byte version = reader.ReadByte();
        if (version != Version)
            throw new InvalidOperationException($"复制协议版本不支持：{version}。");
    }

    /// <summary>按既有字段顺序编码量化输入。</summary>
    static void WriteInputFrame(NetBufferWriter writer, in InputFrame input)
    {
        writer.WriteInt64(input.Frame);
        WriteActorId(writer, input.ActorId);
        writer.WriteSByte(input.MoveX);
        writer.WriteSByte(input.MoveY);
        writer.WriteUInt64(input.ButtonsPressed);
        writer.WriteUInt64(input.ButtonsHeld);
        writer.WriteUInt64(input.ButtonsReleased);
        writer.WriteUInt16(input.MoveReferenceYawQuantized);
    }

    /// <summary>按既有字段顺序解码量化输入。</summary>
    static InputFrame ReadInputFrame(NetBufferReader reader)
    {
        long frame = reader.ReadInt64();
        SimActorId actorId = ReadActorId(reader);
        sbyte moveX = reader.ReadSByte();
        sbyte moveY = reader.ReadSByte();
        ulong pressed = reader.ReadUInt64();
        ulong held = reader.ReadUInt64();
        ulong released = reader.ReadUInt64();
        ushort yaw = reader.ReadUInt16();
        return new InputFrame(frame, actorId, moveX, moveY, pressed, held, released, yaw);
    }

    /// <summary>保持旧协议语义：非正 Actor Id 在线上还原为 Invalid。</summary>
    static SimActorId ReadActorId(NetBufferReader reader)
    {
        int value = reader.ReadInt32();
        return value <= 0 ? SimActorId.Invalid : new SimActorId(value);
    }

    /// <summary>把 ACTGame Actor Id 作为既有 int32 线值写入。</summary>
    static void WriteActorId(NetBufferWriter writer, SimActorId id) =>
        writer.WriteInt32(id.Value);
}
