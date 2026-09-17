using System;

/// <summary>严格编解码 V2 ACT Snapshot Meta，不包含命中分支。</summary>
public static class ActReplicationSnapshotMetaCodec
{
    /// <summary>唯一 Meta 版本。</summary>
    public const byte Version = 2;

    /// <summary>编码 applied hints、阵容身份/flags、活动槽与队灭态。</summary>
    public static byte[] Encode(ActReplicationSnapshotMeta meta)
    {
        if (meta == null) throw new ArgumentNullException(nameof(meta));
        var writer = new NetBufferWriter(64);
        writer.WriteByte(Version);
        writer.WriteInt64(meta.AppliedClientFrameHint);
        writer.WriteInt64(meta.LastAppliedClientFrameHint);
        writer.WriteByte((byte)meta.PartyActorIdBuffer.Length);
        for (int i = 0; i < meta.PartyActorIdBuffer.Length; i++)
        {
            writer.WriteInt32(meta.PartyActorIdBuffer[i].Value);
            writer.WriteInt32(meta.PartyFlagBuffer[i]);
        }
        writer.WriteSByte((sbyte)meta.ActivePartySlot);
        writer.WriteByte(meta.PartyWiped ? (byte)1 : (byte)0);
        return writer.ToArray();
    }

    /// <summary>严格解码完整 V2 Meta。</summary>
    public static ActReplicationSnapshotMeta Decode(byte[] body)
    {
        var reader = new NetBufferReader(body);
        byte version = reader.ReadByte();
        if (version != Version) throw new InvalidOperationException($"仅支持 ACT Snapshot Meta V2，收到 {version}。");
        long applied = reader.ReadInt64();
        long lastApplied = reader.ReadInt64();
        int count = reader.ReadByte();
        if (count > PartyLoadoutRules.MaxMembers) throw new NetBufferException("Party Meta 数量超限。");
        var ids = new SimActorId[count];
        var flags = new int[count];
        for (int i = 0; i < count; i++)
        {
            int id = reader.ReadInt32();
            ids[i] = id > 0 ? new SimActorId(id) : SimActorId.Invalid;
            flags[i] = reader.ReadInt32();
        }
        int active = reader.ReadSByte();
        bool wiped = reader.ReadByte() != 0;
        reader.EnsureComplete();
        return new ActReplicationSnapshotMeta(applied, lastApplied, ids, flags, active, wiped);
    }
}
