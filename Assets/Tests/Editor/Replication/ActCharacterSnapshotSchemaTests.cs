using System;
using NUnit.Framework;

/// <summary>验证生产 Character Schema 统一提供 Capture 门禁与 V1 线格式。</summary>
public sealed class ActCharacterSnapshotSchemaTests
{
    /// <summary>空 CharacterActor 必须在 Capture 边界立即拒绝。</summary>
    [Test]
    public void Capture_NullActor_Throws()
    {
        var schema = new ActCharacterSnapshotSchema(new ActContentRegistry());

        Assert.Throws<ArgumentNullException>(() => schema.Capture(null));
    }

    /// <summary>生产 Schema 编解码必须保持纯 C# CharacterSnapshotSchemaV1 的字段布局。</summary>
    [Test]
    public void EncodeDecode_RoundTrip_PreservesSnapshot()
    {
        var schema = new ActCharacterSnapshotSchema(new ActContentRegistry());
        var snapshot = new ActorReplicationSnapshot(
            new SimActorId(7),
            teamId: 2,
            kind: ReplicationActorKind.Player,
            posXMm: 100,
            posZMm: -200,
            posYMm: 300,
            facingMilliDeg: 45000,
            moveVxMm: 500,
            moveVzMm: 600,
            locomotionPhase: 1,
            gait: 2,
            cardinal: 3,
            actionId: 9,
            graphNodeId: "Attack/A",
            actionFrame: 4,
            freezeFrames: 1,
            selectedTargetId: new SimActorId(8),
            healthMilli: 99000,
            flagsPacked: 0,
            vitalityEdge: VitalityReplicationEdge.None,
            locomotionNormalizedMilli: 750);

        byte[] payload = schema.Encode(in snapshot);
        ActorReplicationSnapshot restored = schema.DecodeSnapshot(payload);

        Assert.That(restored, Is.EqualTo(snapshot));
        Assert.That(schema.SchemaId, Is.EqualTo(ActCharacterSnapshotSchema.Id));
    }
}
