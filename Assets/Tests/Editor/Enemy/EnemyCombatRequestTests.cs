using NUnit.Framework;
using UnityEngine;

/// <summary>E-REQ1：CombatRequest 黑板 / 缓冲 / Wait 闩 EditMode 覆盖。</summary>
public sealed class EnemyCombatRequestTests
{
    [Test]
    public void RequestCombatAction_WritesDistinctEntryIds()
    {
        var bb = new EnemyBlackboard();
        bb.ResetFrameOutputs();

        Assert.That(new RequestCombatAction("Entry_A").Tick(bb), Is.EqualTo(BehaviorStatus.Success));
        Assert.That(bb.HasCombatRequest, Is.True);
        Assert.That(bb.CombatRequestEntryId, Is.EqualTo("Entry_A"));

        bb.ResetFrameOutputs();
        Assert.That(new RequestCombatAction("Entry_B").Tick(bb), Is.EqualTo(BehaviorStatus.Success));
        Assert.That(bb.CombatRequestEntryId, Is.EqualTo("Entry_B"));
        Assert.That(bb.CombatRequestEntryId, Is.Not.EqualTo("Entry_A"));
    }

    [Test]
    public void RequestCombatAction_EmptyEntry_Fails()
    {
        var bb = new EnemyBlackboard();
        Assert.That(new RequestCombatAction("").Tick(bb), Is.EqualTo(BehaviorStatus.Failure));
        Assert.That(bb.HasCombatRequest, Is.False);
    }

    [Test]
    public void CombatRequestBuffer_SetPeekConsume()
    {
        var buffer = new EnemyCombatRequestBuffer();
        buffer.Set(new EnemyCombatRequest("Entry_Swipe"));

        Assert.That(buffer.HasPending, Is.True);
        Assert.That(buffer.TryPeek(out EnemyCombatRequest peek), Is.True);
        Assert.That(peek.EntryNodeId, Is.EqualTo("Entry_Swipe"));
        Assert.That(buffer.HasPending, Is.True);

        Assert.That(buffer.TryConsume(out EnemyCombatRequest consumed), Is.True);
        Assert.That(consumed.EntryNodeId, Is.EqualTo("Entry_Swipe"));
        Assert.That(buffer.HasPending, Is.False);
        Assert.That(buffer.TryConsume(out _), Is.False);
    }

    [Test]
    public void WaitWhileInAction_LatchesOnCombatRequest()
    {
        var wait = new WaitWhileInActionAction();
        var bb = new EnemyBlackboard
        {
            HasCombatRequest = true,
            CombatRequestEntryId = "Entry_A",
            CharacterState = CharacterStateType.Locomotion,
        };

        Assert.That(wait.Tick(bb), Is.EqualTo(BehaviorStatus.Running));
        Assert.That(bb.MoveDesire, Is.EqualTo(Vector2.zero));

        bb.HasCombatRequest = false;
        bb.AttackConfirmPending = true;
        Assert.That(wait.Tick(bb), Is.EqualTo(BehaviorStatus.Running));

        bb.AttackConfirmPending = false;
        bb.CharacterState = CharacterStateType.Action;
        Assert.That(wait.Tick(bb), Is.EqualTo(BehaviorStatus.Running));

        bb.CharacterState = CharacterStateType.Locomotion;
        Assert.That(wait.Tick(bb), Is.EqualTo(BehaviorStatus.Success));
    }

    [Test]
    public void RequestCombatActionDef_Build_UsesEntryId()
    {
        var def = new RequestCombatActionDef { EntryNodeId = "Entry_Leap" };
        var bb = new EnemyBlackboard();
        Assert.That(def.Build().Tick(bb), Is.EqualTo(BehaviorStatus.Success));
        Assert.That(bb.CombatRequestEntryId, Is.EqualTo("Entry_Leap"));
    }
}
