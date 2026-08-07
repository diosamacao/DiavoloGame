using NUnit.Framework;

/// <summary>GAS G4：攻防公式、出伤/承伤倍率与 DOT 经 Health handler（无 Reaction 语义）。</summary>
public sealed class DamageNumericCalculatorTests
{
    [Test]
    public void Baseline_MatchesReferenceAttackScaling()
    {
        var attacker = new NumericSystem(CharacterNumericConfig.Default);
        var defender = new NumericSystem(CharacterNumericConfig.Default);

        // Attack=10, BaseDamage=10, Def=0 → 10 点（与旧扁平伤对齐）
        int milli = DamageNumericCalculator.CalculateMilli(attacker, defender, baseDamage: 10f);
        Assert.That(milli, Is.EqualTo(10_000));
    }

    [Test]
    public void OutgoingMult_1250_IncreasesDamage()
    {
        var attacker = new NumericSystem(CharacterNumericConfig.Default);
        var defender = new NumericSystem(CharacterNumericConfig.Default);
        attacker.Attributes.SetBase(AttributeId.OutgoingDamageMult, 1250);

        int milli = DamageNumericCalculator.CalculateMilli(attacker, defender, 10f);
        Assert.That(milli, Is.EqualTo(12_500));
    }

    [Test]
    public void IncomingMult_800_ReducesDamage()
    {
        var attacker = new NumericSystem(CharacterNumericConfig.Default);
        var defender = new NumericSystem(CharacterNumericConfig.Default);
        defender.Attributes.SetBase(AttributeId.IncomingDamageMult, 800);

        int milli = DamageNumericCalculator.CalculateMilli(attacker, defender, 10f);
        Assert.That(milli, Is.EqualTo(8_000));
    }

    [Test]
    public void StackedMults_MultiplyInStableOrder()
    {
        var attacker = new NumericSystem(CharacterNumericConfig.Default);
        var defender = new NumericSystem(CharacterNumericConfig.Default);
        attacker.Attributes.SetBase(AttributeId.OutgoingDamageMult, 1250);
        defender.Attributes.SetBase(AttributeId.IncomingDamageMult, 800);

        // 10 × 1.25 × 0.8 = 10
        int milli = DamageNumericCalculator.CalculateMilli(attacker, defender, 10f);
        Assert.That(milli, Is.EqualTo(10_000));
    }

    [Test]
    public void HigherBaseAttack_IncreasesDamageWithoutBuff()
    {
        var low = new NumericSystem(CharacterNumericConfig.Default);
        var high = new NumericSystem(CharacterNumericConfig.Default);
        var defender = new NumericSystem(CharacterNumericConfig.Default);
        high.Attributes.SetBase(AttributeId.Attack, CharacterNumericConfig.ToMilli(20));

        int lowDmg = DamageNumericCalculator.CalculateMilli(low, defender, 10f);
        int highDmg = DamageNumericCalculator.CalculateMilli(high, defender, 10f);
        Assert.That(highDmg, Is.EqualTo(lowDmg * 2));
    }

    [Test]
    public void Defense_MitigatesDamage()
    {
        var attacker = new NumericSystem(CharacterNumericConfig.Default);
        var defender = new NumericSystem(CharacterNumericConfig.Default);
        // Defense = K = 100 点 → 减半
        defender.Attributes.SetBase(AttributeId.Defense, DamageNumericCalculator.DefenseConstantMilli);

        int milli = DamageNumericCalculator.CalculateMilli(attacker, defender, 10f);
        Assert.That(milli, Is.EqualTo(5_000));
    }

    [Test]
    public void PeriodicDot_UsesHealthHandler_ThreeTicksTotal()
    {
        var numeric = new NumericSystem(CharacterNumericConfig.Default);
        int handlerCalls = 0;
        int totalDamageMilli = 0;
        numeric.Effects.SetHealthDamageHandler(milli =>
        {
            handlerCalls++;
            totalDamageMilli += milli;
            numeric.Attributes.AddToBase(AttributeId.Health, -milli);
        });

        int start = numeric.Attributes.GetCurrent(AttributeId.Health);
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

        Assert.That(handlerCalls, Is.EqualTo(3));
        Assert.That(totalDamageMilli, Is.EqualTo(3000));
        Assert.That(numeric.Attributes.GetCurrent(AttributeId.Health), Is.EqualTo(start - 3000));
        Assert.That(numeric.Effects.ActiveCount, Is.EqualTo(0));
    }
}
