using NUnit.Framework;

/// <summary>验证 ActionSim 起手前 NumericCostGate 鉴权与 Begin 扣费一次。</summary>
public sealed class ActionSimResourceGateTests
{
    [Test]
    public void TryStart_FailsWhenCannotAfford_AndDoesNotBegin()
    {
        var numeric = new NumericSystem(CharacterNumericConfig.Default);
        ActionResourceSpecEffectCompiler.ApplyCost(
            numeric,
            ActionResourceSpec.Create(energyCost: numeric.Attributes.GetPoints(AttributeId.MaxEnergy)));
        var gate = new CountingGate(numeric);
        var sim = new ActionSim(resourceGate: gate);
        var content = new FakeContent();

        bool started = sim.TryStart(ActionSimResolveResult.FromContent(content));
        Assert.That(started, Is.False);
        Assert.That(sim.IsActive, Is.False);
        Assert.That(gate.CommitCount, Is.EqualTo(0));
    }

    [Test]
    public void TryStart_CommitsCostOnceOnSuccess()
    {
        var numeric = new NumericSystem(CharacterNumericConfig.Default);
        var gate = new CountingGate(numeric);
        var sim = new ActionSim(resourceGate: gate);
        var content = new FakeContent();
        int maxEnergy = numeric.Attributes.GetPoints(AttributeId.MaxEnergy);

        Assert.That(sim.TryStart(ActionSimResolveResult.FromContent(content)), Is.True);
        Assert.That(gate.CommitCount, Is.EqualTo(1));
        Assert.That(numeric.Attributes.GetPoints(AttributeId.Energy), Is.EqualTo(maxEnergy - 10));
    }

    [Test]
    public void Grant_OnlyViaCompiler_DoesNotDoubleApply()
    {
        var numeric = new NumericSystem(CharacterNumericConfig.Default);
        ActionResourceSpecEffectCompiler.ApplyCost(
            numeric,
            ActionResourceSpec.Create(energyCost: 40));
        ActionResourceSpecEffectCompiler.ApplyGrant(
            numeric,
            ActionResourceSpec.Create(energyGrantOnHit: 10, decibelGrantOnHit: 80));

        Assert.That(
            numeric.Attributes.GetPoints(AttributeId.Energy),
            Is.EqualTo(numeric.Attributes.GetPoints(AttributeId.MaxEnergy) - 40 + 10));
        Assert.That(numeric.Attributes.GetPoints(AttributeId.Decibel), Is.EqualTo(80));
    }

    sealed class CountingGate : IActionResourceGate
    {
        readonly NumericSystem _numeric;
        public int CommitCount { get; private set; }

        public CountingGate(NumericSystem numeric) => _numeric = numeric;

        public bool CanAfford(IActionSimContent content) =>
            ActionResourceSpecEffectCompiler.CanAfford(
                _numeric,
                ActionResourceSpec.Create(energyCost: 10));

        public void CommitCost(IActionSimContent content)
        {
            CommitCount++;
            ActionResourceSpecEffectCompiler.ApplyCost(
                _numeric,
                ActionResourceSpec.Create(energyCost: 10));
        }
    }

    sealed class FakeContent : IActionSimContent
    {
        public bool IsSimulationReady => true;
        public int SampleRate => ActionSim.LogicHz;
        public int TotalFrames => 4;
        public int InterruptPriority => 0;
        public bool IsInterruptibleAtFrame(int frame) => true;
        public bool IsCancelWindowActiveAtFrame(CancelWindowType windowType, int frame) => false;
        public bool AllowsRecoveryEntryRestartAtFrame(int frame) => false;
        public bool AllowsMovementCancelAtFrame(int frame) => false;
    }
}
