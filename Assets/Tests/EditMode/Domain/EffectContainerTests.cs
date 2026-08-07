using NUnit.Framework;

/// <summary>GAS G2：Duration/Periodic/叠层与 Debug Snapshot。</summary>
public sealed class EffectContainerTests
{
    [Test]
    public void Duration_AttackUp_ExpiresAndRestoresCurrent()
    {
        var numeric = new NumericSystem(CharacterNumericConfig.Default);
        int baseAttack = numeric.Attributes.GetCurrent(AttributeId.Attack);

        EffectDefinition buff = EffectDefinition.CreateDuration(
            "AttackUp",
            durationFrames: 3,
            EffectStackPolicy.Replace,
            maxStacks: 1,
            new EffectModifierSpec(AttributeId.Attack, ModifierOp.Flat, 5000));

        numeric.ApplyEffect(buff);
        Assert.That(numeric.Attributes.GetCurrent(AttributeId.Attack), Is.EqualTo(baseAttack + 5000));
        Assert.That(numeric.Effects.ActiveCount, Is.EqualTo(1));

        numeric.Step();
        numeric.Step();
        Assert.That(numeric.Effects.ActiveCount, Is.EqualTo(1));
        numeric.Step(); // 第 3 帧到期移除

        Assert.That(numeric.Effects.ActiveCount, Is.EqualTo(0));
        Assert.That(numeric.Attributes.GetCurrent(AttributeId.Attack), Is.EqualTo(baseAttack));
    }

    [Test]
    public void StackCount_StopsAtMaxStacks_ButRefreshesDuration()
    {
        var numeric = new NumericSystem(CharacterNumericConfig.Default);
        int baseAttack = numeric.Attributes.GetCurrent(AttributeId.Attack);

        EffectDefinition buff = EffectDefinition.CreateDuration(
            "AttackUp",
            durationFrames: 5,
            EffectStackPolicy.StackCount,
            maxStacks: 2,
            new EffectModifierSpec(AttributeId.Attack, ModifierOp.Flat, 1000));

        numeric.ApplyEffect(buff);
        numeric.ApplyEffect(buff);
        Assert.That(numeric.Effects.FindById("AttackUp").StackCount, Is.EqualTo(2));
        Assert.That(numeric.Attributes.GetCurrent(AttributeId.Attack), Is.EqualTo(baseAttack + 2000));

        numeric.ApplyEffect(buff); // 达上限：不加层，只刷新
        Assert.That(numeric.Effects.FindById("AttackUp").StackCount, Is.EqualTo(2));
        Assert.That(numeric.Attributes.GetCurrent(AttributeId.Attack), Is.EqualTo(baseAttack + 2000));
        Assert.That(numeric.Effects.FindById("AttackUp").RemainingFrames, Is.EqualTo(5));
    }

    [Test]
    public void Periodic_Dot_TicksPredictableTotalDamage()
    {
        var numeric = new NumericSystem(CharacterNumericConfig.Default);
        int startHealth = numeric.Attributes.GetCurrent(AttributeId.Health);

        // duration=30, interval=10 → 第 10/20/30 帧各跳 1 次，共 3 跳
        EffectDefinition poison = EffectDefinition.CreatePeriodic(
            "Poison",
            durationFrames: 30,
            intervalFrames: 10,
            EffectStackPolicy.Replace,
            maxStacks: 1,
            new EffectAttributeDelta(AttributeId.Health, -1000));

        numeric.ApplyEffect(poison);
        for (int i = 0; i < 30; i++)
            numeric.Step();

        Assert.That(numeric.Effects.ActiveCount, Is.EqualTo(0));
        Assert.That(numeric.Attributes.GetCurrent(AttributeId.Health), Is.EqualTo(startHealth - 3000));
    }

    [Test]
    public void Refresh_ResetsRemainingFrames_WithoutExtraModifiers()
    {
        var numeric = new NumericSystem(CharacterNumericConfig.Default);
        int baseAttack = numeric.Attributes.GetCurrent(AttributeId.Attack);

        EffectDefinition buff = EffectDefinition.CreateDuration(
            "AttackUp",
            durationFrames: 10,
            EffectStackPolicy.Refresh,
            maxStacks: 1,
            new EffectModifierSpec(AttributeId.Attack, ModifierOp.Flat, 2000));

        numeric.ApplyEffect(buff);
        for (int i = 0; i < 4; i++)
            numeric.Step();

        Assert.That(numeric.Effects.FindById("AttackUp").RemainingFrames, Is.EqualTo(6));
        numeric.ApplyEffect(buff);
        Assert.That(numeric.Effects.FindById("AttackUp").RemainingFrames, Is.EqualTo(10));
        Assert.That(numeric.Attributes.GetCurrent(AttributeId.Attack), Is.EqualTo(baseAttack + 2000));
    }

    [Test]
    public void SameSteps_Replay_MatchesRemainingFrames()
    {
        NumericDebugSnapshot a = RunPoisonScenario();
        NumericDebugSnapshot b = RunPoisonScenario();

        Assert.That(b.HealthMilli, Is.EqualTo(a.HealthMilli));
        Assert.That(b.Effects.Length, Is.EqualTo(a.Effects.Length));
        Assert.That(b.Effects[0].RemainingFrames, Is.EqualTo(a.Effects[0].RemainingFrames));
        Assert.That(b.Effects[0].FramesUntilNextPeriod, Is.EqualTo(a.Effects[0].FramesUntilNextPeriod));
    }

    [Test]
    public void Instant_GrantEnergy_DoesNotStayActive()
    {
        var numeric = new NumericSystem(CharacterNumericConfig.Default);
        numeric.Attributes.AddToBase(AttributeId.Energy, -CharacterNumericConfig.ToMilli(20));
        int before = numeric.Attributes.GetCurrent(AttributeId.Energy);

        numeric.ApplyEffect(EffectDefinition.CreateInstant(
            "GrantEnergy",
            new EffectAttributeDelta(AttributeId.Energy, CharacterNumericConfig.ToMilli(5))));

        Assert.That(numeric.Effects.ActiveCount, Is.EqualTo(0));
        Assert.That(numeric.Attributes.GetCurrent(AttributeId.Energy), Is.EqualTo(before + 5000));
    }

    [Test]
    public void BuildDebugSnapshot_ListsActiveEffectsAndFlags()
    {
        var numeric = new NumericSystem(CharacterNumericConfig.Default);
        numeric.NotifyInCombat();
        numeric.ApplyEffect(EffectDefinition.CreateDuration(
            "AttackUp",
            durationFrames: 8,
            EffectStackPolicy.Replace,
            maxStacks: 1,
            new EffectModifierSpec(AttributeId.Attack, ModifierOp.Percent, 1250)));

        NumericDebugSnapshot snap = numeric.BuildDebugSnapshot();
        Assert.That(snap.InCombatHoldFrames, Is.GreaterThan(0));
        Assert.That(snap.Effects.Length, Is.EqualTo(1));
        Assert.That(snap.Effects[0].Id, Is.EqualTo("AttackUp"));
        Assert.That(snap.AttackMilli, Is.EqualTo(numeric.Attributes.GetCurrent(AttributeId.Attack)));
        Assert.That(snap.DefenseMilli, Is.EqualTo(numeric.Attributes.GetCurrent(AttributeId.Defense)));
        Assert.That(snap.OutgoingDamageMultMilli, Is.EqualTo(1000));
        Assert.That(snap.IncomingDamageMultMilli, Is.EqualTo(1000));
    }

    static NumericDebugSnapshot RunPoisonScenario()
    {
        var numeric = new NumericSystem(CharacterNumericConfig.Default);
        numeric.ApplyEffect(EffectDefinition.CreatePeriodic(
            "Poison",
            durationFrames: 25,
            intervalFrames: 5,
            EffectStackPolicy.Replace,
            maxStacks: 1,
            new EffectAttributeDelta(AttributeId.Health, -500)));

        for (int i = 0; i < 12; i++)
            numeric.Step();

        return numeric.BuildDebugSnapshot();
    }
}
