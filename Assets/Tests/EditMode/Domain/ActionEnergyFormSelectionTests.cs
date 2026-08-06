using NUnit.Framework;

/// <summary>Wave 3 / N5：同键 Special 能量分支选形。</summary>
public sealed class ActionEnergyFormSelectionTests
{
    [Test]
    public void PrefersExSpecial_WhenAffordable()
    {
        ActionResourceTag[] tags =
        {
            ActionResourceTag.Special,
            ActionResourceTag.ExSpecial,
        };

        bool found = ActionEnergyFormSelector.TryFindIndex(
            tags.Length,
            i => tags[i],
            _ => true,
            out int index);

        Assert.That(found, Is.True);
        Assert.That(index, Is.EqualTo(1));
        Assert.That(
            ActionEnergyFormSelector.WouldSelectExSpecial(tags.Length, i => tags[i], _ => true),
            Is.True);
    }

    [Test]
    public void FallsBackToSpecial_WhenExNotAffordable()
    {
        ActionResourceTag[] tags =
        {
            ActionResourceTag.Special,
            ActionResourceTag.ExSpecial,
        };

        bool found = ActionEnergyFormSelector.TryFindIndex(
            tags.Length,
            i => tags[i],
            i => tags[i] != ActionResourceTag.ExSpecial,
            out int index);

        Assert.That(found, Is.True);
        Assert.That(index, Is.EqualTo(0));
        Assert.That(
            ActionEnergyFormSelector.WouldSelectExSpecial(
                tags.Length,
                i => tags[i],
                i => tags[i] != ActionResourceTag.ExSpecial),
            Is.False);
    }

    [Test]
    public void SingleExCandidate_ReturnedEvenIfUnaffordable()
    {
        ActionResourceTag[] tags = { ActionResourceTag.ExSpecial };

        bool found = ActionEnergyFormSelector.TryFindIndex(
            tags.Length,
            i => tags[i],
            _ => false,
            out int index);

        Assert.That(found, Is.True);
        Assert.That(index, Is.EqualTo(0));
    }

    [Test]
    public void MultiCandidateAllUnaffordable_Fails()
    {
        ActionResourceTag[] tags =
        {
            ActionResourceTag.Special,
            ActionResourceTag.ExSpecial,
        };

        bool found = ActionEnergyFormSelector.TryFindIndex(
            tags.Length,
            i => tags[i],
            _ => false,
            out _);

        Assert.That(found, Is.False);
    }
}
