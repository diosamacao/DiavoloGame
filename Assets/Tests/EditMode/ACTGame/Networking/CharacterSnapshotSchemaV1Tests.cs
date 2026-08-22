using System;
using NUnit.Framework;

/// <summary>验证角色快照 Schema 的字段完整性、Registry 接入和严格载荷边界。</summary>
public sealed class CharacterSnapshotSchemaV1Tests
{
    /// <summary>版本 1 Schema 往返保留 ActorReplicationSnapshot 的全部 21 个字段。</summary>
    [Test]
    public void RoundTrip_PreservesAllSnapshotFields()
    {
        ActorReplicationSnapshot source = CreateSnapshot();
        var schema = new CharacterSnapshotSchemaV1();

        ActorReplicationSnapshot restored = schema.DecodeSnapshot(schema.Encode(in source));

        Assert.That(restored.Equals(source), Is.True);
        Assert.That(restored.ActorId, Is.EqualTo(source.ActorId));
        Assert.That(restored.TeamId, Is.EqualTo(source.TeamId));
        Assert.That(restored.Kind, Is.EqualTo(source.Kind));
        Assert.That(restored.PosXMm, Is.EqualTo(source.PosXMm));
        Assert.That(restored.PosZMm, Is.EqualTo(source.PosZMm));
        Assert.That(restored.PosYMm, Is.EqualTo(source.PosYMm));
        Assert.That(restored.FacingMilliDeg, Is.EqualTo(source.FacingMilliDeg));
        Assert.That(restored.MoveVxMm, Is.EqualTo(source.MoveVxMm));
        Assert.That(restored.MoveVzMm, Is.EqualTo(source.MoveVzMm));
        Assert.That(restored.LocomotionPhase, Is.EqualTo(source.LocomotionPhase));
        Assert.That(restored.Gait, Is.EqualTo(source.Gait));
        Assert.That(restored.Cardinal, Is.EqualTo(source.Cardinal));
        Assert.That(restored.ActionId, Is.EqualTo(source.ActionId));
        Assert.That(restored.GraphNodeKey, Is.EqualTo(source.GraphNodeKey));
        Assert.That(restored.ActionFrame, Is.EqualTo(source.ActionFrame));
        Assert.That(restored.FreezeFrames, Is.EqualTo(source.FreezeFrames));
        Assert.That(restored.SelectedTargetId, Is.EqualTo(source.SelectedTargetId));
        Assert.That(restored.HealthMilli, Is.EqualTo(source.HealthMilli));
        Assert.That(restored.FlagsPacked, Is.EqualTo(source.FlagsPacked));
        Assert.That(restored.VitalityEdge, Is.EqualTo(source.VitalityEdge));
        Assert.That(
            restored.LocomotionNormalizedMilli,
            Is.EqualTo(source.LocomotionNormalizedMilli));
    }

    /// <summary>Schema 可通过通用 Registry 按 Id 编解码并返回角色快照。</summary>
    [Test]
    public void Registry_RoundTrip_ReturnsCharacterSnapshot()
    {
        ActorReplicationSnapshot source = CreateSnapshot();
        var registry = new ReplicationSchemaRegistry();
        registry.Register(new CharacterSnapshotSchemaV1());

        byte[] payload = registry.Encode(CharacterSnapshotSchemaV1.Id, source);
        object restored = registry.Decode(CharacterSnapshotSchemaV1.Id, payload);

        Assert.That(restored, Is.TypeOf<ActorReplicationSnapshot>());
        Assert.That(((ActorReplicationSnapshot)restored).Equals(source), Is.True);
    }

    /// <summary>object 入口明确拒绝 null 与非角色快照类型。</summary>
    [Test]
    public void EncodeObject_RejectsNullAndWrongType()
    {
        var schema = new CharacterSnapshotSchemaV1();

        Assert.Throws<ArgumentNullException>(() => schema.Encode((object)null));
        Assert.Throws<ArgumentException>(() => schema.Encode("not-a-snapshot"));
    }

    /// <summary>独立 Schema 载荷拒绝截断与任意尾随字节。</summary>
    [Test]
    public void Decode_RejectsTruncatedAndTrailingPayload()
    {
        ActorReplicationSnapshot source = CreateSnapshot();
        var schema = new CharacterSnapshotSchemaV1();
        byte[] payload = schema.Encode(in source);
        var truncated = new byte[payload.Length - 1];
        Buffer.BlockCopy(payload, 0, truncated, 0, truncated.Length);
        var trailing = new byte[payload.Length + 1];
        Buffer.BlockCopy(payload, 0, trailing, 0, payload.Length);
        trailing[trailing.Length - 1] = 0x7F;

        Assert.Throws<NetBufferException>(() => schema.DecodeSnapshot(truncated));
        Assert.Throws<NetBufferException>(() => schema.DecodeSnapshot(trailing));
    }

    /// <summary>Schema 与 Simulation Codec 必须输出完全相同的正文，避免重复布局真源。</summary>
    [Test]
    public void Encode_MatchesActorReplicationSnapshotCodecPayload()
    {
        ActorReplicationSnapshot source = CreateSnapshot();
        var schema = new CharacterSnapshotSchemaV1();

        byte[] expected = ActorReplicationSnapshotCodec.Encode(in source);
        byte[] actual = schema.Encode(in source);

        Assert.That(actual, Is.EqualTo(expected));
    }

    /// <summary>创建每个复制字段均具有辨识值的测试快照。</summary>
    static ActorReplicationSnapshot CreateSnapshot() =>
        new ActorReplicationSnapshot(
            new SimActorId(101),
            teamId: -7,
            ReplicationActorKind.Enemy,
            posXMm: 123456,
            posZMm: -234567,
            posYMm: 3456,
            facingMilliDeg: 271234,
            moveVxMm: -4567,
            moveVzMm: 5678,
            locomotionPhase: 6,
            gait: 7,
            cardinal: 8,
            actionId: 901,
            graphNodeKey: GraphNodeKey.FromStableName("Enemy/Heavy_二段"),
            actionFrame: 34,
            freezeFrames: 5,
            selectedTargetId: new SimActorId(202),
            healthMilli: 76543,
            flagsPacked: unchecked((int)0x89ABCDEF),
            VitalityReplicationEdge.Death,
            locomotionNormalizedMilli: 4321);
}
