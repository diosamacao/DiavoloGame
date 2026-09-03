using NUnit.Framework;

/// <summary>P-HR1：受击档位纯函数。不碰 Service / EnterHit。</summary>
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
}
