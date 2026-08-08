using NUnit.Framework;

/// <summary>Wave 4：TargetAdhesion 连线 desired 与剩余帧均摊（Simulation 纯函数）。</summary>
public sealed class ActionMotionAdhesionTests
{
    /// <summary>offset&gt;0 时 desired 落在敌人连线远侧（穿后）。</summary>
    [Test]
    public void BuildDesired_PositiveOffset_IsBeyondEnemyAlongAxis()
    {
        // 玩家在原点，敌人在 +Z 2000mm
        Assert.That(
            ActionMotionAdhesion.TryBuildDesiredMm(
                actorXMm: 0,
                actorZMm: 0,
                targetXMm: 0,
                targetZMm: 2000,
                horizontalOffsetMm: 1000,
                lateralOffsetMm: 0,
                out int desiredX,
                out int desiredZ),
            Is.True);
        Assert.That(desiredX, Is.EqualTo(0));
        Assert.That(desiredZ, Is.EqualTo(3000));
    }

    /// <summary>offset=0 吸向敌人中心。</summary>
    [Test]
    public void BuildDesired_ZeroOffset_IsEnemyCenter()
    {
        Assert.That(
            ActionMotionAdhesion.TryBuildDesiredMm(
                0, 0, 500, 0, 0, 0, out int desiredX, out int desiredZ),
            Is.True);
        Assert.That(desiredX, Is.EqualTo(500));
        Assert.That(desiredZ, Is.EqualTo(0));
    }

    /// <summary>offset&lt;0 停在敌人身前（连线近侧）。</summary>
    [Test]
    public void BuildDesired_NegativeOffset_IsInFrontOfEnemy()
    {
        Assert.That(
            ActionMotionAdhesion.TryBuildDesiredMm(
                0, 0, 0, 2000, -500, 0, out int desiredX, out int desiredZ),
            Is.True);
        Assert.That(desiredX, Is.EqualTo(0));
        Assert.That(desiredZ, Is.EqualTo(1500));
    }

    /// <summary>敌人位移后 desired 随连线重算。</summary>
    [Test]
    public void BuildDesired_FollowsMovingEnemy()
    {
        ActionMotionAdhesion.TryBuildDesiredMm(
            0, 0, 0, 2000, 1000, 0, out int z1x, out int z1z);
        ActionMotionAdhesion.TryBuildDesiredMm(
            0, 0, 1000, 2000, 1000, 0, out int z2x, out int z2z);
        Assert.That(z1x, Is.EqualTo(0));
        Assert.That(z1z, Is.EqualTo(3000));
        Assert.That(z2x == z1x && z2z == z1z, Is.False);
        Assert.That(z2x, Is.GreaterThan(0));
    }

    /// <summary>剩余帧均摊：首帧约为误差的 1/N。</summary>
    [Test]
    public void Correction_AmortizesOverRemainingFrames()
    {
        ActionMotionAdhesionParams window = CreateAdhesionWindow(
            start: 0,
            end: 9,
            horizontalOffsetMm: 0,
            maxCorrectionMmPerFrame: 100000);

        // 玩家在 0，敌人在 +Z 2000，吸敌心 → errorZ=2000，10 帧 → ~200
        Assert.That(
            ActionMotionAdhesion.TryComputeCorrectionMm(
                actorXMm: 0,
                actorZMm: 0,
                actorYawDegrees: 0f,
                targetXMm: 0,
                targetZMm: 2000,
                in window,
                currentFrame: 0,
                out int cx,
                out int cz),
            Is.True);
        Assert.That(cx, Is.EqualTo(0));
        Assert.That(cz, Is.EqualTo(200));
    }

    /// <summary>窗外不产生修正。</summary>
    [Test]
    public void Correction_OutsideWindow_IsZero()
    {
        ActionMotionAdhesionParams window = CreateAdhesionWindow(
            start: 5,
            end: 10,
            horizontalOffsetMm: 0,
            maxCorrectionMmPerFrame: 100000);

        Assert.That(
            ActionMotionAdhesion.TryComputeCorrectionMm(
                0, 0, 0f, 0, 2000, in window, currentFrame: 4, out _, out _),
            Is.False);
    }

    /// <summary>超距不吸。</summary>
    [Test]
    public void Correction_BeyondAcquireDistance_IsZero()
    {
        ActionMotionAdhesionParams window = CreateAdhesionWindow(
            start: 0,
            end: 9,
            horizontalOffsetMm: 0,
            maxCorrectionMmPerFrame: 100000,
            maxAcquireDistanceMm: 1000);

        Assert.That(
            ActionMotionAdhesion.TryComputeCorrectionMm(
                0, 0, 0f, 0, 2500, in window, currentFrame: 0, out _, out _),
            Is.False);
    }

    /// <summary>触顶不超过 maxCorrection。</summary>
    [Test]
    public void Correction_ClampsToMaxPerFrame()
    {
        ActionMotionAdhesionParams window = CreateAdhesionWindow(
            start: 0,
            end: 0,
            horizontalOffsetMm: 0,
            maxCorrectionMmPerFrame: 100);

        Assert.That(
            ActionMotionAdhesion.TryComputeCorrectionMm(
                0, 0, 0f, 0, 2000, in window, currentFrame: 0, out int cx, out int cz),
            Is.True);
        Assert.That(cx, Is.EqualTo(0));
        Assert.That(cz, Is.EqualTo(100));
    }

    /// <summary>同输入两次结果一致。</summary>
    [Test]
    public void Correction_IsDeterministic()
    {
        ActionMotionAdhesionParams window = CreateAdhesionWindow(
            start: 0,
            end: 7,
            horizontalOffsetMm: 800,
            maxCorrectionMmPerFrame: 400);

        ActionMotionAdhesion.TryComputeCorrectionMm(
            100, -50, 15f, 900, 1200, in window, 2, out int ax, out int az);
        ActionMotionAdhesion.TryComputeCorrectionMm(
            100, -50, 15f, 900, 1200, in window, 2, out int bx, out int bz);
        Assert.That(ax, Is.EqualTo(bx));
        Assert.That(az, Is.EqualTo(bz));
    }

    /// <summary>方案 A：过冲后 desired 落到朝向后方时不倒拖。</summary>
    [Test]
    public void Correction_PastDesired_DoesNotPullBack()
    {
        // 敌人 +Z 2000；演员已冲到 Z=3500（朝向 +Z），缺口在身后
        ActionMotionAdhesionParams window = CreateAdhesionWindow(
            start: 0,
            end: 7,
            horizontalOffsetMm: 1000,
            maxCorrectionMmPerFrame: 100000);

        Assert.That(
            ActionMotionAdhesion.TryComputeCorrectionMm(
                actorXMm: 0,
                actorZMm: 3500,
                actorYawDegrees: 0f,
                targetXMm: 0,
                targetZMm: 2000,
                in window,
                currentFrame: 6,
                out int cx,
                out int cz),
            Is.False);
        Assert.That(cx, Is.EqualTo(0));
        Assert.That(cz, Is.EqualTo(0));
    }

    /// <summary>未过冲时仍补朝向前方缺口（正修正）。</summary>
    [Test]
    public void Correction_BeforeDesired_FillsGapForward()
    {
        ActionMotionAdhesionParams window = CreateAdhesionWindow(
            start: 0,
            end: 3,
            horizontalOffsetMm: 1000,
            maxCorrectionMmPerFrame: 100000);

        // desired Z=3000，演员在 0、朝向 +Z → forwardGap 3000 / 4 帧
        Assert.That(
            ActionMotionAdhesion.TryComputeCorrectionMm(
                0, 0, 0f, 0, 2000, in window, currentFrame: 0, out int cx, out int cz),
            Is.True);
        Assert.That(cx, Is.EqualTo(0));
        Assert.That(cz, Is.EqualTo(750));
    }

    /// <summary>构造吸附窗参数（Simulation 可测，无 Timeline 类型）。</summary>
    static ActionMotionAdhesionParams CreateAdhesionWindow(
        int start,
        int end,
        int horizontalOffsetMm,
        int maxCorrectionMmPerFrame,
        int maxAcquireDistanceMm = 100000)
    {
        return new ActionMotionAdhesionParams(
            start,
            end,
            horizontalOffsetMm,
            lateralOffsetMm: 0,
            maxCorrectionMmPerFrame,
            maxAcquireDistanceMm,
            maxAngleMilliDeg: 0);
    }
}
