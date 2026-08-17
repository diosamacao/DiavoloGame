using System;

/// <summary>以 Version 1 纯小端布局严格编解码通用复制帧。</summary>
public static class ReplicationFrameCodec
{
    /// <summary>当前唯一支持的帧线格式版本。</summary>
    public const byte Version = 1;

    /// <summary>单个生命周期分区允许的最大记录数。</summary>
    public const int MaxRecordsPerSection = 4096;

    /// <summary>单条 Spawn 或 Update 状态载荷的最大字节数。</summary>
    public const int MaxRecordPayloadBytes = 64 * 1024;

    /// <summary>帧级应用附加载荷的最大字节数。</summary>
    public const int MaxApplicationPayloadBytes = 64 * 1024;

    /// <summary>完整编码帧允许的最大字节数。</summary>
    public const int MaxFrameBytes = 1024 * 1024;

    /// <summary>按固定 Version 1 布局编码帧，并执行数量、字段与总长门禁。</summary>
    public static byte[] Encode(ReplicationFrame frame)
    {
        if (frame == null)
            throw new ArgumentNullException(nameof(frame));

        ValidateCount(frame.SpawnBuffer.Length, nameof(frame.Spawns));
        ValidateCount(frame.UpdateBuffer.Length, nameof(frame.Updates));
        ValidateCount(frame.DespawnBuffer.Length, nameof(frame.Despawns));
        ValidateLength(
            frame.ApplicationPayloadBuffer.Length,
            MaxApplicationPayloadBytes,
            nameof(frame.ApplicationPayload));

        // V1 顺序固定为 header → spawns → updates → despawns → application payload。
        var writer = new NetBufferWriter(256, MaxFrameBytes);
        writer.WriteByte(Version);
        writer.WriteInt64(frame.Tick.Value);
        writer.WriteInt64(frame.Sequence.Value);
        WriteSpawns(writer, frame.SpawnBuffer);
        WriteUpdates(writer, frame.UpdateBuffer);
        WriteDespawns(writer, frame.DespawnBuffer);
        writer.WriteLengthPrefixedBytes(
            frame.ApplicationPayloadBuffer,
            MaxApplicationPayloadBytes);
        return writer.ToArray();
    }

    /// <summary>严格读取完整 Version 1 帧；拒绝超限、截断、非法 Id 与尾随字节。</summary>
    public static ReplicationFrame Decode(byte[] payload)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));

        var reader = new NetBufferReader(payload, MaxFrameBytes);
        byte version = reader.ReadByte();
        if (version != Version)
            throw new InvalidOperationException($"不支持 ReplicationFrame 版本 {version}。");

        var tick = new NetTick(reader.ReadInt64());
        var sequence = new NetSequence(reader.ReadInt64());
        SpawnRecord[] spawns = ReadSpawns(reader);
        EntityRecord[] updates = ReadUpdates(reader);
        DespawnRecord[] despawns = ReadDespawns(reader);
        byte[] applicationPayload =
            reader.ReadLengthPrefixedBytes(MaxApplicationPayloadBytes);
        reader.EnsureComplete();

        return new ReplicationFrame(
            tick,
            sequence,
            spawns,
            updates,
            despawns,
            applicationPayload);
    }

    // 写入带 Archetype 与首状态的 Spawn 分区。
    static void WriteSpawns(NetBufferWriter writer, SpawnRecord[] records)
    {
        writer.WriteInt32(records.Length);
        for (int i = 0; i < records.Length; i++)
        {
            SpawnRecord record = records[i];
            ValidateLength(
                record.PayloadBuffer.Length,
                MaxRecordPayloadBytes,
                nameof(record.Payload));
            writer.WriteInt32(record.EntityId.Value);
            writer.WriteInt32(record.ArchetypeId.Value);
            writer.WriteUInt16(record.SchemaId);
            writer.WriteLengthPrefixedBytes(record.PayloadBuffer, MaxRecordPayloadBytes);
        }
    }

    // 写入仅含实体、Schema 与状态的 Update 分区。
    static void WriteUpdates(NetBufferWriter writer, EntityRecord[] records)
    {
        writer.WriteInt32(records.Length);
        for (int i = 0; i < records.Length; i++)
        {
            EntityRecord record = records[i];
            ValidateLength(
                record.PayloadBuffer.Length,
                MaxRecordPayloadBytes,
                nameof(record.Payload));
            writer.WriteInt32(record.EntityId.Value);
            writer.WriteUInt16(record.SchemaId);
            writer.WriteLengthPrefixedBytes(record.PayloadBuffer, MaxRecordPayloadBytes);
        }
    }

    // 写入只以 EntityId 表达生命周期终止的 Despawn 分区。
    static void WriteDespawns(NetBufferWriter writer, DespawnRecord[] records)
    {
        writer.WriteInt32(records.Length);
        for (int i = 0; i < records.Length; i++)
            writer.WriteInt32(records[i].EntityId.Value);
    }

    // 在数量门禁后读取 Spawn，字段构造器继续验证 Id 与 Schema。
    static SpawnRecord[] ReadSpawns(NetBufferReader reader)
    {
        int count = ReadCount(reader);
        var records = new SpawnRecord[count];
        for (int i = 0; i < count; i++)
        {
            records[i] = new SpawnRecord(
                new NetEntityId(reader.ReadInt32()),
                new NetArchetypeId(reader.ReadInt32()),
                reader.ReadUInt16(),
                reader.ReadLengthPrefixedBytes(MaxRecordPayloadBytes));
        }

        return records;
    }

    // 在数量门禁后读取 Update，载荷长度由 Reader 在分配前验证。
    static EntityRecord[] ReadUpdates(NetBufferReader reader)
    {
        int count = ReadCount(reader);
        var records = new EntityRecord[count];
        for (int i = 0; i < count; i++)
        {
            records[i] = new EntityRecord(
                new NetEntityId(reader.ReadInt32()),
                reader.ReadUInt16(),
                reader.ReadLengthPrefixedBytes(MaxRecordPayloadBytes));
        }

        return records;
    }

    // 在数量门禁后读取显式 Despawn EntityId。
    static DespawnRecord[] ReadDespawns(NetBufferReader reader)
    {
        int count = ReadCount(reader);
        var records = new DespawnRecord[count];
        for (int i = 0; i < count; i++)
            records[i] = new DespawnRecord(new NetEntityId(reader.ReadInt32()));
        return records;
    }

    static int ReadCount(NetBufferReader reader)
    {
        int count = reader.ReadInt32();
        ValidateCount(count, "recordCount");
        return count;
    }

    static void ValidateCount(int count, string fieldName)
    {
        if (count < 0 || count > MaxRecordsPerSection)
        {
            throw new NetBufferException(
                $"{fieldName} 数量 {count} 不在 [0,{MaxRecordsPerSection}]。");
        }
    }

    static void ValidateLength(int length, int maximum, string fieldName)
    {
        if (length < 0 || length > maximum)
            throw new NetBufferException($"{fieldName} 长度 {length} 不在 [0,{maximum}]。");
    }
}
