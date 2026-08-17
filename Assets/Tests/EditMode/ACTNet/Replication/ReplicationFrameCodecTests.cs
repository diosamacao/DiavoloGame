using System;
using NUnit.Framework;

/// <summary>验证 ReplicationFrame V1 小端布局、稳定排序与严格边界拒绝。</summary>
public sealed class ReplicationFrameCodecTests
{
    /// <summary>所有帧字段与三类记录必须按 EntityId 稳定排序后完整往返。</summary>
    [Test]
    public void EncodeDecode_RoundTrip_PreservesSortedFrame()
    {
        byte[] sourcePayload = { 7, 8 };
        var frame = new ReplicationFrame(
            new NetTick(123),
            new NetSequence(9),
            new[]
            {
                new SpawnRecord(new NetEntityId(3), new NetArchetypeId(20), 2, sourcePayload),
                new SpawnRecord(new NetEntityId(1), new NetArchetypeId(10), 1, new byte[] { 1 }),
            },
            new[]
            {
                new EntityRecord(new NetEntityId(4), 2, new byte[] { 4 }),
                new EntityRecord(new NetEntityId(2), 1, new byte[] { 2 }),
            },
            new[]
            {
                new DespawnRecord(new NetEntityId(8)),
                new DespawnRecord(new NetEntityId(6)),
            },
            new byte[] { 90, 91 });
        sourcePayload[0] = 99;

        byte[] encoded = ReplicationFrameCodec.Encode(frame);
        ReplicationFrame restored = ReplicationFrameCodec.Decode(encoded);

        Assert.That(restored.Tick.Value, Is.EqualTo(123));
        Assert.That(restored.Sequence.Value, Is.EqualTo(9));
        Assert.That(restored.Spawns[0].EntityId.Value, Is.EqualTo(1));
        Assert.That(restored.Spawns[1].EntityId.Value, Is.EqualTo(3));
        Assert.That(restored.Spawns[1].Payload, Is.EqualTo(new byte[] { 7, 8 }));
        Assert.That(restored.Updates[0].EntityId.Value, Is.EqualTo(2));
        Assert.That(restored.Despawns[0].EntityId.Value, Is.EqualTo(6));
        Assert.That(restored.ApplicationPayload, Is.EqualTo(new byte[] { 90, 91 }));
    }

    /// <summary>超过单记录 payload 上限的帧必须在编码边界被拒绝。</summary>
    [Test]
    public void Encode_OversizedRecordPayload_Throws()
    {
        var frame = new ReplicationFrame(
            new NetTick(1),
            new NetSequence(1),
            new[]
            {
                new SpawnRecord(
                    new NetEntityId(1),
                    new NetArchetypeId(1),
                    1,
                    new byte[ReplicationFrameCodec.MaxRecordPayloadBytes + 1]),
            },
            Array.Empty<EntityRecord>(),
            Array.Empty<DespawnRecord>(),
            Array.Empty<byte>());

        Assert.Throws<NetBufferException>(() => ReplicationFrameCodec.Encode(frame));
    }

    /// <summary>负数和超上限记录数量都必须在分配数组前被拒绝。</summary>
    [TestCase(-1)]
    [TestCase(ReplicationFrameCodec.MaxRecordsPerSection + 1)]
    public void Decode_InvalidSpawnCount_Throws(int count)
    {
        var writer = new NetBufferWriter(64, ReplicationFrameCodec.MaxFrameBytes);
        writer.WriteByte(ReplicationFrameCodec.Version);
        writer.WriteInt64(1);
        writer.WriteInt64(1);
        writer.WriteInt32(count);

        Assert.Throws<NetBufferException>(() =>
            ReplicationFrameCodec.Decode(writer.ToArray()));
    }

    /// <summary>声明长度超过字段上限的 payload 必须在读取正文前被拒绝。</summary>
    [Test]
    public void Decode_InvalidRecordPayloadLength_Throws()
    {
        var writer = new NetBufferWriter(64, ReplicationFrameCodec.MaxFrameBytes);
        writer.WriteByte(ReplicationFrameCodec.Version);
        writer.WriteInt64(1);
        writer.WriteInt64(1);
        writer.WriteInt32(1);
        writer.WriteInt32(1);
        writer.WriteInt32(1);
        writer.WriteUInt16(1);
        writer.WriteInt32(ReplicationFrameCodec.MaxRecordPayloadBytes + 1);

        Assert.Throws<NetBufferException>(() =>
            ReplicationFrameCodec.Decode(writer.ToArray()));
    }

    /// <summary>完整合法帧后的任何未声明尾随字节都必须被拒绝。</summary>
    [Test]
    public void Decode_TrailingByte_Throws()
    {
        var frame = new ReplicationFrame(
            new NetTick(1),
            new NetSequence(1),
            Array.Empty<SpawnRecord>(),
            Array.Empty<EntityRecord>(),
            Array.Empty<DespawnRecord>(),
            Array.Empty<byte>());
        byte[] valid = ReplicationFrameCodec.Encode(frame);
        var withTrailing = new byte[valid.Length + 1];
        Buffer.BlockCopy(valid, 0, withTrailing, 0, valid.Length);

        Assert.Throws<NetBufferException>(() =>
            ReplicationFrameCodec.Decode(withTrailing));
    }
}
