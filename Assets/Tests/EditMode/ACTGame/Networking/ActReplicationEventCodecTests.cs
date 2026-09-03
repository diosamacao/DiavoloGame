using System;
using NUnit.Framework;

/// <summary>可靠命中事件包往返与版本门禁。</summary>
public sealed class ActReplicationEventCodecTests
{
    /// <summary>命中字段编码后可完整还原。</summary>
    [Test]
    public void RoundTrip_PreservesHits()
    {
        ReplicatedHitEvent hit = new(
            12,
            new SimHitKey(12, new SimActorId(1), 3, 0, new SimActorId(2)),
            7,
            HitReactionKind.Flinch,
            100,
            200,
            300,
            1000,
            0);
        ReplicatedHitEvent[] restored = ActReplicationEventCodec.Decode(
            ActReplicationEventCodec.Encode(new[] { hit }));
        Assert.That(restored, Has.Length.EqualTo(1));
        Assert.That(restored[0].Equals(hit), Is.True);
    }

    /// <summary>Flinch 档位字节往返一致。</summary>
    [Test]
    public void RoundTrip_PreservesFlinchReactionKind()
    {
        ReplicatedHitEvent hit = new(
            1,
            new SimHitKey(1, new SimActorId(2), 0, 0, new SimActorId(3)),
            actionId: 4,
            HitReactionKind.Flinch,
            hitXMm: 0,
            hitYMm: 0,
            hitZMm: 0,
            dirXMm: 1000,
            dirZMm: 0);
        ReplicatedHitEvent[] restored = ActReplicationEventCodec.Decode(
            ActReplicationEventCodec.Encode(new[] { hit }));
        Assert.That(restored[0].ReactionKind, Is.EqualTo(HitReactionKind.Flinch));
    }

    /// <summary>未知版本必须拒绝。</summary>
    [Test]
    public void Decode_UnsupportedVersion_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => ActReplicationEventCodec.Decode(new byte[] { 2, 0, 0, 0, 0 }));
    }
}
