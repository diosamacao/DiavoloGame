using System;

/// <summary>V2 生命周期与快照的严格 MTU 内小端编解码器。</summary>
public static class ReplicationProtocolV2Codec
{
    /// <summary>当前唯一线格式版本。</summary>
    public const byte Version = 2;
    /// <summary>生命周期固定头字节数。</summary>
    public const int LifecycleHeaderBytes = 25;
    /// <summary>快照固定头字节数。</summary>
    public const int SnapshotHeaderBytes = 25;
    /// <summary>单条 Spawn 固定开销，不含 payload。</summary>
    public const int SpawnRecordHeaderBytes = 12;
    /// <summary>单条 Update 固定开销，不含 payload。</summary>
    public const int UpdateRecordHeaderBytes = 8;

    /// <summary>编码生命周期并证明正文不超过调用方预算。</summary>
    public static byte[] EncodeLifecycle(ReplicationLifecycle message, int bodyBudgetBytes)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        if (bodyBudgetBytes < LifecycleHeaderBytes)
            throw new ArgumentOutOfRangeException(nameof(bodyBudgetBytes));
        var writer = new NetBufferWriter(Math.Min(bodyBudgetBytes, 256), bodyBudgetBytes);
        writer.WriteByte(Version);
        writer.WriteInt64(message.LifecycleSequence);
        writer.WriteInt64(message.Tick.Value);
        writer.WriteUInt16(message.BatchIndex);
        writer.WriteUInt16(message.BatchCount);
        WriteSpawns(writer, message.SpawnBuffer);
        WriteDespawns(writer, message.DespawnBuffer);
        return writer.ToArray();
    }

    /// <summary>严格解码一个完整生命周期批。</summary>
    public static ReplicationLifecycle DecodeLifecycle(byte[] body)
    {
        var reader = new NetBufferReader(body);
        RequireVersion(reader);
        long sequence = reader.ReadInt64();
        var tick = new NetTick(reader.ReadInt64());
        ushort index = reader.ReadUInt16();
        ushort count = reader.ReadUInt16();
        SpawnRecord[] spawns = ReadSpawns(reader);
        DespawnRecord[] despawns = ReadDespawns(reader);
        reader.EnsureComplete();
        return new ReplicationLifecycle(sequence, tick, index, count, spawns, despawns);
    }

    /// <summary>编码快照并证明正文不超过调用方预算。</summary>
    public static byte[] EncodeSnapshot(ReplicationSnapshot message, int bodyBudgetBytes)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        if (bodyBudgetBytes < SnapshotHeaderBytes)
            throw new ArgumentOutOfRangeException(nameof(bodyBudgetBytes));
        var writer = new NetBufferWriter(Math.Min(bodyBudgetBytes, 256), bodyBudgetBytes);
        writer.WriteByte(Version);
        writer.WriteInt64(message.Tick.Value);
        writer.WriteInt64(message.RequiredLifecycleSequence);
        writer.WriteUInt16(message.BatchIndex);
        writer.WriteUInt16(message.BatchCount);
        EntityRecord[] updates = message.UpdateBuffer;
        if (updates.Length > ushort.MaxValue) throw new NetBufferException("Update 数量超限。");
        writer.WriteUInt16((ushort)updates.Length);
        for (int i = 0; i < updates.Length; i++)
        {
            EntityRecord record = updates[i];
            if (record.PayloadBuffer.Length > ushort.MaxValue) throw new NetBufferException("Update payload 超限。");
            writer.WriteInt32(record.EntityId.Value);
            writer.WriteUInt16(record.SchemaId);
            writer.WriteUInt16((ushort)record.PayloadBuffer.Length);
            writer.WriteBytes(record.PayloadBuffer, 0, record.PayloadBuffer.Length);
        }
        if (message.MetadataBuffer.Length > ushort.MaxValue) throw new NetBufferException("Meta 超限。");
        writer.WriteUInt16((ushort)message.MetadataBuffer.Length);
        writer.WriteBytes(message.MetadataBuffer, 0, message.MetadataBuffer.Length);
        return writer.ToArray();
    }

    /// <summary>严格解码一个完整快照批。</summary>
    public static ReplicationSnapshot DecodeSnapshot(byte[] body)
    {
        var reader = new NetBufferReader(body);
        RequireVersion(reader);
        var tick = new NetTick(reader.ReadInt64());
        long required = reader.ReadInt64();
        ushort index = reader.ReadUInt16();
        ushort count = reader.ReadUInt16();
        int updateCount = reader.ReadUInt16();
        var updates = new EntityRecord[updateCount];
        for (int i = 0; i < updateCount; i++)
        {
            updates[i] = new EntityRecord(
                new NetEntityId(reader.ReadInt32()),
                reader.ReadUInt16(),
                reader.ReadBytes(reader.ReadUInt16()));
        }
        byte[] metadata = reader.ReadBytes(reader.ReadUInt16());
        reader.EnsureComplete();
        return new ReplicationSnapshot(tick, required, index, count, updates, metadata);
    }

    static void WriteSpawns(NetBufferWriter writer, SpawnRecord[] records)
    {
        if (records.Length > ushort.MaxValue) throw new NetBufferException("Spawn 数量超限。");
        writer.WriteUInt16((ushort)records.Length);
        for (int i = 0; i < records.Length; i++)
        {
            SpawnRecord record = records[i];
            if (record.PayloadBuffer.Length > ushort.MaxValue) throw new NetBufferException("Spawn payload 超限。");
            writer.WriteInt32(record.EntityId.Value);
            writer.WriteInt32(record.ArchetypeId.Value);
            writer.WriteUInt16(record.SchemaId);
            writer.WriteUInt16((ushort)record.PayloadBuffer.Length);
            writer.WriteBytes(record.PayloadBuffer, 0, record.PayloadBuffer.Length);
        }
    }

    static SpawnRecord[] ReadSpawns(NetBufferReader reader)
    {
        int count = reader.ReadUInt16();
        var records = new SpawnRecord[count];
        for (int i = 0; i < count; i++)
        {
            records[i] = new SpawnRecord(
                new NetEntityId(reader.ReadInt32()),
                new NetArchetypeId(reader.ReadInt32()),
                reader.ReadUInt16(),
                reader.ReadBytes(reader.ReadUInt16()));
        }
        return records;
    }

    static void WriteDespawns(NetBufferWriter writer, DespawnRecord[] records)
    {
        if (records.Length > ushort.MaxValue) throw new NetBufferException("Despawn 数量超限。");
        writer.WriteUInt16((ushort)records.Length);
        for (int i = 0; i < records.Length; i++) writer.WriteInt32(records[i].EntityId.Value);
    }

    static DespawnRecord[] ReadDespawns(NetBufferReader reader)
    {
        int count = reader.ReadUInt16();
        var records = new DespawnRecord[count];
        for (int i = 0; i < count; i++) records[i] = new DespawnRecord(new NetEntityId(reader.ReadInt32()));
        return records;
    }

    static void RequireVersion(NetBufferReader reader)
    {
        byte version = reader.ReadByte();
        if (version != Version) throw new InvalidOperationException($"仅支持 replication protocol V2，收到 {version}。");
    }
}
