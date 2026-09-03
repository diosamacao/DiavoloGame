using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>冲击力对韧性裁定档位、默认值与 Phase 加成。</summary>
public sealed class HitReactionResolverTests
{
    readonly CharacterReactionResolver _resolver = new CharacterReactionResolver(new CharacterReactionSet());

    /// <summary>冲击力低于韧性只 Shake，不进 Hit。</summary>
    [Test]
    public void ImpactBelowToughness_ReturnsFlinch()
    {
        HitReactionCommand command = _resolver.Resolve(
            HitReactionResolveQuery.CombatHit(
                interruptLevel: 1,
                baseInterruptResist: 3));

        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.Flinch));
        Assert.That(command.InterruptsAction, Is.False);
        Assert.That(command.FlinchKey, Is.EqualTo(AnimationKey.HitShake));
        Assert.That(command.StunAction, Is.Null);
    }

    /// <summary>冲击力达到韧性进 LightStun（HardHit）。</summary>
    [Test]
    public void ImpactAtLeastToughness_ReturnsLightStun()
    {
        HitReactionCommand command = _resolver.Resolve(
            HitReactionResolveQuery.CombatHit(
                interruptLevel: 3,
                baseInterruptResist: 1));

        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.LightStun));
        Assert.That(command.InterruptsAction, Is.True);
    }

    /// <summary>超出韧性 2 档升 HeavyStun。</summary>
    [Test]
    public void ExcessAtLeast2_ReturnsHeavyStun()
    {
        HitReactionCommand command = _resolver.Resolve(
            HitReactionResolveQuery.CombatHit(
                interruptLevel: 5,
                baseInterruptResist: 3));

        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.HeavyStun));
        Assert.That(CharacterReactionResolver.ResolveKind(5, 3, superArmor: false),
            Is.EqualTo(HitReactionKind.HeavyStun));
    }

    /// <summary>超出韧性 4 档升 Launch。</summary>
    [Test]
    public void ExcessAtLeast4_ReturnsLaunch()
    {
        Assert.That(CharacterReactionResolver.ResolveKind(7, 3, superArmor: false),
            Is.EqualTo(HitReactionKind.Launch));
    }

    /// <summary>SuperArmor 窗非致命最多 Flinch。</summary>
    [Test]
    public void SuperArmor_NonDeath_CapsAtFlinch()
    {
        HitReactionCommand command = _resolver.Resolve(
            HitReactionResolveQuery.CombatHit(
                interruptLevel: 99,
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
            HitReactionResolveQuery.CombatHit(interruptLevel: 1, baseInterruptResist: 3));
        HitReactionCommand stun = _resolver.Resolve(
            HitReactionResolveQuery.CombatHit(interruptLevel: 1, baseInterruptResist: 1));

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
            baseInterruptResist: 1,
            phaseInterruptResistBonus: 0,
            superArmor: false,
            hitReactionId: string.Empty);

        HitReactionCommand command = _resolver.Resolve(in query);
        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.None));
        Assert.That(command.InterruptsAction, Is.False);
    }

    /// <summary>旧盒子默认冲击 1 打杂兵韧性 1，进 LightStun。</summary>
    [Test]
    public void EmptyPayloadDefaults_LightStunAndLevel1()
    {
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
            baseInterruptResist: 1,
            phaseInterruptResistBonus: 0,
            superArmor: false,
            hitReactionId: string.Empty);

        Assert.That(_resolver.Resolve(in invuln).Kind, Is.EqualTo(HitReactionKind.None));
        Assert.That(_resolver.Resolve(in dodge).Kind, Is.EqualTo(HitReactionKind.None));
    }

    /// <summary>Phase 韧性加成抬高后，普攻打不进 Stun。</summary>
    [Test]
    public void PhaseResistBonus_BlocksLightStun()
    {
        HitReactionResolveQuery query = HitReactionResolveQuery.CombatHit(
            interruptLevel: 1,
            baseInterruptResist: 1,
            phaseInterruptResistBonus: 2);
        HitReactionCommand command = _resolver.Resolve(query);

        Assert.That(query.Toughness, Is.EqualTo(3));
        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.Flinch));
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

    /// <summary>Attack01 冲击 2 打杂兵韧性 1：HardHit。</summary>
    [Test]
    public void Attack01Impact2_GruntToughness1_LightStun()
    {
        HitReactionCommand command = _resolver.Resolve(
            HitReactionResolveQuery.CombatHit(
                interruptLevel: 2,
                baseInterruptResist: 1));

        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.LightStun));
        Assert.That(command.InterruptsAction, Is.True);
    }

    /// <summary>Attack01 冲击 2 打精英韧性 3：只 Shake。</summary>
    [Test]
    public void Attack01Impact2_EliteToughness3_Flinch()
    {
        HitReactionCommand command = _resolver.Resolve(
            HitReactionResolveQuery.CombatHit(
                interruptLevel: 2,
                baseInterruptResist: 3));

        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.Flinch));
        Assert.That(command.InterruptsAction, Is.False);
    }

    /// <summary>技能冲击 3 打精英韧性 3：HardHit。</summary>
    [Test]
    public void SkillImpact3_EliteToughness3_LightStun()
    {
        HitReactionCommand command = _resolver.Resolve(
            HitReactionResolveQuery.CombatHit(
                interruptLevel: 3,
                baseInterruptResist: 3));

        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.LightStun));
        Assert.That(command.InterruptsAction, Is.True);
    }

    /// <summary>未填冲击力写成 1。</summary>
    [Test]
    public void HitPayload_EnsureInterruptLevel_WritesDefault()
    {
        var payload = new HitPayload(10f, interruptLevel: 0);
        Assert.That(payload.EnsureInterruptLevelDefault(), Is.True);
        Assert.That(payload.InterruptLevel, Is.EqualTo(HitReactionResolveQuery.DefaultInterruptLevel));
        Assert.That(payload.EnsureInterruptLevelDefault(), Is.False);
    }

    /// <summary>未填站立韧性按杂兵 1 读，Ensure 只补写空字段。</summary>
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

    /// <summary>当前帧生效 Phase 的韧性加成求和；窗外为 0。</summary>
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
