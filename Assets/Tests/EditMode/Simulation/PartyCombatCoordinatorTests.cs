using NUnit.Framework;

/// <summary>验证角色稳定身份与单键顺序切槽的纯模拟规则。</summary>
public sealed class PartyCombatCoordinatorTests
{
    /// <summary>角色 Id 必须去除首尾空白并按序号字符串比较。</summary>
    [Test]
    public void CharacterId_TrimsAndUsesOrdinalEquality()
    {
        var first = new CharacterId(" anby ");
        var second = new CharacterId("anby");

        Assert.That(first.IsValid, Is.True);
        Assert.That(first, Is.EqualTo(second));
        Assert.That(first.Value, Is.EqualTo("anby"));
    }

    /// <summary>阵容允许中间空槽，但拒绝重复 Id 与空开局槽。</summary>
    [Test]
    public void LoadoutRules_ValidateEmptySlotsAndStableIds()
    {
        CharacterId[] valid =
        {
            new("anby"),
            default,
            new("billy"),
        };
        Assert.That(
            PartyLoadoutRules.TryValidate(valid, 0, out PartyLoadoutValidationError validError),
            Is.True);
        Assert.That(validError, Is.EqualTo(PartyLoadoutValidationError.None));

        CharacterId[] duplicate =
        {
            new("anby"),
            new("anby"),
        };
        Assert.That(
            PartyLoadoutRules.TryValidate(duplicate, 0, out PartyLoadoutValidationError duplicateError),
            Is.False);
        Assert.That(duplicateError, Is.EqualTo(PartyLoadoutValidationError.DuplicateCharacterId));

        Assert.That(
            PartyLoadoutRules.TryValidate(valid, 1, out PartyLoadoutValidationError emptyStartError),
            Is.False);
        Assert.That(emptyStartError, Is.EqualTo(PartyLoadoutValidationError.EmptyStartingSlot));
    }

    /// <summary>顺序切人应绕回并跳过 Empty、Exiting 与 Dead 槽。</summary>
    [Test]
    public void Selector_SkipsUnavailableSlotsAndWraps()
    {
        PartyMemberState[] states =
        {
            PartyMemberState.Active,
            PartyMemberState.Empty,
            PartyMemberState.Inactive,
        };

        Assert.That(PartySlotSelector.TryFindNext(0, states, out int next), Is.True);
        Assert.That(next, Is.EqualTo(2));

        states[0] = PartyMemberState.Inactive;
        states[2] = PartyMemberState.Active;
        Assert.That(PartySlotSelector.TryFindNext(2, states, out next), Is.True);
        Assert.That(next, Is.EqualTo(0));
    }

    /// <summary>没有其他 Inactive 槽时不得把切人目标回退为当前角色。</summary>
    [Test]
    public void Selector_NoAvailableMember_Fails()
    {
        PartyMemberState[] states =
        {
            PartyMemberState.Active,
            PartyMemberState.Exiting,
            PartyMemberState.Dead,
        };

        Assert.That(PartySlotSelector.TryFindNext(0, states, out int next), Is.False);
        Assert.That(next, Is.EqualTo(-1));
    }

    /// <summary>普通切人必须输出 DualPresence，并在收招完成前跳过旧槽。</summary>
    [Test]
    public void Coordinator_SwitchIn_UsesSequentialDualPresence()
    {
        var coordinator = new PartyCombatCoordinator(
            new[] { true, true, true },
            0);

        Assert.That(coordinator.TryResolveSwitchIn(out PartySwitchCommand first), Is.True);
        Assert.That(first.FromSlot, Is.EqualTo(0));
        Assert.That(first.ToSlot, Is.EqualTo(1));
        Assert.That(first.Kind, Is.EqualTo(PartySwitchKind.SwitchIn));
        Assert.That(first.Presentation, Is.EqualTo(PartySwitchPresentation.DualPresence));
        Assert.That(coordinator.States[0], Is.EqualTo(PartyMemberState.Exiting));

        Assert.That(coordinator.TryResolveSwitchIn(out PartySwitchCommand second), Is.True);
        Assert.That(second.ToSlot, Is.EqualTo(2));
        coordinator.CompleteExit(0);

        Assert.That(coordinator.TryResolveSwitchIn(out PartySwitchCommand wrapped), Is.True);
        Assert.That(wrapped.ToSlot, Is.EqualTo(0));
    }

    /// <summary>占用表中的空槽必须保持 Empty，不能被单键选择器选中。</summary>
    [Test]
    public void Coordinator_EmptyMiddleSlot_IsSkipped()
    {
        var coordinator = new PartyCombatCoordinator(
            new[] { true, false, true },
            0);

        Assert.That(coordinator.States[1], Is.EqualTo(PartyMemberState.Empty));
        Assert.That(coordinator.TryResolveSwitchIn(out PartySwitchCommand command), Is.True);
        Assert.That(command.ToSlot, Is.EqualTo(2));
    }

    /// <summary>权威 Active 纠正必须清掉预测中的 Exiting，同时保留空槽。</summary>
    [Test]
    public void Coordinator_SynchronizeActive_ReplacesPredictedTransition()
    {
        var coordinator = new PartyCombatCoordinator(
            new[] { true, false, true },
            0);
        coordinator.TryResolveSwitchIn(out _);

        coordinator.SynchronizeActive(0);

        Assert.That(coordinator.ActiveIndex, Is.Zero);
        Assert.That(coordinator.States[0], Is.EqualTo(PartyMemberState.Active));
        Assert.That(coordinator.States[1], Is.EqualTo(PartyMemberState.Empty));
        Assert.That(coordinator.States[2], Is.EqualTo(PartyMemberState.Inactive));
    }

    /// <summary>阵容状态占用 FlagsPacked 低三位且必须保留其它战斗标志。</summary>
    [Test]
    public void ReplicationPacking_RoundTripsStateAndPreservesOtherFlags()
    {
        const int existingFlags = 1 << 8;
        int packed = PartyReplicationPacking.WithMemberState(
            existingFlags,
            PartyMemberState.Exiting);

        Assert.That(
            PartyReplicationPacking.ReadMemberState(packed),
            Is.EqualTo(PartyMemberState.Exiting));
        Assert.That(packed & existingFlags, Is.EqualTo(existingFlags));
    }

    /// <summary>普通换人落点必须跟随旧角色局部右向，而不是固定世界轴或相机朝向。</summary>
    [TestCase(0, 1600, 2000)]
    [TestCase(90000, 1000, 1400)]
    [TestCase(180000, 400, 2000)]
    [TestCase(-90000, 1000, 2600)]
    public void Placement_UsesOutgoingLocalRight(
        int facingMilliDeg,
        int expectedXMm,
        int expectedZMm)
    {
        SimVec2 result = PartySwitchPlacement.ResolveNormalSwitchPosition(
            new SimVec2(1000, 2000),
            facingMilliDeg);

        Assert.That(result.X, Is.EqualTo(expectedXMm));
        Assert.That(result.Z, Is.EqualTo(expectedZMm));
    }
}
