using NUnit.Framework;

/// <summary>验证 ActionSim 起手前 Gate 鉴权与 Begin 扣费一次。</summary>
public sealed class ActionSimResourceGateTests
{
    [Test]
    public void TryStart_FailsWhenCannotAfford_AndDoesNotBegin()
    {
        var resources = new CharacterResourceSim(CharacterResourceConfig.Default);
        resources.CommitCost(ActionResourceSpec.Create(energyCost: resources.MaxEnergy));
        var gate = new CountingGate(resources);
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
        var resources = new CharacterResourceSim(CharacterResourceConfig.Default);
        var gate = new CountingGate(resources);
        var sim = new ActionSim(resourceGate: gate);
        var content = new FakeContent();

        Assert.That(sim.TryStart(ActionSimResolveResult.FromContent(content)), Is.True);
        Assert.That(gate.CommitCount, Is.EqualTo(1));
        Assert.That(resources.EnergyPoints, Is.EqualTo(resources.MaxEnergy - 10));
    }

    sealed class CountingGate : IActionResourceGate
    {
        readonly CharacterResourceSim _resources;
        public int CommitCount { get; private set; }

        public CountingGate(CharacterResourceSim resources) => _resources = resources;

        public bool CanAfford(IActionSimContent content) =>
            _resources.CanAfford(ActionResourceSpec.Create(energyCost: 10));

        public void CommitCost(IActionSimContent content)
        {
            CommitCount++;
            _resources.CommitCost(ActionResourceSpec.Create(energyCost: 10));
        }
    }

    /// <summary>最小可 Begin 的假内容。</summary>
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
