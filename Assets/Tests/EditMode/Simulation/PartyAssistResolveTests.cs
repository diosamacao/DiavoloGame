using NUnit.Framework;

/// <summary>P-SW2 Coordinator 裁定表：Gold/Red/点数/远程点名/突击武装。</summary>
public sealed class PartyAssistResolveTests
{
    static PartyCombatCoordinator Create(
        CharacterAssistStyle secondStyle = CharacterAssistStyle.MeleeParry)
    {
        return new PartyCombatCoordinator(
            new[] { true, true, true },
            0,
            new[]
            {
                CharacterAssistStyle.MeleeParry,
                secondStyle,
                CharacterAssistStyle.MeleeParry,
            });
    }

    /// <summary>无 Cue 仍是 DualPresence SwitchIn。</summary>
    [Test]
    public void NoCue_ResolvesSwitchInDualPresence()
    {
        PartyCombatCoordinator coordinator = Create();
        Assert.That(
            coordinator.TryResolveSwitch(PartyAssistResolveQuery.None, out PartySwitchCommand command),
            Is.True);
        Assert.That(command.Kind, Is.EqualTo(PartySwitchKind.SwitchIn));
        Assert.That(command.Presentation, Is.EqualTo(PartySwitchPresentation.DualPresence));
        Assert.That(coordinator.States[0], Is.EqualTo(PartyMemberState.Exiting));
        Assert.That(command.SpendAssistPoints, Is.Zero);
    }

    /// <summary>近战 Gold 且有点：AssistParry InstantReplace，扣 1 点。</summary>
    [Test]
    public void Gold_Melee_SpendsAndAssistParry()
    {
        PartyCombatCoordinator coordinator = Create();
        var query = new PartyAssistResolveQuery(
            hasCue: true,
            AssistCueKind.Gold,
            requiresRanged: false,
            assistFollowUpArmed: false,
            new SimActorId(9));

        Assert.That(coordinator.TryResolveSwitch(in query, out PartySwitchCommand command), Is.True);
        Assert.That(command.Kind, Is.EqualTo(PartySwitchKind.AssistParry));
        Assert.That(command.Presentation, Is.EqualTo(PartySwitchPresentation.InstantReplace));
        Assert.That(coordinator.States[0], Is.EqualTo(PartyMemberState.Inactive));
        Assert.That(coordinator.AssistPoints.Current, Is.EqualTo(PartyAssistPoints.Starting - 1));
        Assert.That(command.SpendAssistPoints, Is.EqualTo(1));
        Assert.That(command.CueOwnerId.Value, Is.EqualTo(9));
    }

    /// <summary>远程上场 Gold：AssistEvade。</summary>
    [Test]
    public void Gold_Ranged_ResolvesAssistEvade()
    {
        PartyCombatCoordinator coordinator = Create(CharacterAssistStyle.RangedEvade);
        var query = new PartyAssistResolveQuery(true, AssistCueKind.Gold, false, false);

        Assert.That(coordinator.TryResolveSwitch(in query, out PartySwitchCommand command), Is.True);
        Assert.That(command.Kind, Is.EqualTo(PartySwitchKind.AssistEvade));
        Assert.That(command.Presentation, Is.EqualTo(PartySwitchPresentation.InstantReplace));
    }

    /// <summary>0 点 Gold 对外当 Red：换人闪，不扣点。</summary>
    [Test]
    public void Gold_ZeroPoints_BecomesRedSwitchPerfectDodge()
    {
        PartyCombatCoordinator coordinator = Create();
        while (coordinator.AssistPoints.CanSpendAssist)
            coordinator.AssistPoints.TrySpendAssist();

        var query = new PartyAssistResolveQuery(true, AssistCueKind.Gold, false, false);
        Assert.That(coordinator.TryResolveSwitch(in query, out PartySwitchCommand command), Is.True);
        Assert.That(command.Kind, Is.EqualTo(PartySwitchKind.SwitchPerfectDodge));
        Assert.That(command.SpendAssistPoints, Is.Zero);
        Assert.That(coordinator.AssistPoints.Current, Is.Zero);
    }

    /// <summary>远程点名金光切远程：仍走 AssistEvade 并扣点。</summary>
    [Test]
    public void Gold_RequiresRanged_RangedIncoming_IsAssistEvade()
    {
        PartyCombatCoordinator coordinator = Create(CharacterAssistStyle.RangedEvade);
        var query = new PartyAssistResolveQuery(true, AssistCueKind.Gold, true, false);

        Assert.That(coordinator.TryResolveSwitch(in query, out PartySwitchCommand command), Is.True);
        Assert.That(command.Kind, Is.EqualTo(PartySwitchKind.AssistEvade));
        Assert.That(coordinator.AssistPoints.Current, Is.EqualTo(PartyAssistPoints.Starting - 1));
    }

    /// <summary>远程点名金光切近战：按 Red 处理。</summary>
    [Test]
    public void Gold_RequiresRanged_MeleeIncoming_IsRed()
    {
        PartyCombatCoordinator coordinator = Create();
        var query = new PartyAssistResolveQuery(true, AssistCueKind.Gold, true, false);

        Assert.That(coordinator.TryResolveSwitch(in query, out PartySwitchCommand command), Is.True);
        Assert.That(command.Kind, Is.EqualTo(PartySwitchKind.SwitchPerfectDodge));
        Assert.That(coordinator.AssistPoints.Current, Is.EqualTo(PartyAssistPoints.Starting));
    }

    /// <summary>突击武装中禁止切人。</summary>
    [Test]
    public void AssistFollowUpArmed_RejectsSwitch()
    {
        PartyCombatCoordinator coordinator = Create();
        var query = new PartyAssistResolveQuery(true, AssistCueKind.Gold, false, true);
        Assert.That(coordinator.TryResolveSwitch(in query, out _), Is.False);
        Assert.That(coordinator.ActiveIndex, Is.Zero);
        Assert.That(coordinator.AssistPoints.Current, Is.EqualTo(PartyAssistPoints.Starting));
    }

    /// <summary>CueBoard 优先锁定目标，否则取最小 OwnerId。</summary>
    [Test]
    public void CueBoard_PrefersSelectedThenLowestId()
    {
        var board = new WorldAssistCueBoard();
        board.BeginFrame();
        board.Publish(new AssistCue(new SimActorId(8), AssistCueKind.Gold, false, 4, 0, 0, 1200, 0, 0, 1400));
        board.Publish(new AssistCue(new SimActorId(3), AssistCueKind.Red, false, 2, 0, 0, 1200, 0, 0, 1400));

        Assert.That(board.TryGetActive(new SimActorId(8), out AssistCue preferred), Is.True);
        Assert.That(preferred.OwnerId.Value, Is.EqualTo(8));
        Assert.That(preferred.Kind, Is.EqualTo(AssistCueKind.Gold));

        Assert.That(board.TryGetActive(SimActorId.Invalid, out AssistCue lowest), Is.True);
        Assert.That(lowest.OwnerId.Value, Is.EqualTo(3));
    }
}
