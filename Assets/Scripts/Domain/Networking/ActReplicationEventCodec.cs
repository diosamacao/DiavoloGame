using System;

/// <summary>可靠命中事件包：本帧权威命中，不含 Snapshot 冗余窗口。</summary>
public static class ActReplicationEventCodec
{
    /// <summary>当前唯一支持的事件包版本（命中线 Version 2 含 ReactionKind）。</summary>
    public const byte Version = 2;

    /// <summary>单包允许的最大命中数。</summary>
    public const int MaxHits = 1024;

    /// <summary>编码本帧命中数组。</summary>
    public static byte[] Encode(ReplicatedHitEvent[] hits)
    {
        hits ??= Array.Empty<ReplicatedHitEvent>();
        if (hits.Length > MaxHits)
            throw new NetBufferException($"命中数量 {hits.Length} 超过上限 {MaxHits}。");

        var writer = new NetBufferWriter();
        writer.WriteByte(Version);
        writer.WriteInt32(hits.Length);
        for (int i = 0; i < hits.Length; i++)
            ActReplicatedHitEventCodec.Write(writer, in hits[i]);
        return writer.ToArray();
    }

    /// <summary>严格解码完整事件包。</summary>
    public static ReplicatedHitEvent[] Decode(byte[] payload)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));

        var reader = new NetBufferReader(payload);
        byte version = reader.ReadByte();
        if (version != Version)
            throw new InvalidOperationException($"不支持复制事件版本 {version}。");

        int hitCount = reader.ReadLength(MaxHits);
        var hits = new ReplicatedHitEvent[hitCount];
        for (int i = 0; i < hitCount; i++)
            hits[i] = ActReplicatedHitEventCodec.Read(reader);
        reader.EnsureComplete();
        return hits;
    }
}
