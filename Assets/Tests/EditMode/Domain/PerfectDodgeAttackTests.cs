using NUnit.Framework;

/// <summary>Wave 3.4：PerfectDodgeAttack 优先级与起手清反击缓冲。</summary>
public sealed class PerfectDodgeAttackTests
{
    [Test]
    public void CancelPriority_PerfectDodgeAttack_BeatsDodgeAttack()
    {
        Assert.That(
            GameplayIntentCancelPriority.Get(GameplayIntentType.PerfectDodgeAttack),
            Is.GreaterThan(GameplayIntentCancelPriority.Get(GameplayIntentType.DodgeAttack)));
        Assert.That(
            GameplayIntentCancelPriority.Get(GameplayIntentType.PerfectDodgeAttack),
            Is.LessThan(GameplayIntentCancelPriority.Get(GameplayIntentType.Ultimate)));
    }

    [Test]
    public void ActionSim_BeginPerfectDodgeAttack_InvokesOnBegun()
    {
        GameplayIntentType seen = GameplayIntentType.None;
        var sim = new ActionSim(onBegun: intent => seen = intent);
        var content = new FakeContent();

        Assert.That(
            sim.TryStart(ActionSimResolveResult.FromGraph(
                content,
                graph: null,
                nodeId: null,
                GameplayIntentType.PerfectDodgeAttack)),
            Is.True);
        Assert.That(seen, Is.EqualTo(GameplayIntentType.PerfectDodgeAttack));
    }

    [Test]
    public void ActionSim_BeginPerfectDodgeAttack_ClearsCounterFlag()
    {
        var numeric = new NumericSystem(CharacterNumericConfig.Default);
        numeric.ArmPerfectDodgeCounter();
        Assert.That(numeric.Flags.HasPerfectDodgeCounter, Is.True);

        var sim = new ActionSim(
            onBegun: intent =>
            {
                if (intent == GameplayIntentType.PerfectDodgeAttack)
                    numeric.ClearPerfectDodgeCounter();
            });

        Assert.That(
            sim.TryStart(ActionSimResolveResult.FromGraph(
                new FakeContent(),
                null,
                null,
                GameplayIntentType.PerfectDodgeAttack)),
            Is.True);
        Assert.That(numeric.Flags.HasPerfectDodgeCounter, Is.False);
    }

    [Test]
    public void ActionSim_BeginAttack_DoesNotClearCounterFlag()
    {
        var numeric = new NumericSystem(CharacterNumericConfig.Default);
        numeric.ArmPerfectDodgeCounter();

        var sim = new ActionSim(
            onBegun: intent =>
            {
                if (intent == GameplayIntentType.PerfectDodgeAttack)
                    numeric.ClearPerfectDodgeCounter();
            });

        Assert.That(
            sim.TryStart(ActionSimResolveResult.FromGraph(
                new FakeContent(),
                null,
                null,
                GameplayIntentType.Attack)),
            Is.True);
        Assert.That(numeric.Flags.HasPerfectDodgeCounter, Is.True);
    }

    /// <summary>最小可起手内容（与 ResourceGate 测试同形）。</summary>
    sealed class FakeContent : IActionSimContent
    {
        public bool IsSimulationReady => true;
        public int SampleRate => ActionSim.LogicHz;
        public int TotalFrames => 2;
        public int InterruptPriority => 0;
        public bool IsInterruptibleAtFrame(int frame) => true;
        public bool IsCancelWindowActiveAtFrame(CancelWindowType windowType, int frame) => false;
        public bool AllowsRecoveryEntryRestartAtFrame(int frame) => false;
        public bool AllowsMovementCancelAtFrame(int frame) => false;
    }
}
