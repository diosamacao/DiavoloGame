/// <summary>权威命中事件的唯一线布局；Snapshot 载荷与可靠事件通道共用。</summary>
public static class ActReplicatedHitEventCodec
{
    /// <summary>写入 EventFrame → SimHitKey → ActionId → 落点 → 水平方向。</summary>
    public static void Write(NetBufferWriter writer, in ReplicatedHitEvent hit)
    {
        writer.WriteInt64(hit.Frame);
        writer.WriteInt64(hit.Key.Frame);
        writer.WriteInt32(hit.Key.AttackerId.Value);
        writer.WriteInt32(hit.Key.ActionInstanceId);
        writer.WriteInt32(hit.Key.HitboxIndex);
        writer.WriteInt32(hit.Key.TargetId.Value);
        writer.WriteInt32(hit.ActionId);
        writer.WriteInt32(hit.HitXMm);
        writer.WriteInt32(hit.HitYMm);
        writer.WriteInt32(hit.HitZMm);
        writer.WriteInt32(hit.DirXMm);
        writer.WriteInt32(hit.DirZMm);
    }

    /// <summary>与 Write 严格对称；非正 ActorId 按 Invalid 处理。</summary>
    public static ReplicatedHitEvent Read(NetBufferReader reader)
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

    static SimActorId ReadActorId(NetBufferReader reader)
    {
        int value = reader.ReadInt32();
        return value <= 0 ? SimActorId.Invalid : new SimActorId(value);
    }
}
