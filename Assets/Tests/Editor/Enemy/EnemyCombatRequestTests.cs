using NUnit.Framework;
using UnityEngine;

/// <summary>Entry Request 黑板 / 通用缓冲 / Wait 闩 EditMode 覆盖。</summary>
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
        var buffer = new ActionEntryRequestBuffer();
        buffer.Set(new ActionEntryRequest("Entry_Swipe"));

        Assert.That(buffer.HasPending, Is.True);
        Assert.That(buffer.TryPeek(out ActionEntryRequest peek), Is.True);
        Assert.That(peek.EntryNodeId, Is.EqualTo("Entry_Swipe"));
        Assert.That(buffer.HasPending, Is.True);

        Assert.That(buffer.TryConsume(out ActionEntryRequest consumed), Is.True);
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

    /// <summary>真实进入 Action 后确认节点暂存的完整攻击冷却。</summary>
    [Test]
    public void EnemyBrain_RequestConfirmed_CommitsGateCooldown()
    {
        CharacterStateType state = CharacterStateType.Locomotion;
        var self = new GameObject("BrainTestSelf");
        var target = new GameObject("BrainTestTarget");
        var profile = ScriptableObject.CreateInstance<EnemyBrainProfile>();
        var requests = new ActionEntryRequestBuffer();
        var perception = new EnemyPerception(
            self.transform,
            () => target.transform,
            () => state,
            () => false);
        var brain = new EnemyBrain(
            profile,
            perception,
            null,
            new ConfirmableAttackRunner(cooldownFrames: 72),
            actionEntryRequests: requests);

        brain.Step();
        Assert.That(requests.HasPending, Is.True);
        Assert.That(brain.DebugBasicAttackCooldownFrames, Is.EqualTo(0));

        state = CharacterStateType.Action;
        brain.Step();
        Assert.That(brain.DebugBasicAttackCooldownFrames, Is.EqualTo(72));
        Assert.That(brain.DebugActionEntryRetryFrames, Is.EqualTo(0));

        UnityEngine.Object.DestroyImmediate(profile);
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(self);
    }

    /// <summary>请求未进入 Action 时丢弃完整 CD，只启用独立短重试槽。</summary>
    [Test]
    public void EnemyBrain_RequestFailed_UsesRetryCooldownOnly()
    {
        CharacterStateType state = CharacterStateType.Locomotion;
        var self = new GameObject("BrainTestSelf");
        var target = new GameObject("BrainTestTarget");
        var profile = ScriptableObject.CreateInstance<EnemyBrainProfile>();
        var requests = new ActionEntryRequestBuffer();
        var perception = new EnemyPerception(
            self.transform,
            () => target.transform,
            () => state,
            () => false);
        var brain = new EnemyBrain(
            profile,
            perception,
            null,
            new ConfirmableAttackRunner(cooldownFrames: 72),
            actionEntryRequests: requests);

        brain.Step();
        Assert.That(requests.HasPending, Is.True);

        // Driver 失败时角色仍为 Locomotion；下一 Brain 帧必须走短重试而非完整攻击 CD。
        brain.Step();
        Assert.That(brain.DebugBasicAttackCooldownFrames, Is.EqualTo(0));
        Assert.That(brain.DebugActionEntryRetryFrames, Is.EqualTo(12));
        Assert.That(requests.HasPending, Is.False);

        UnityEngine.Object.DestroyImmediate(profile);
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(self);
    }

    /// <summary>用真实 CooldownGate + Request 复现 Brain 起手确认协议。</summary>
    sealed class ConfirmableAttackRunner : IEnemyBehaviorRunner
    {
        readonly CooldownGateNode _gate;

        public ConfirmableAttackRunner(int cooldownFrames)
        {
            _gate = new CooldownGateNode(
                EnemyCooldownIds.BasicAttack,
                cooldownFrames,
                new RequestCombatAction("Entry_Test"));
        }

        /// <inheritdoc />
        public BehaviorStatus Tick(EnemyBlackboard blackboard) => _gate.Tick(blackboard);

        /// <inheritdoc />
        public void Reset() => _gate.Reset();
    }
}
