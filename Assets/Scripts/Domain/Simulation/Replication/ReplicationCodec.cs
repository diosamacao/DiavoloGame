using System;

/// <summary>
/// ClientCommand / AuthorityTick 的无引擎小端编解码；首字节为协议版本。
/// 命中条目在 Key 之后带 ActionId 与毫米落点，供客机播受击 Cue。
/// </summary>
public static class ReplicationCodec
{
    const byte Version = 1;
    const int MaxActorsPerTick = 1024;
    const int MaxHitsPerTick = 1024;
    const int MaxLifecycleIdsPerTick = 1024;
    const int MaxGraphNodeIdBytes = 256;

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

    /// <summary>编码权威 Tick。</summary>
    public static byte[] WriteAuthorityTick(AuthorityTick tick)
    {
        if (tick == null)
            throw new ArgumentNullException(nameof(tick));

        var writer = new NetBufferWriter(128);
        writer.WriteByte(Version);
        writer.WriteInt64(tick.AuthorityFrame);
        writer.WriteInt32(tick.Actors.Length);
        for (int i = 0; i < tick.Actors.Length; i++)
            WriteSnapshot(writer, tick.Actors[i]);

        writer.WriteInt32(tick.Hits.Length);
        for (int i = 0; i < tick.Hits.Length; i++)
            WriteHit(writer, tick.Hits[i]);

        WriteIdArray(writer, tick.Spawns);
        WriteIdArray(writer, tick.Despawns);
        return writer.ToArray();
    }

    /// <summary>解码权威 Tick；版本不匹配时抛错。</summary>
    public static AuthorityTick ReadAuthorityTick(byte[] payload)
    {
        var reader = new NetBufferReader(payload);
        ReadVersion(reader);
        long frame = reader.ReadInt64();
        int actorCount = reader.ReadLength(MaxActorsPerTick);
        var actors = new ActorReplicationSnapshot[actorCount];
        for (int i = 0; i < actorCount; i++)
            actors[i] = ReadSnapshot(reader);

        int hitCount = reader.ReadLength(MaxHitsPerTick);
        var hits = new ReplicatedHitEvent[hitCount];
        for (int i = 0; i < hitCount; i++)
            hits[i] = ReadHit(reader);

        SimActorId[] spawns = ReadIdArray(reader);
        SimActorId[] despawns = ReadIdArray(reader);
        reader.EnsureComplete();
        return new AuthorityTick(frame, actors, hits, spawns, despawns);
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

    /// <summary>编码一个 ACTGame Actor 快照，不向 Core 泄漏 Gameplay 类型。</summary>
    static void WriteSnapshot(NetBufferWriter writer, in ActorReplicationSnapshot snapshot)
    {
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

    /// <summary>解码一个 ACTGame Actor 快照并校验可变长度字段。</summary>
    static ActorReplicationSnapshot ReadSnapshot(NetBufferReader reader)
    {
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

    /// <summary>编码权威命中事件及其稳定去重键。</summary>
    static void WriteHit(NetBufferWriter writer, in ReplicatedHitEvent hit)
    {
        writer.WriteInt64(hit.Frame);
        writer.WriteInt64(hit.Key.Frame);
        WriteActorId(writer, hit.Key.AttackerId);
        writer.WriteInt32(hit.Key.ActionInstanceId);
        writer.WriteInt32(hit.Key.HitboxIndex);
        WriteActorId(writer, hit.Key.TargetId);
        writer.WriteInt32(hit.ActionId);
        writer.WriteInt32(hit.HitXMm);
        writer.WriteInt32(hit.HitYMm);
        writer.WriteInt32(hit.HitZMm);
        writer.WriteInt32(hit.DirXMm);
        writer.WriteInt32(hit.DirZMm);
    }

    /// <summary>解码权威命中事件及其稳定去重键。</summary>
    static ReplicatedHitEvent ReadHit(NetBufferReader reader)
    {
        long frame = reader.ReadInt64();
        var key = new SimHitKey(
            reader.ReadInt64(),
            ReadActorId(reader),
            reader.ReadInt32(),
            reader.ReadInt32(),
            ReadActorId(reader));
        return new ReplicatedHitEvent(
            frame,
            key,
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32());
    }

    /// <summary>编码 Spawn / Despawn Actor Id 数组。</summary>
    static void WriteIdArray(NetBufferWriter writer, SimActorId[] ids)
    {
        writer.WriteInt32(ids.Length);
        for (int i = 0; i < ids.Length; i++)
            WriteActorId(writer, ids[i]);
    }

    /// <summary>解码受数量上限保护的 Spawn / Despawn Actor Id 数组。</summary>
    static SimActorId[] ReadIdArray(NetBufferReader reader)
    {
        int count = reader.ReadLength(MaxLifecycleIdsPerTick);
        if (count == 0)
            return Array.Empty<SimActorId>();

        var ids = new SimActorId[count];
        for (int i = 0; i < count; i++)
            ids[i] = ReadActorId(reader);
        return ids;
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
