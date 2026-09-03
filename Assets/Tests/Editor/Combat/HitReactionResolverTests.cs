using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>P-HR1/P-HR3：受击档位纯函数、抗打断默认值与 Phase 加成。</summary>
public sealed class HitReactionResolverTests
{
    readonly CharacterReactionResolver _resolver = new CharacterReactionResolver(new CharacterReactionSet());

    /// <summary>等级不够时击飞期望降为 Flinch。</summary>
    [Test]
    public void InterruptBelowResist_DesiredLaunch_ReturnsFlinch()
    {
        HitReactionCommand command = _resolver.Resolve(
            HitReactionResolveQuery.CombatHit(
                interruptLevel: 1,
                desiredReaction: HitReactionKind.Launch,
                baseInterruptResist: 3));

        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.Flinch));
        Assert.That(command.InterruptsAction, Is.False);
        Assert.That(command.FlinchKey, Is.EqualTo(AnimationKey.HitShake));
        Assert.That(command.StunAction, Is.Null);
    }

    /// <summary>等级够但仍期望 Flinch 时不进 Hit。</summary>
    [Test]
    public void LevelAtLeastResist_DesiredFlinch_ReturnsFlinch()
    {
        HitReactionCommand command = _resolver.Resolve(
            HitReactionResolveQuery.CombatHit(
                interruptLevel: 3,
                desiredReaction: HitReactionKind.Flinch,
                baseInterruptResist: 1));

        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.Flinch));
        Assert.That(command.InterruptsAction, Is.False);
    }

    /// <summary>SuperArmor 窗非致命最多 Flinch。</summary>
    [Test]
    public void SuperArmor_NonDeath_CapsAtFlinch()
    {
        HitReactionCommand command = _resolver.Resolve(
            HitReactionResolveQuery.CombatHit(
                interruptLevel: 99,
                desiredReaction: HitReactionKind.Launch,
                superArmor: true));

        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.Flinch));
        Assert.That(command.InterruptsAction, Is.False);
    }

    /// <summary>致命伤不受 SuperArmor 限制。</summary>
    [Test]
    public void SuperArmor_Fatal_StillDeath()
    {
        var query = new HitReactionResolveQuery(
            isDead: false,
            isFatal: true,
            isInvincible: false,
            absorbedByPerfectDodge: false,
            isDot: false,
            hasHitPayload: true,
            interruptLevel: 1,
            desiredReaction: HitReactionKind.Flinch,
            baseInterruptResist: 1,
            phaseInterruptResistBonus: 0,
            superArmor: true,
            hitReactionId: string.Empty);

        HitReactionCommand command = _resolver.Resolve(in query);
        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.Death));
        Assert.That(command.InterruptsAction, Is.True);
    }

    /// <summary>同帧 Flinch 与 LightStun 合并为更高档。</summary>
    [Test]
    public void Merge_FlinchAndLightStun_ReturnsLightStun()
    {
        HitReactionCommand flinch = _resolver.Resolve(
            HitReactionResolveQuery.CombatHit(desiredReaction: HitReactionKind.Flinch));
        HitReactionCommand stun = _resolver.Resolve(
            HitReactionResolveQuery.CombatHit(desiredReaction: HitReactionKind.LightStun));

        HitReactionCommand merged = HitReactionCommand.Merge(flinch, stun);
        Assert.That(merged.Kind, Is.EqualTo(HitReactionKind.LightStun));
        Assert.That(merged.InterruptsAction, Is.True);
    }

    /// <summary>DOT 无打击语义。</summary>
    [Test]
    public void Dot_ReturnsNone()
    {
        var query = new HitReactionResolveQuery(
            isDead: false,
            isFatal: false,
            isInvincible: false,
            absorbedByPerfectDodge: false,
            isDot: true,
            hasHitPayload: true,
            interruptLevel: 1,
            desiredReaction: HitReactionKind.LightStun,
            baseInterruptResist: 1,
            phaseInterruptResistBonus: 0,
            superArmor: false,
            hitReactionId: string.Empty);

        HitReactionCommand command = _resolver.Resolve(in query);
        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.None));
        Assert.That(command.InterruptsAction, Is.False);
    }

    /// <summary>旧盒子默认 level 1 + LightStun，杂兵仍断招。</summary>
    [Test]
    public void EmptyPayloadDefaults_LightStunAndLevel1()
    {
        // 旧盒子：有 Payload、未填打断字段 → level=1、desired=LightStun，杂兵 resist=1 仍断招。
        HitReactionCommand command = _resolver.Resolve(HitReactionResolveQuery.CombatHit());

        Assert.That(HitReactionResolveQuery.DefaultInterruptLevel, Is.EqualTo(1));
        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.LightStun));
        Assert.That(command.InterruptsAction, Is.True);
        Assert.That(command.StunFrames, Is.EqualTo(new CharacterReactionSet().DefaultHitStunFrames));
    }

    /// <summary>无 HitPayload 的数值伤不出档。</summary>
    [Test]
    public void MissingHitPayload_ReturnsNone()
    {
        var query = new HitReactionResolveQuery(
            isDead: false,
            isFatal: false,
            isInvincible: false,
            absorbedByPerfectDodge: false,
            isDot: false,
            hasHitPayload: false,
            interruptLevel: 1,
            desiredReaction: HitReactionKind.LightStun,
            baseInterruptResist: 1,
            phaseInterruptResistBonus: 0,
            superArmor: false,
            hitReactionId: string.Empty);

        Assert.That(_resolver.Resolve(in query).Kind, Is.EqualTo(HitReactionKind.None));
    }

    /// <summary>无敌与完美吞伤即使叫到 Resolver 也是 None。</summary>
    [Test]
    public void InvincibleOrPerfectDodge_ReturnsNone()
    {
        var invuln = new HitReactionResolveQuery(
            isDead: false,
            isFatal: false,
            isInvincible: true,
            absorbedByPerfectDodge: false,
            isDot: false,
            hasHitPayload: true,
            interruptLevel: 3,
            desiredReaction: HitReactionKind.Launch,
            baseInterruptResist: 1,
            phaseInterruptResistBonus: 0,
            superArmor: false,
            hitReactionId: string.Empty);
        var dodge = new HitReactionResolveQuery(
            isDead: false,
            isFatal: false,
            isInvincible: false,
            absorbedByPerfectDodge: true,
            isDot: false,
            hasHitPayload: true,
            interruptLevel: 3,
            desiredReaction: HitReactionKind.Launch,
            baseInterruptResist: 1,
            phaseInterruptResistBonus: 0,
            superArmor: false,
            hitReactionId: string.Empty);

        Assert.That(_resolver.Resolve(in invuln).Kind, Is.EqualTo(HitReactionKind.None));
        Assert.That(_resolver.Resolve(in dodge).Kind, Is.EqualTo(HitReactionKind.None));
    }

    /// <summary>Phase 抗打断加成抬高 resist 后，普攻打不进 Stun。</summary>
    [Test]
    public void PhaseResistBonus_BlocksLaunch()
    {
        HitReactionCommand command = _resolver.Resolve(
            HitReactionResolveQuery.CombatHit(
                interruptLevel: 1,
                desiredReaction: HitReactionKind.Launch,
                baseInterruptResist: 1,
                phaseInterruptResistBonus: 2));

        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.Flinch));
    }

    /// <summary>Payload 标明 Flinch 时即使等级够也不进 Hit。</summary>
    [Test]
    public void PayloadDesiredFlinch_ReturnsFlinch()
    {
        var payload = new HitPayload(10f, interruptLevel: 1, HitReactionKind.Flinch);
        HitReactionCommand command = _resolver.Resolve(
            HitReactionResolveQuery.CombatHit(
                payload.InterruptLevel,
                payload.DesiredReaction));

        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.Flinch));
        Assert.That(command.InterruptsAction, Is.False);
    }

    /// <summary>Flinch 裁定后复制边沿不得停在 Hit，避免幽灵重播走跑/出招。</summary>
    [Test]
    public void ConfirmHitReaction_Flinch_ClearsHitEdge()
    {
        var vitality = new CharacterVitality(new NumericSystem(CharacterNumericConfig.Default));
        vitality.ApplyDamage(1f, default);
        Assert.That(vitality.ReplicationEdge, Is.EqualTo(VitalityReplicationEdge.Hit));

        vitality.ConfirmHitReaction(HitReactionKind.Flinch);
        Assert.That(vitality.ReplicationEdge, Is.EqualTo(VitalityReplicationEdge.None));
    }

    /// <summary>Stun+ 仍标 Hit 边沿，供硬直硬吸。</summary>
    [Test]
    public void ConfirmHitReaction_LightStun_KeepsHitEdge()
    {
        var vitality = new CharacterVitality(new NumericSystem(CharacterNumericConfig.Default));
        vitality.ApplyDamage(1f, default);
        vitality.ConfirmHitReaction(HitReactionKind.LightStun);
        Assert.That(vitality.ReplicationEdge, Is.EqualTo(VitalityReplicationEdge.Hit));
    }

    /// <summary>无 Hitbox 的上下文按数值伤处理。</summary>
    [Test]
    public void FromHitContext_NoHitbox_IsNumericNone()
    {
        var context = new ActionHitContext(
            action: null,
            hitbox: null,
            attacker: null,
            actionInstanceId: 0,
            attackerId: default);
        HitReactionCommand command = _resolver.Resolve(HitReactionResolveQuery.FromHitContext(in context));
        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.None));
    }

    /// <summary>P-HR3：精英 resist=3 吃普攻 level1，即使期望 LightStun 也只 Flinch。</summary>
    [Test]
    public void EliteResist3_NormalAttackLevel1_DesiredLightStun_Flinch()
    {
        HitReactionCommand command = _resolver.Resolve(
            HitReactionResolveQuery.CombatHit(
                interruptLevel: 1,
                desiredReaction: HitReactionKind.LightStun,
                baseInterruptResist: 3));

        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.Flinch));
        Assert.That(command.InterruptsAction, Is.False);
    }

    /// <summary>P-HR3：同精英被技能 level≥3 打断，按期望进 LightStun。</summary>
    [Test]
    public void EliteResist3_SkillLevel3_LightStun()
    {
        HitReactionCommand command = _resolver.Resolve(
            HitReactionResolveQuery.CombatHit(
                interruptLevel: 3,
                desiredReaction: HitReactionKind.LightStun,
                baseInterruptResist: 3));

        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.LightStun));
        Assert.That(command.InterruptsAction, Is.True);
    }

    /// <summary>P-HR3：杂兵 resist=1 + 未改旧盒子仍断招。</summary>
    [Test]
    public void GruntResist1_LegacyBox_LightStun()
    {
        HitReactionCommand command = _resolver.Resolve(
            HitReactionResolveQuery.CombatHit(
                interruptLevel: HitReactionResolveQuery.DefaultInterruptLevel,
                desiredReaction: HitReactionKind.LightStun,
                baseInterruptResist: HitReactionResolveQuery.DefaultBaseInterruptResist));

        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.LightStun));
        Assert.That(command.InterruptsAction, Is.True);
    }

    /// <summary>未填 interruptLevel 写成 1，已设的 Flinch 不被覆盖。</summary>
    [Test]
    public void HitPayload_EnsureInterruptLevel_PreservesDesiredFlinch()
    {
        var payload = new HitPayload(10f, interruptLevel: 0, HitReactionKind.Flinch);
        Assert.That(payload.EnsureInterruptLevelDefault(), Is.True);
        Assert.That(payload.InterruptLevel, Is.EqualTo(HitReactionResolveQuery.DefaultInterruptLevel));
        Assert.That(payload.DesiredReaction, Is.EqualTo(HitReactionKind.Flinch));
        Assert.That(payload.EnsureInterruptLevelDefault(), Is.False);
    }

    /// <summary>未填站立抗打断按杂兵 1 读，Ensure 只补写空字段。</summary>
    [Test]
    public void CharacterCombatConfig_UnfilledResist_ReadsAsGruntDefault()
    {
        var combat = default(CharacterCombatConfig);
        Assert.That(combat.BaseInterruptResist, Is.EqualTo(HitReactionResolveQuery.DefaultBaseInterruptResist));

        combat.EnsureInterruptResistDefault();
        Assert.That(combat.BaseInterruptResist, Is.EqualTo(HitReactionResolveQuery.DefaultBaseInterruptResist));
        Assert.That(CharacterCombatConfig.Default.BaseInterruptResist,
            Is.EqualTo(HitReactionResolveQuery.DefaultBaseInterruptResist));
    }

    /// <summary>当前帧生效 Phase 的抗打断加成求和；窗外为 0。</summary>
    [Test]
    public void ActionDefinition_SumsActivePhaseInterruptResistBonus()
    {
        ActionDefinition action = ScriptableObject.CreateInstance<ActionDefinition>();
        var so = new SerializedObject(action);
        SerializedProperty phases = so.FindProperty("timeline").FindPropertyRelative("phaseStates");
        phases.arraySize = 2;

        SerializedProperty first = phases.GetArrayElementAtIndex(0);
        first.FindPropertyRelative("startFrame").intValue = 0;
        first.FindPropertyRelative("endFrame").intValue = 10;
        first.FindPropertyRelative("interruptResistBonus").intValue = 2;

        SerializedProperty second = phases.GetArrayElementAtIndex(1);
        second.FindPropertyRelative("startFrame").intValue = 5;
        second.FindPropertyRelative("endFrame").intValue = 10;
        second.FindPropertyRelative("interruptResistBonus").intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(action.GetInterruptResistBonusAtFrame(0), Is.EqualTo(2));
        Assert.That(action.GetInterruptResistBonusAtFrame(5), Is.EqualTo(3));
        Assert.That(action.GetInterruptResistBonusAtFrame(11), Is.EqualTo(0));

        Object.DestroyImmediate(action);
    }
}
