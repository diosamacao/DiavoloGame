using NUnit.Framework;

/// <summary>吸附捕获轴、烘焙节奏重映射与末帧毫米级收敛回归。</summary>
public sealed class ActionMotionAdhesionTests
{
    static ActionMotionAdhesionParams Window(int offset = 1000, int lateral = 0, int cap = 100000,
        int distance = 100000, int angle = 0) => new(0, 9, offset, lateral, cap, distance, angle);

    [TestCase(1000, 3000)]
    [TestCase(0, 2000)]
    [TestCase(-500, 1500)]
    public void LongBake_ReachesSpecifiedOffsetWithoutAddingFullBake(int offset, int expectedZ)
    {
        var state = new ActionMotionAdhesion.State();
        var window = Window(offset);
        int x = 0, z = 0;
        for (int frame = 0; frame <= 9; frame++)
        {
            Assert.That(ActionMotionAdhesion.TryComputeDisplacementMm(ref state, x, z, 0,
                0, 2000, in window, frame, 0, 1000, 1d / (10 - frame), out int dx, out int dz), Is.True);
            x += dx; z += dz;
            Assert.That(z, Is.InRange(0, expectedZ));
        }
        Assert.That(x, Is.Zero);
        Assert.That(z, Is.EqualTo(expectedZ));
        Assert.That(state.DesiredZMm, Is.EqualTo(expectedZ));
    }

    [Test]
    public void CrossEnemy_WithLateralOffset_KeepsCapturedSide()
    {
        var state = new ActionMotionAdhesion.State();
        var window = Window(lateral: 500);
        ActionMotionAdhesion.TryComputeDisplacementMm(ref state, 0, 0, 0, 0, 2000,
            in window, 0, 0, 0, .1, out _, out _);
        ActionMotionAdhesion.TryComputeDisplacementMm(ref state, -400, 2500, 180, 0, 2000,
            in window, 9, 0, 1000, 1, out int dx, out int dz);
        Assert.That(-400 + dx, Is.EqualTo(-500));
        Assert.That(2500 + dz, Is.EqualTo(3000));
    }

    [Test]
    public void Overshoot_IsCorrectedBackToCapturedEndpoint()
    {
        var state = new ActionMotionAdhesion.State();
        var window = Window();
        ActionMotionAdhesion.TryComputeDisplacementMm(ref state, 0, 0, 0, 0, 2000,
            in window, 0, 0, 0, .1, out _, out _);
        ActionMotionAdhesion.TryComputeDisplacementMm(ref state, 0, 3500, 0, 0, 2000,
            in window, 9, 0, 1000, 1, out _, out int dz);
        Assert.That(dz, Is.EqualTo(-500));
    }

    [Test]
    public void MovingEnemy_TranslatesEndpointWithoutRotatingOffset()
    {
        var state = new ActionMotionAdhesion.State();
        var window = Window();
        ActionMotionAdhesion.TryComputeDisplacementMm(ref state, 0, 0, 0, 0, 2000,
            in window, 0, 0, 0, .1, out _, out _);
        ActionMotionAdhesion.TryComputeDisplacementMm(ref state, 0, 2500, 0, 1000, 2500,
            in window, 9, 0, 0, 1, out int dx, out int dz);
        Assert.That(dx, Is.EqualTo(1000));
        Assert.That(2500 + dz, Is.EqualTo(3500));
    }

    [Test]
    public void SmallCorrectionCap_LimitsIntermediateButNotFinalSettlement()
    {
        var state = new ActionMotionAdhesion.State();
        var window = Window(cap: 10);
        ActionMotionAdhesion.TryComputeDisplacementMm(ref state, 0, 0, 0, 0, 2000,
            in window, 0, 0, 0, .1, out _, out int first);
        Assert.That(first, Is.EqualTo(10));
        ActionMotionAdhesion.TryComputeDisplacementMm(ref state, 0, first, 0, 0, 2000,
            in window, 9, 0, 0, 1, out _, out int last);
        Assert.That(first + last, Is.EqualTo(3000));
    }

    [TestCase(1000, 0, 0, 2000)]
    [TestCase(100000, 45000, 2000, 0)]
    public void AcquisitionOutsideDistanceOrAngle_PreservesBase(int distance, int angle, int tx, int tz)
    {
        var state = new ActionMotionAdhesion.State();
        var window = Window(distance: distance, angle: angle);
        Assert.That(ActionMotionAdhesion.TryComputeDisplacementMm(ref state, 0, 0, 0, tx, tz,
            in window, 0, 2, 30, .1, out int dx, out int dz), Is.False);
        Assert.That(dx, Is.EqualTo(2)); Assert.That(dz, Is.EqualTo(30));
        Assert.That(state.Acquired, Is.False);
    }

    [Test]
    public void OutsideWindow_DoesNotSuppressBaseMotion()
    {
        var state = new ActionMotionAdhesion.State();
        var window = Window();
        Assert.That(ActionMotionAdhesion.TryComputeDisplacementMm(ref state, 0, 0, 0, 0, 2000,
            in window, 10, 20, 50, 1, out int dx, out int dz), Is.False);
        Assert.That(dx, Is.EqualTo(20)); Assert.That(dz, Is.EqualTo(50));
    }

    [Test]
    public void BackwardSeek_RecapturesAndMatchesFreshState()
    {
        var state = new ActionMotionAdhesion.State();
        var window = Window();
        ActionMotionAdhesion.TryComputeDisplacementMm(ref state, 0, 0, 0, 0, 2000,
            in window, 8, 0, 0, .5, out _, out _);
        var fresh = new ActionMotionAdhesion.State();
        ActionMotionAdhesion.TryComputeDisplacementMm(ref state, 0, 0, 90, 2000, 0,
            in window, 0, 0, 0, .1, out int x, out int z);
        ActionMotionAdhesion.TryComputeDisplacementMm(ref fresh, 0, 0, 90, 2000, 0,
            in window, 0, 0, 0, .1, out int fx, out int fz);
        Assert.That(x, Is.EqualTo(fx)); Assert.That(z, Is.EqualTo(fz));
    }

    [Test]
    public void BakedProgress_PreservesFastSlowAndStationaryFrames()
    {
        var baked = new ActionBakedMotion { frameCount = 3, bakeStatus = ActionBakedMotionStatus.Ok,
            positionDeltaMmX = new[] { 0, 0, 0 }, positionDeltaMmZ = new[] { 100, 0, 300 },
            yawDeltaMilliDeg = new[] { 0, 0, 0 } };
        Assert.That(ActionMotionAdhesion.BakedProgress(baked, 0, 2), Is.EqualTo(.25));
        Assert.That(ActionMotionAdhesion.BakedProgress(baked, 1, 2), Is.Zero);
        Assert.That(ActionMotionAdhesion.BakedProgress(baked, 2, 2), Is.EqualTo(1));
        Assert.That(ActionMotionAdhesion.BakedProgress(null, 0, 9), Is.EqualTo(.1));
    }
}