using System;
using System.Collections.Generic;
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
            0,
            absorbedByAssistParry: true,
            hitStopFrames: 6);
        ReplicatedHitEvent[] restored = ActReplicationEventCodec.Decode(
            ActReplicationEventCodec.Encode(new[] { hit }));
        Assert.That(restored, Has.Length.EqualTo(1));
        Assert.That(restored[0].Equals(hit), Is.True);
        Assert.That(restored[0].AbsorbedByAssistParry, Is.True);
        Assert.That(restored[0].HitStopFrames, Is.EqualTo(6));
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

    /// <summary>事件早于 Meta 时 pending；绑定后按稳定槽 Id 应用且重复包不重复起手。</summary>
    [Test]
    public void OwnerAssistParry_EventBeforeMeta_RetriesOnce()
    {
        var queue = new ActReplicationEventCodec.OwnerAssistParryEventQueue();
        ReplicatedHitEvent hit = AssistParryHit(targetId: 22);
        int applied = 0;
        var boundActors = new HashSet<int>();
        queue.ApplyOrPend(in hit, new[] { new SimActorId(11) }, false, value =>
        {
            if (!boundActors.Contains(value.Key.TargetId.Value))
                return false;
            applied++;
            return true;
        });

        boundActors.Add(22);
        SimActorId[] party = { new(11), new(22) };
        queue.Retry(party, value =>
        {
            if (!boundActors.Contains(value.Key.TargetId.Value))
                return false;
            applied++;
            return true;
        });
        queue.ApplyOrPend(in hit, party, true, _ =>
        {
            applied++;
            return true;
        });

        Assert.That(applied, Is.EqualTo(1));
        Assert.That(queue.PendingCount, Is.Zero);
    }

    /// <summary>Meta 已确认目标不属本阵容时完成去重，不重复调用 Owner 应用。</summary>
    [Test]
    public void OwnerAssistParry_NonPartyTarget_CompletesWithoutApplying()
    {
        var queue = new ActReplicationEventCodec.OwnerAssistParryEventQueue();
        ReplicatedHitEvent hit = AssistParryHit(targetId: 99);
        int attempts = 0;
        SimActorId[] party = { new(11), new(22) };
        queue.ApplyOrPend(in hit, party, true, _ =>
        {
            attempts++;
            return false;
        });
        queue.ApplyOrPend(in hit, party, true, _ =>
        {
            attempts++;
            return false;
        });

        Assert.That(attempts, Is.EqualTo(1));
        Assert.That(queue.PendingCount, Is.Zero);
    }

    /// <summary>阵容尚未同步时 pending 保持硬上界，淘汰项不会误标 completed。</summary>
    [Test]
    public void OwnerAssistParry_PendingQueue_IsBounded()
    {
        var queue = new ActReplicationEventCodec.OwnerAssistParryEventQueue();
        for (int i = 0; i <= ActReplicationEventCodec.OwnerAssistParryEventQueue.MaxPending; i++)
        {
            ReplicatedHitEvent hit = AssistParryHit(100 + i);
            queue.ApplyOrPend(in hit, null, false, _ => false);
        }

        Assert.That(
            queue.PendingCount,
            Is.EqualTo(ActReplicationEventCodec.OwnerAssistParryEventQueue.MaxPending));
    }

    static ReplicatedHitEvent AssistParryHit(int targetId) =>
        new(
            10,
            new SimHitKey(10, new SimActorId(7), 3, 1, new SimActorId(targetId)),
            actionId: 0,
            reactionKind: HitReactionKind.None,
            hitXMm: 0,
            hitYMm: 0,
            hitZMm: 0,
            dirXMm: 0,
            dirZMm: 0,
            absorbedByAssistParry: true,
            hitStopFrames: 6);
}
