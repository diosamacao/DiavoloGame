using NUnit.Framework;

/// <summary>GAS G1：Attribute 聚合、Max 钳制、旗标递减与被动 Step。</summary>
public sealed class NumericSystemTests
{
    [Test]
    public void Config_InitializesFullPoolsAndCombatStats()
    {
        var numeric = new NumericSystem(CharacterNumericConfig.Default);

        Assert.That(numeric.Attributes.GetPoints(AttributeId.Health), Is.EqualTo(100));
        Assert.That(numeric.Attributes.GetPoints(AttributeId.MaxHealth), Is.EqualTo(100));
        Assert.That(numeric.Attributes.GetPoints(AttributeId.Energy), Is.EqualTo(120));
        Assert.That(numeric.Attributes.GetPoints(AttributeId.MaxEnergy), Is.EqualTo(120));
        Assert.That(numeric.Attributes.GetPoints(AttributeId.Decibel), Is.EqualTo(0));
        Assert.That(numeric.Attributes.GetPoints(AttributeId.DodgeCharges), Is.EqualTo(2));
        Assert.That(numeric.Attributes.GetPoints(AttributeId.Attack), Is.EqualTo(10));
        Assert.That(numeric.Attributes.GetCurrent(AttributeId.EnergyRegenMilliPerFrame), Is.EqualTo(200));
    }

    [Test]
    public void Aggregator_FlatPlusPercent_MatchesGoldValue()
    {
        var set = new AttributeSet();
        set.SetBase(AttributeId.Attack, 1000);
        set.AddModifier(AttributeId.Attack, ModifierOp.Flat, 100);
        set.AddModifier(AttributeId.Attack, ModifierOp.Percent, 1250); // ×1.25

        // (1000 + 100) * 1.25 = 1375
        Assert.That(set.GetCurrent(AttributeId.Attack), Is.EqualTo(1375));
        Assert.That(set.GetBase(AttributeId.Attack), Is.EqualTo(1000));
    }

    [Test]
    public void Energy_CurrentClampedToMax_AndBaseCannotExceedMax()
    {
        var numeric = new NumericSystem(CharacterNumericConfig.Default);
        int maxMilli = numeric.Attributes.GetCurrent(AttributeId.MaxEnergy);

        numeric.Attributes.SetBase(AttributeId.Energy, maxMilli + 50_000);
        Assert.That(numeric.Attributes.GetBase(AttributeId.Energy), Is.EqualTo(maxMilli));
        Assert.That(numeric.Attributes.GetCurrent(AttributeId.Energy), Is.EqualTo(maxMilli));

        numeric.Attributes.SetBase(AttributeId.Energy, -1);
        Assert.That(numeric.Attributes.GetBase(AttributeId.Energy), Is.EqualTo(0));
        Assert.That(numeric.Attributes.GetCurrent(AttributeId.Energy), Is.EqualTo(0));
    }

    [Test]
    public void MaxEnergyModifier_ClampsEnergyPool()
    {
        var numeric = new NumericSystem(CharacterNumericConfig.Default);
        int handle = numeric.Attributes.AddModifier(
            AttributeId.MaxEnergy,
            ModifierOp.Percent,
            500); // Max ×0.5

        Assert.That(numeric.Attributes.GetPoints(AttributeId.MaxEnergy), Is.EqualTo(60));
        Assert.That(numeric.Attributes.GetPoints(AttributeId.Energy), Is.EqualTo(60));

        Assert.That(numeric.Attributes.RemoveModifier(handle), Is.True);
        Assert.That(numeric.Attributes.GetPoints(AttributeId.MaxEnergy), Is.EqualTo(120));
        // Base 已被钳到 60 点，恢复 Max 后 Current 仍为 60（不自动回满）
        Assert.That(numeric.Attributes.GetPoints(AttributeId.Energy), Is.EqualTo(60));
    }

    [Test]
    public void Flags_StepDecrementsHoldAndCounter()
    {
        var numeric = new NumericSystem(CharacterNumericConfig.Default);
        numeric.NotifyInCombat();
        numeric.ArmPerfectDodgeCounter();

        Assert.That(numeric.Flags.IsInCombat, Is.True);
        Assert.That(numeric.Flags.HasPerfectDodgeCounter, Is.True);

        int hold = numeric.Flags.InCombatHoldFrames;
        int counter = numeric.Flags.PerfectDodgeCounterFrames;
        numeric.Step();

        Assert.That(numeric.Flags.InCombatHoldFrames, Is.EqualTo(hold - 1));
        Assert.That(numeric.Flags.PerfectDodgeCounterFrames, Is.EqualTo(counter - 1));
    }

    [Test]
    public void Step_RegensEnergyWhileInCombatHold()
    {
        var numeric = new NumericSystem(CharacterNumericConfig.Default);
        numeric.Attributes.AddToBase(AttributeId.Energy, -CharacterNumericConfig.ToMilli(10));
        numeric.NotifyInCombat();

        int before = numeric.Attributes.GetCurrent(AttributeId.Energy);
        numeric.Step();
        Assert.That(numeric.Attributes.GetCurrent(AttributeId.Energy), Is.EqualTo(before + 200));
    }

    [Test]
    public void TryConsumeDodgeCharge_StartsRechargeAndBlocksAtZero()
    {
        var numeric = new NumericSystem(CharacterNumericConfig.Default);
        Assert.That(numeric.TryConsumeDodgeCharge(), Is.True);
        Assert.That(numeric.TryConsumeDodgeCharge(), Is.True);
        Assert.That(numeric.Attributes.GetPoints(AttributeId.DodgeCharges), Is.EqualTo(0));
        Assert.That(numeric.TryConsumeDodgeCharge(), Is.False);
        Assert.That(numeric.Flags.DodgeRechargeFramesLeft, Is.GreaterThan(0));
    }
}
