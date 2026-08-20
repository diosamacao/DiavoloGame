using System;

/// <summary>严格编解码 ACT 复制帧级 Version 1 小端应用载荷。</summary>
public static class ActReplicationApplicationPayloadCodec
{
    /// <summary>当前唯一支持的 ACT 帧级应用载荷版本。</summary>
    public const byte Version = 1;

    /// <summary>单帧允许的最大权威命中事件数。</summary>
    public const int MaxHits = 1024;

    /// <summary>编码 applied hint 与命中数组；Tick 由外层 ReplicationFrame 唯一承载。</summary>
    public static byte[] Encode(ActReplicationApplicationPayload payload)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));
        if (payload.HitBuffer.Length > MaxHits)
            throw new NetBufferException($"命中数量 {payload.HitBuffer.Length} 超过上限 {MaxHits}。");

        ReplicatedHitEvent[] hits = payload.HitBuffer;
        var writer = new NetBufferWriter();
        writer.WriteByte(Version);
        writer.WriteInt64(payload.AppliedClientFrameHint);
        writer.WriteInt32(hits.Length);
        for (int i = 0; i < hits.Length; i++)
            ActReplicatedHitEventCodec.Write(writer, in hits[i]);
        return writer.ToArray();
    }

    /// <summary>严格解码完整 Version 1 载荷；拒绝 null、非法数量、截断与尾随字节。</summary>
    public static ActReplicationApplicationPayload Decode(byte[] payload)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));

        var reader = new NetBufferReader(payload);
        byte version = reader.ReadByte();
        if (version != Version)
            throw new InvalidOperationException($"不支持 ACT 复制应用载荷版本 {version}。");
        long appliedClientFrameHint = reader.ReadInt64();
        int hitCount = reader.ReadLength(MaxHits);
        var hits = new ReplicatedHitEvent[hitCount];
        for (int i = 0; i < hitCount; i++)
            hits[i] = ActReplicatedHitEventCodec.Read(reader);
        reader.EnsureComplete();
        return new ActReplicationApplicationPayload(appliedClientFrameHint, hits);
    }
}
