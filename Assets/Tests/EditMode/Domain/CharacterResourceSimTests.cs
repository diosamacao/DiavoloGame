using NUnit.Framework;

/// <summary>Wave 3 / N1：资源扣费、回填与接战回能（Assembly-CSharp-Editor，可见 Domain 类型）。</summary>
public sealed class CharacterResourceSimTests
{
    [Test]
    public void CanAfford_RejectsWhenEnergyTooLow()
    {
        var sim = new CharacterResourceSim(CharacterResourceConfig.Default);
        var drain = ActionResourceSpec.Create(energyCost: sim.MaxEnergy);
        Assert.That(sim.CanAfford(drain), Is.True);
        sim.CommitCost(drain);
        Assert.That(sim.EnergyPoints, Is.EqualTo(0));
        Assert.That(sim.CanAfford(ActionResourceSpec.Create(energyCost: 1)), Is.False);
    }

    [Test]
    public void GrantOnHit_AddsEnergyAndDecibel()
    {
        var sim = new CharacterResourceSim(CharacterResourceConfig.Default);
        sim.CommitCost(ActionResourceSpec.Create(energyCost: 40));
        sim.GrantOnHit(ActionResourceSpec.Create(energyGrantOnHit: 10, decibelGrantOnHit: 80));
        Assert.That(sim.EnergyPoints, Is.EqualTo(sim.MaxEnergy - 40 + 10));
        Assert.That(sim.Decibel, Is.EqualTo(80));
    }

    [Test]
    public void Step_RegensWhileInCombatHold()
    {
        var sim = new CharacterResourceSim(CharacterResourceConfig.Default);
        sim.CommitCost(ActionResourceSpec.Create(energyCost: 10));
        int before = sim.EnergyMilli;
        sim.Step();
        Assert.That(sim.EnergyMilli, Is.GreaterThan(before));
    }

    [Test]
    public void ClearsDecibelOnStart()
    {
        var sim = new CharacterResourceSim(CharacterResourceConfig.Default);
        sim.GrantOnHit(ActionResourceSpec.Create(decibelGrantOnHit: 3000));
        Assert.That(sim.Decibel, Is.EqualTo(sim.MaxDecibel));
        var ult = ActionResourceSpec.Create(requiresDecibelFull: true, clearsDecibelOnStart: true);
        Assert.That(sim.CanAfford(ult), Is.True);
        sim.CommitCost(ult);
        Assert.That(sim.Decibel, Is.EqualTo(0));
    }

    [Test]
    public void ConsumeDodgeCharge_BlocksWhenEmpty()
    {
        var sim = new CharacterResourceSim(CharacterResourceConfig.Default);
        var dodge = ActionResourceSpec.Create(consumeDodgeCharge: true);
        Assert.That(sim.CanAfford(dodge), Is.True);
        sim.CommitCost(dodge);
        sim.CommitCost(dodge);
        Assert.That(sim.DodgeCharges, Is.EqualTo(0));
        Assert.That(sim.CanAfford(dodge), Is.False);
    }
}
