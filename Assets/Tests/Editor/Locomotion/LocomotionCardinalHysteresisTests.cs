using NUnit.Framework;

/// <summary>L-DIR2 Gait Cardinal 滞回：最短驻留后才换向。</summary>
public sealed class LocomotionCardinalHysteresisTests
{
    [Test]
    public void FirstProposal_AdoptsImmediately()
    {
        MoveCardinal current = MoveCardinal.None;
        int dwell = 0;
        MoveCardinal r = LocomotionCardinalHysteresis.Resolve(
            ref current, ref dwell, MoveCardinal.Left, 3);
        Assert.That(r, Is.EqualTo(MoveCardinal.Left));
        Assert.That(current, Is.EqualTo(MoveCardinal.Left));
    }

    [Test]
    public void Switch_BlockedUntilMinDwell()
    {
        MoveCardinal current = MoveCardinal.None;
        int dwell = 0;
        LocomotionCardinalHysteresis.Resolve(ref current, ref dwell, MoveCardinal.Left, 3);
        Assert.That(
            LocomotionCardinalHysteresis.Resolve(ref current, ref dwell, MoveCardinal.Right, 3),
            Is.EqualTo(MoveCardinal.Left));
        Assert.That(
            LocomotionCardinalHysteresis.Resolve(ref current, ref dwell, MoveCardinal.Right, 3),
            Is.EqualTo(MoveCardinal.Left));
        Assert.That(
            LocomotionCardinalHysteresis.Resolve(ref current, ref dwell, MoveCardinal.Right, 3),
            Is.EqualTo(MoveCardinal.Left));
        Assert.That(
            LocomotionCardinalHysteresis.Resolve(ref current, ref dwell, MoveCardinal.Right, 3),
            Is.EqualTo(MoveCardinal.Right));
    }

    [Test]
    public void MinDwellZero_SwitchesNextFrame()
    {
        MoveCardinal current = MoveCardinal.None;
        int dwell = 0;
        LocomotionCardinalHysteresis.Resolve(ref current, ref dwell, MoveCardinal.Forward, 0);
        Assert.That(
            LocomotionCardinalHysteresis.Resolve(ref current, ref dwell, MoveCardinal.Left, 0),
            Is.EqualTo(MoveCardinal.Left));
    }
}
