using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 敌人行为树 EditMode 覆盖（含 BT-E1 Task/冷却/Writer）。
/// 放在 Editor 程序集：Enemy BT 位于 Assembly-CSharp，Domain.EditModeTests asmdef 无法引用。
/// </summary>
public sealed class EnemyBehaviorTreeTests
{
    [Test]
    public void MeleeTree_NoTarget_StopMove()
    {
        var bb = CreateBlackboard(hasTarget: false, distance: 0f, aggroed: false, cdReady: true);
        var runner = CreateRunner(EnemyBehaviorTreeDefFactory.CreateMeleeChaseAttack());

        BehaviorStatus status = runner.Tick(bb);

        Assert.That(status, Is.EqualTo(BehaviorStatus.Success));
        Assert.That(bb.MoveDesire, Is.EqualTo(Vector2.zero));
        Assert.That(bb.AttackPulse, Is.False);
    }

    [Test]
    public void MeleeTree_InAggroOutOfAttackRange_MovesForward()
    {
        var bb = CreateBlackboard(hasTarget: true, distance: 5f, aggroed: true, cdReady: true);
        bb.PlanarDirection = Vector3.forward;
        bb.PathDirection = Vector3.forward;
        var runner = CreateRunner(EnemyBehaviorTreeDefFactory.CreateMeleeChaseAttack());

        runner.Tick(bb);

        Assert.That(bb.AttackPulse, Is.False);
        Assert.That(bb.MoveDesire.y, Is.GreaterThan(0f));
    }

    [Test]
    public void MeleeTree_InAttackRange_Locomotion_CdReady_PulsesAttack()
    {
        var bb = CreateBlackboard(hasTarget: true, distance: 1.5f, aggroed: true, cdReady: true);
        bb.CharacterState = CharacterStateType.Locomotion;
        var runner = CreateRunner(EnemyBehaviorTreeDefFactory.CreateMeleeChaseAttack());

        runner.Tick(bb);

        Assert.That(bb.AttackPulse, Is.True);
        Assert.That(bb.MoveDesire, Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void MeleeTree_AttackConfirmPending_BlocksPulse()
    {
        var bb = CreateBlackboard(hasTarget: true, distance: 1.5f, aggroed: true, cdReady: true);
        bb.CharacterState = CharacterStateType.Locomotion;
        bb.AttackConfirmPending = true;
        var runner = CreateRunner(EnemyBehaviorTreeDefFactory.CreateMeleeChaseAttack());

        runner.Tick(bb);

        Assert.That(bb.AttackPulse, Is.False);
    }

    [Test]
    public void ChaseOnlyTree_NeverPulsesAttack()
    {
        var bb = CreateBlackboard(hasTarget: true, distance: 1.5f, aggroed: true, cdReady: true);
        bb.CharacterState = CharacterStateType.Locomotion;
        var runner = CreateRunner(EnemyBehaviorTreeDefFactory.CreateChaseOnly());

        runner.Tick(bb);

        Assert.That(bb.AttackPulse, Is.False);
        Assert.That(bb.MoveDesire.y, Is.GreaterThan(0f));
    }

    [Test]
    public void KiteTree_TooClose_BacksOff()
    {
        var bb = CreateBlackboard(hasTarget: true, distance: 2f, aggroed: true, cdReady: true);
        var runner = CreateRunner(EnemyBehaviorTreeDefFactory.CreateKite());

        runner.Tick(bb);

        Assert.That(bb.MoveDesire.y, Is.LessThan(0f));
        Assert.That(bb.FaceTargetRequested, Is.True);
    }

    [Test]
    public void KiteTree_TooFar_Chases()
    {
        var bb = CreateBlackboard(hasTarget: true, distance: 5f, aggroed: true, cdReady: true);
        bb.PlanarDirection = Vector3.forward;
        bb.PathDirection = Vector3.forward;
        var runner = CreateRunner(EnemyBehaviorTreeDefFactory.CreateKite());

        runner.Tick(bb);

        Assert.That(bb.MoveDesire.y, Is.GreaterThan(0f));
    }

    [Test]
    public void StrafeAction_WritesLateralMove()
    {
        var bb = CreateBlackboard(hasTarget: true, distance: 3f, aggroed: true, cdReady: true);
        var action = new StrafeAroundTargetAction(-1f);

        Assert.That(action.Tick(bb), Is.EqualTo(BehaviorStatus.Success));
        Assert.That(bb.MoveDesire.x, Is.LessThan(0f));
        Assert.That(bb.MoveDesire.y, Is.EqualTo(0f));
    }

    [Test]
    public void CooldownGate_BlocksThenAllowsAfterTickDown()
    {
        var bb = CreateBlackboard(hasTarget: true, distance: 3f, aggroed: true, cdReady: true);
        var gate = new CooldownGateNode(
            EnemyCooldownIds.Dodge,
            2,
            new PulseDodgeAction());

        Assert.That(gate.Tick(bb), Is.EqualTo(BehaviorStatus.Success));
        Assert.That(bb.DodgePulse, Is.True);
        bb.ResetFrameOutputs();
        Assert.That(gate.Tick(bb), Is.EqualTo(BehaviorStatus.Failure));

        bb.Cooldowns.TickDown();
        bb.Cooldowns.TickDown();
        Assert.That(gate.Tick(bb), Is.EqualTo(BehaviorStatus.Success));
    }

    [Test]
    public void AIInputWriter_PulseDodge_WritesButtonBit()
    {
        var writer = AIInputWriter.CreateForEditorTests(InputButton.Attack, InputButton.Dodge);
        writer.Enable();
        Assert.That(writer.PulseDodge(), Is.True);

        InputFrame frame = writer.BuildFrame(1, default);
        Assert.That(frame.WasPressed(InputButton.Dodge), Is.True);

        InputFrame release = writer.BuildFrame(2, default);
        Assert.That(release.WasReleased(InputButton.Dodge), Is.True);
    }

    [Test]
    public void AIInputWriter_PulseAttack_WritesButtonBit()
    {
        var writer = AIInputWriter.CreateForEditorTests(InputButton.Attack);
        writer.Enable();
        Assert.That(writer.PulseAttack(), Is.True);

        InputFrame frame = writer.BuildFrame(1, default);
        Assert.That(frame.WasPressed(InputButton.Attack), Is.True);
    }

    [Test]
    public void Asset_CreateRunner_EmptyRoot_Throws()
    {
        var asset = ScriptableObject.CreateInstance<EnemyBehaviorTreeAsset>();
        var profile = ScriptableObject.CreateInstance<EnemyBrainProfile>();
        var ctx = new EnemyBehaviorBuildContext(profile, new StraightPathQuery());

        Assert.Throws<InvalidOperationException>(() =>
            ((IEnemyBehaviorTreeAsset)asset).CreateRunner(in ctx));

        UnityEngine.Object.DestroyImmediate(asset);
        UnityEngine.Object.DestroyImmediate(profile);
    }

    [Test]
    public void CustomDef_MeleeFactory_PulsesAttack()
    {
        var bb = CreateBlackboard(hasTarget: true, distance: 1.5f, aggroed: true, cdReady: true);
        bb.CharacterState = CharacterStateType.Locomotion;
        var runner = CreateRunner(EnemyBehaviorTreeDefFactory.CreateMeleeChaseAttack());

        runner.Tick(bb);

        Assert.That(bb.AttackPulse, Is.True);
    }

    [Test]
    public void CustomDef_ChaseOnlyFactory_NeverPulses()
    {
        var bb = CreateBlackboard(hasTarget: true, distance: 1.5f, aggroed: true, cdReady: true);
        bb.CharacterState = CharacterStateType.Locomotion;
        var runner = CreateRunner(EnemyBehaviorTreeDefFactory.CreateChaseOnly());

        runner.Tick(bb);

        Assert.That(bb.AttackPulse, Is.False);
    }

    [Test]
    public void CustomDef_KiteFactory_BacksOffWhenClose()
    {
        var bb = CreateBlackboard(hasTarget: true, distance: 2f, aggroed: true, cdReady: true);
        var runner = CreateRunner(EnemyBehaviorTreeDefFactory.CreateKite());

        runner.Tick(bb);

        Assert.That(bb.MoveDesire.y, Is.LessThan(0f));
    }

    [Test]
    public void Asset_SerializedRoot_UsedByCreateRunner()
    {
        var asset = ScriptableObject.CreateInstance<EnemyBehaviorTreeAsset>();
        asset.SetCustomRootForEditor(EnemyBehaviorTreeDefFactory.CreateChaseOnly());
        var profile = ScriptableObject.CreateInstance<EnemyBrainProfile>();
        var ctx = new EnemyBehaviorBuildContext(profile, new StraightPathQuery());
        var bb = CreateBlackboard(hasTarget: true, distance: 1.5f, aggroed: true, cdReady: true);
        bb.CharacterState = CharacterStateType.Locomotion;

        IEnemyBehaviorRunner runner = asset.CreateRunner(in ctx);
        runner.Tick(bb);

        Assert.That(bb.AttackPulse, Is.False);

        UnityEngine.Object.DestroyImmediate(asset);
        UnityEngine.Object.DestroyImmediate(profile);
    }

    [Test]
    public void NamedNode_WritesDebugPath_WhenEnabled()
    {
        var bb = CreateBlackboard(hasTarget: true, distance: 5f, aggroed: true, cdReady: true);
        bb.DebugEnabled = true;
        bb.PlanarDirection = Vector3.forward;
        bb.PathDirection = Vector3.forward;
        var runner = CreateRunner(EnemyBehaviorTreeDefFactory.CreateMeleeChaseAttack());

        runner.Tick(bb);

        Assert.That(bb.DebugPath, Does.Contain("Chase"));
        Assert.That(bb.DebugPath, Does.Contain("MeleeRoot"));
    }

    [Test]
    public void Sequence_Reset_ClearsRunningChild()
    {
        var wait = new WaitFramesAction(3);
        var seq = new SequenceNode(wait, new StopMoveAction());
        var bb = CreateBlackboard(hasTarget: false, distance: 0f, aggroed: false, cdReady: true);

        Assert.That(seq.Tick(bb), Is.EqualTo(BehaviorStatus.Running));
        Assert.That(seq.Tick(bb), Is.EqualTo(BehaviorStatus.Running));
        seq.Reset();
        Assert.That(seq.Tick(bb), Is.EqualTo(BehaviorStatus.Running));
        Assert.That(seq.Tick(bb), Is.EqualTo(BehaviorStatus.Running));
        Assert.That(seq.Tick(bb), Is.EqualTo(BehaviorStatus.Success));
    }

    [Test]
    public void GraphMapper_FlattenRebuild_PreservesMeleePulse()
    {
        EnemyBehaviorNodeDef root = EnemyBehaviorTreeDefFactory.CreateMeleeChaseAttack();
        var flat = EnemyBehaviorTreeGraphMapper.Flatten(root);
        Assert.That(flat.Count, Is.GreaterThan(5));
        Assert.That(flat[0].parentGuid, Is.Null.Or.Empty);

        EnemyBehaviorNodeDef rebuilt = EnemyBehaviorTreeGraphMapper.Rebuild(flat);
        Assert.That(rebuilt, Is.Not.Null);

        var bb = CreateBlackboard(hasTarget: true, distance: 1.5f, aggroed: true, cdReady: true);
        bb.CharacterState = CharacterStateType.Locomotion;
        new NativeBehaviorTreeRunner(rebuilt.Build()).Tick(bb);
        Assert.That(bb.AttackPulse, Is.True);
    }

    [Test]
    public void GraphMapper_EnsureStableIds_AssignsGuidAndNodeName()
    {
        var leaf = new StopMoveActionDef();
        Assert.That(leaf.NodeGuid, Is.Null.Or.Empty);

        EnemyBehaviorTreeGraphMapper.EnsureStableIds(leaf);
        Assert.That(leaf.NodeGuid, Is.Not.Null.And.Not.Empty);
        Assert.That(leaf.NodeName, Is.EqualTo("StopMove"));
    }

    [Test]
    public void GraphMapper_SyncLayout_AddsMissingAndPrunesOrphans()
    {
        var root = EnemyBehaviorTreeDefFactory.CreateChaseOnly();
        var layout = new EnemyBehaviorGraphLayout();
        layout.SetNode("orphan-guid", Vector2.zero);

        EnemyBehaviorTreeGraphMapper.SyncLayout(layout, root);

        Assert.That(layout.TryGetNode("orphan-guid", out _), Is.False);
        Assert.That(layout.Nodes.Count, Is.GreaterThan(0));
        Assert.That(layout.TryGetNode(root.NodeGuid, out EnemyBehaviorGraphNodeLayout node), Is.True);
        Assert.That(node.position.y, Is.GreaterThanOrEqualTo(0f));
    }

    [Test]
    public void GraphMapper_TopDownLayout_RootAboveChildren()
    {
        EnemyBehaviorNodeDef root = EnemyBehaviorTreeDefFactory.CreateChaseOnly();
        var flat = EnemyBehaviorTreeGraphMapper.Flatten(root);
        Dictionary<string, Vector2> pos = EnemyBehaviorTreeGraphMapper.ComputeTopDownPositions(flat);

        Assert.That(pos.ContainsKey(root.NodeGuid), Is.True);
        float rootY = pos[root.NodeGuid].y;
        foreach (KeyValuePair<string, Vector2> pair in pos)
        {
            if (pair.Key == root.NodeGuid)
                continue;
            Assert.That(pair.Value.y, Is.GreaterThan(rootY), pair.Key);
        }
    }

    [Test]
    public void Validator_EmptyRoot_Fails()
    {
        var asset = ScriptableObject.CreateInstance<EnemyBehaviorTreeAsset>();

        EnemyBehaviorTreeValidationResult result = asset.ValidateAsset();
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors[0], Does.Contain("customRoot"));

        UnityEngine.Object.DestroyImmediate(asset);
    }

    [Test]
    public void Validator_DetectsReferenceCycle()
    {
        var seq = new SequenceNodeDef { NodeName = "Loop" };
        seq.children.Add(seq);

        EnemyBehaviorTreeValidationResult result = EnemyBehaviorTreeValidator.ValidateTree(seq);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors[0], Does.Contain("环"));
    }

    [Test]
    public void Validator_DetectsNullChild()
    {
        var inv = new InverterNodeDef { NodeName = "Inv", child = null };
        EnemyBehaviorTreeValidationResult result = EnemyBehaviorTreeValidator.ValidateTree(inv);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors[0], Does.Contain("child 为空"));
    }

    [Test]
    public void Catalog_Create_AllEntries_HaveGuidAndBuild()
    {
        for (int i = 0; i < EnemyBehaviorNodeCatalog.All.Count; i++)
        {
            EnemyBehaviorNodeCatalog.Entry entry = EnemyBehaviorNodeCatalog.All[i];
            EnemyBehaviorNodeDef def = EnemyBehaviorNodeCatalog.Create(entry.DefType);
            Assert.That(def, Is.Not.Null, entry.DisplayName);
            Assert.That(def.NodeGuid, Is.Not.Null.And.Not.Empty, entry.DisplayName);
            Assert.That(def.Build(), Is.Not.Null, entry.DisplayName);
        }
    }

    [Test]
    public void CustomDef_Build_AlwaysNamedForDebugPath()
    {
        var bb = CreateBlackboard(hasTarget: true, distance: 5f, aggroed: true, cdReady: true);
        bb.DebugEnabled = true;
        bb.PlanarDirection = Vector3.forward;
        bb.PathDirection = Vector3.forward;

        // 无 NodeName 的叶子也应出现短类型名
        var root = new SequenceNodeDef
        {
            children = new List<EnemyBehaviorNodeDef>
            {
                new HasTargetConditionDef(),
                new MoveTowardTargetActionDef(),
            },
        };

        new NativeBehaviorTreeRunner(root.Build()).Tick(bb);
        Assert.That(bb.DebugPath, Does.Contain("HasTarget"));
        Assert.That(bb.DebugPath, Does.Contain("MoveTowardTarget"));
    }

    /// <summary>由 Def 工厂构造测试 Runner（非资产默认 Kind）。</summary>
    static NativeBehaviorTreeRunner CreateRunner(EnemyBehaviorNodeDef root) =>
        new NativeBehaviorTreeRunner(root.Build());

    /// <summary>构造带 Profile 与冷却表的测试黑板。</summary>
    static EnemyBlackboard CreateBlackboard(bool hasTarget, float distance, bool aggroed, bool cdReady)
    {
        var profile = ScriptableObject.CreateInstance<EnemyBrainProfile>();
        var bb = new EnemyBlackboard
        {
            Profile = profile,
            PathQuery = new StraightPathQuery(),
            HasTarget = hasTarget,
            PlanarDistance = distance,
            PlanarDirection = Vector3.forward,
            PathDirection = Vector3.forward,
            CharacterState = CharacterStateType.Locomotion,
            IsAggroed = aggroed,
        };
        if (!cdReady)
            bb.Cooldowns.Set(EnemyCooldownIds.BasicAttack, 10);
        bb.ResetFrameOutputs();
        return bb;
    }
}
