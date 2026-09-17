using NUnit.Framework;

/// <summary>验证 V2 角色 Schema 使用 int32 LocomotionPhaseFrame。</summary>
public sealed class CharacterSnapshotSchemaV2Tests
{
    /// <summary>超过 ushort 的相位帧仍无损往返。</summary>
    [Test]
    public void PhaseFrame_Int32_RoundTrips()
    {
        var source = new ActorReplicationSnapshot(
            new SimActorId(1), 1, ReplicationActorKind.Player,
            0, 0, 0, 0, 0, 0,
            4, 2, 1,
            0, 0, 0, 0, SimActorId.Invalid, 1000, 0, VitalityReplicationEdge.None,
            locomotionPhaseFrame: 70000);
        var schema = new CharacterSnapshotSchemaV2();

        ActorReplicationSnapshot restored = schema.DecodeSnapshot(schema.Encode(in source));

        Assert.That(restored.LocomotionPhaseFrame, Is.EqualTo(70000));
        Assert.That(schema.SchemaId, Is.EqualTo(2));
    }
}
