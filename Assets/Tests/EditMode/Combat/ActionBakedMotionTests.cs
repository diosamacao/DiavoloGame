using NUnit.Framework;

/// <summary>验证运动表就绪判定、越界钳制与 ForwardOnly 投影。</summary>
public sealed class ActionBakedMotionTests
{
    /// <summary>Ok 且数组长度对齐时可以查表。</summary>
    [Test]
    public void TryGetDelta_ReturnsFrameValues()
    {
        var motion = new ActionBakedMotion
        {
            logicHz = 60,
            frameCount = 2,
            planarMode = ActionMotionPlanarMode.FullPlanar,
            positionDeltaMmX = new[] { 10, 20 },
            positionDeltaMmZ = new[] { 30, 40 },
            yawDeltaMilliDeg = new[] { 0, 1000 },
            bakeStatus = ActionBakedMotionStatus.Ok,
        };

        Assert.That(motion.TryGetDelta(1, out SimVec2 delta, out int yaw), Is.True);
        Assert.That(delta.X, Is.EqualTo(20));
        Assert.That(delta.Z, Is.EqualTo(40));
        // 朝向不由运动表驱动，即使数组残留非零也对外返回 0
        Assert.That(yaw, Is.EqualTo(0));
    }

    /// <summary>越界帧钳到最后一帧，避免 Cancel 后读空。</summary>
    [Test]
    public void TryGetDelta_ClampsPastEnd()
    {
        var motion = new ActionBakedMotion
        {
            logicHz = 60,
            frameCount = 1,
            positionDeltaMmX = new[] { 5 },
            positionDeltaMmZ = new[] { 7 },
            yawDeltaMilliDeg = new[] { 0 },
            bakeStatus = ActionBakedMotionStatus.Ok,
        };

        Assert.That(motion.TryGetDelta(99, out SimVec2 delta, out _), Is.True);
        Assert.That(delta.X, Is.EqualTo(5));
        Assert.That(delta.Z, Is.EqualTo(7));
    }

    /// <summary>ForwardOnly 把水平模长投到 +Z，清零横向（旧语义保持不变）。</summary>
    [Test]
    public void TryGetDelta_ForwardOnlyProjectsToZ()
    {
        var motion = new ActionBakedMotion
        {
            logicHz = 60,
            frameCount = 1,
            planarMode = ActionMotionPlanarMode.ForwardOnly,
            positionDeltaMmX = new[] { 30 },
            positionDeltaMmZ = new[] { 40 },
            yawDeltaMilliDeg = new[] { 0 },
            bakeStatus = ActionBakedMotionStatus.Ok,
        };

        Assert.That(motion.TryGetDelta(0, out SimVec2 delta, out _), Is.True);
        Assert.That(delta.X, Is.EqualTo(0));
        Assert.That(delta.Z, Is.EqualTo(50));
    }

    /// <summary>ForwardSigned：纯横摆不产生前进，只保留原始 dz。</summary>
    [Test]
    public void TryGetDelta_ForwardSignedDropsXKeepsZ()
    {
        var motion = new ActionBakedMotion
        {
            logicHz = 60,
            frameCount = 2,
            planarMode = ActionMotionPlanarMode.ForwardSigned,
            positionDeltaMmX = new[] { 100, 20 },
            positionDeltaMmZ = new[] { 0, 40 },
            yawDeltaMilliDeg = new[] { 0, 0 },
            bakeStatus = ActionBakedMotionStatus.Ok,
        };

        Assert.That(motion.TryGetDelta(0, out SimVec2 frame0, out _), Is.True);
        Assert.That(frame0.X, Is.EqualTo(0));
        Assert.That(frame0.Z, Is.EqualTo(0));

        Assert.That(motion.TryGetDelta(1, out SimVec2 frame1, out _), Is.True);
        Assert.That(frame1.X, Is.EqualTo(0));
        Assert.That(frame1.Z, Is.EqualTo(40));
    }

    /// <summary>Wave2：ForwardSigned 残差吸收横向，Gameplay+Residual 重建 Full。</summary>
    [Test]
    public void TryGetVisualResidual_ForwardSignedHoldsLateral()
    {
        var motion = new ActionBakedMotion
        {
            logicHz = 60,
            frameCount = 2,
            planarMode = ActionMotionPlanarMode.ForwardSigned,
            positionDeltaMmX = new[] { 100, 20 },
            positionDeltaMmZ = new[] { 0, 40 },
            yawDeltaMilliDeg = new[] { 0, 0 },
            bakeStatus = ActionBakedMotionStatus.Ok,
        };

        Assert.That(motion.TryGetVisualResidualMm(0, out int r0x, out int r0z), Is.True);
        Assert.That(r0x, Is.EqualTo(100));
        Assert.That(r0z, Is.EqualTo(0));

        Assert.That(motion.TryGetVisualResidualMm(1, out int r1x, out int r1z), Is.True);
        Assert.That(r1x, Is.EqualTo(120));
        Assert.That(r1z, Is.EqualTo(0));

        // FullPlanar 残差应为 0
        motion.planarMode = ActionMotionPlanarMode.FullPlanar;
        Assert.That(motion.TryGetVisualResidualMm(1, out int fullRx, out int fullRz), Is.True);
        Assert.That(fullRx, Is.EqualTo(0));
        Assert.That(fullRz, Is.EqualTo(0));
    }

    /// <summary>未烘焙表不得查表。</summary>
    [Test]
    public void TryGetDelta_RejectsNoneStatus()
    {
        var motion = ActionBakedMotion.CreateEmpty();
        Assert.That(motion.TryGetDelta(0, out _, out _), Is.False);
    }

    /// <summary>表就绪后禁止 Animator RM，避免双倍位移。</summary>
    [Test]
    public void RuntimePolicy_BakedDisablesAnimatorRootMotion()
    {
        Assert.That(ActionMotionRuntimePolicy.ShouldUseBakedMotion(true), Is.True);
        Assert.That(
            ActionMotionRuntimePolicy.ShouldUseAnimatorRootMotion(useRootMotionPolicy: true, bakedMotionReady: true),
            Is.False);
        Assert.That(
            ActionMotionRuntimePolicy.ShouldUseAnimatorRootMotion(useRootMotionPolicy: true, bakedMotionReady: false),
            Is.True);
    }
}
