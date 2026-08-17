using System;

/// <summary>定义 ActorReplicationSnapshot 的唯一字段布局，并提供独立与嵌入式编解码。</summary>
public static class ActorReplicationSnapshotCodec
{
    const int MaxGraphNodeIdBytes = 256;

    /// <summary>把单份快照编码为无版本头、无外层长度的完整独立载荷。</summary>
    public static byte[] Encode(in ActorReplicationSnapshot snapshot)
    {
        var writer = new NetBufferWriter(128);
        WriteFields(writer, in snapshot);
        return writer.ToArray();
    }

    /// <summary>严格解码一份完整快照载荷；截断或存在尾随字节时抛错。</summary>
    public static ActorReplicationSnapshot Decode(byte[] payload)
    {
        var reader = new NetBufferReader(payload);
        ActorReplicationSnapshot snapshot = ReadFields(reader);
        reader.EnsureComplete();
        return snapshot;
    }

    /// <summary>按固定字段顺序写入快照字段；不写版本或长度，仅供外层协议嵌入。</summary>
    public static void WriteFields(NetBufferWriter writer, in ActorReplicationSnapshot snapshot)
    {
        if (writer == null)
            throw new ArgumentNullException(nameof(writer));

        WriteActorId(writer, snapshot.ActorId);
        writer.WriteInt32(snapshot.TeamId);
        writer.WriteByte((byte)snapshot.Kind);
        writer.WriteInt32(snapshot.PosXMm);
        writer.WriteInt32(snapshot.PosZMm);
        writer.WriteInt32(snapshot.PosYMm);
        writer.WriteInt32(snapshot.FacingMilliDeg);
        writer.WriteInt32(snapshot.MoveVxMm);
        writer.WriteInt32(snapshot.MoveVzMm);
        writer.WriteByte(snapshot.LocomotionPhase);
        writer.WriteByte(snapshot.Gait);
        writer.WriteByte(snapshot.Cardinal);
        writer.WriteInt32(snapshot.ActionId);
        writer.WriteString(snapshot.GraphNodeId, MaxGraphNodeIdBytes);
        writer.WriteInt32(snapshot.ActionFrame);
        writer.WriteInt32(snapshot.FreezeFrames);
        WriteActorId(writer, snapshot.SelectedTargetId);
        writer.WriteInt32(snapshot.HealthMilli);
        writer.WriteInt32(snapshot.FlagsPacked);
        writer.WriteByte((byte)snapshot.VitalityEdge);
        writer.WriteUInt16(snapshot.LocomotionNormalizedMilli);
    }

    /// <summary>按固定字段顺序读取快照字段；不校验外层尾部，仅供外层协议嵌入。</summary>
    public static ActorReplicationSnapshot ReadFields(NetBufferReader reader)
    {
        if (reader == null)
            throw new ArgumentNullException(nameof(reader));

        return new ActorReplicationSnapshot(
            ReadActorId(reader),
            reader.ReadInt32(),
            (ReplicationActorKind)reader.ReadByte(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadByte(),
            reader.ReadByte(),
            reader.ReadByte(),
            reader.ReadInt32(),
            reader.ReadString(MaxGraphNodeIdBytes),
            reader.ReadInt32(),
            reader.ReadInt32(),
            ReadActorId(reader),
            reader.ReadInt32(),
            reader.ReadInt32(),
            (VitalityReplicationEdge)reader.ReadByte(),
            reader.ReadUInt16());
    }

    /// <summary>保持既有协议语义：非正线值统一还原为 Invalid。</summary>
    static SimActorId ReadActorId(NetBufferReader reader)
    {
        int value = reader.ReadInt32();
        return value <= 0 ? SimActorId.Invalid : new SimActorId(value);
    }

    /// <summary>把模拟身份写为既有 int32 线值。</summary>
    static void WriteActorId(NetBufferWriter writer, SimActorId id) =>
        writer.WriteInt32(id.Value);
}
