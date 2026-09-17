using NUnit.Framework;

/// <summary>验证 V2 Snapshot Meta 只承载确认与权威阵容，不含命中分支。</summary>
public sealed class ActReplicationSnapshotMetaCodecTests
{
    /// <summary>Party flags、ActiveSlot 与 PartyWiped 严格往返。</summary>
    [Test]
    public void Meta_RoundTrip_PreservesPartyAuthority()
    {
        var source = new ActReplicationSnapshotMeta(
            7,
            9,
            new[] { new SimActorId(1), new SimActorId(2) },
            new[]
            {
                PartyReplicationPacking.WithMemberState(0, PartyMemberState.Dead),
                PartyReplicationPacking.WithMemberState(0, PartyMemberState.Active),
            },
            1,
            false);

        ActReplicationSnapshotMeta restored =
            ActReplicationSnapshotMetaCodec.Decode(ActReplicationSnapshotMetaCodec.Encode(source));

        Assert.That(restored.AppliedClientFrameHint, Is.EqualTo(7));
        Assert.That(restored.LastAppliedClientFrameHint, Is.EqualTo(9));
        Assert.That(restored.ActivePartySlot, Is.EqualTo(1));
        Assert.That(PartyReplicationPacking.ReadMemberState(restored.PartyFlags[0]), Is.EqualTo(PartyMemberState.Dead));
        Assert.That(typeof(ActReplicationSnapshotMeta).GetProperty("Hits"), Is.Null);
    }
}
