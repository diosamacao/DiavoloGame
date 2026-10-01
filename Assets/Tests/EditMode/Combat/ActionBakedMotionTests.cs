using NUnit.Framework;

/// <summary>验证运动表就绪、端点投影、推进回撤以及毫米量化后的残差闭合。</summary>
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

    /// <summary>斜向净位移进入逻辑根，偏离端点轴的位移留给模型。</summary>
    [Test]
    public void TryGetDelta_EndpointSignedProjectsOntoDiagonal()
    {
        var motion = new ActionBakedMotion
        {
            logicHz = 60,
            frameCount = 2,
            planarMode = ActionMotionPlanarMode.EndpointSigned,
            positionDeltaMmX = new[] { 100, 0 },
            positionDeltaMmZ = new[] { 0, 100 },
            yawDeltaMilliDeg = new[] { 0, 0 },
            bakeStatus = ActionBakedMotionStatus.Ok,
        };

        Assert.That(motion.TryGetDelta(0, out SimVec2 frame0, out _), Is.True);
        Assert.That(frame0.X, Is.EqualTo(50));
        Assert.That(frame0.Z, Is.EqualTo(50));

        Assert.That(motion.TryGetDelta(1, out SimVec2 frame1, out _), Is.True);
        Assert.That(frame1.X, Is.EqualTo(50));
        Assert.That(frame1.Z, Is.EqualTo(50));
    }

    /// <summary>斜向轴残差包含 XZ，完整动作结束时自然归零。</summary>
    [Test]
    public void TryGetVisualResidual_EndpointSignedClosesAtEnd()
    {
        var motion = new ActionBakedMotion
        {
            logicHz = 60,
            frameCount = 2,
            planarMode = ActionMotionPlanarMode.EndpointSigned,
            positionDeltaMmX = new[] { 100, 0 },
            positionDeltaMmZ = new[] { 0, 100 },
            yawDeltaMilliDeg = new[] { 0, 0 },
            bakeStatus = ActionBakedMotionStatus.Ok,
        };

        Assert.That(motion.TryGetVisualResidualMm(0, out int r0x, out int r0z), Is.True);
        Assert.That(r0x, Is.EqualTo(50));
        Assert.That(r0z, Is.EqualTo(-50));

        Assert.That(motion.TryGetVisualResidualMm(1, out int r1x, out int r1z), Is.True);
        Assert.That(r1x, Is.EqualTo(0));
        Assert.That(r1z, Is.EqualTo(0));

        // FullPlanar 残差应为 0
        motion.planarMode = ActionMotionPlanarMode.FullPlanar;
        Assert.That(motion.TryGetVisualResidualMm(1, out int fullRx, out int fullRz), Is.True);
        Assert.That(fullRx, Is.EqualTo(0));
        Assert.That(fullRz, Is.EqualTo(0));
    }

    [Test]
    public void EndpointSigned_PreservesBackwardAndOvershootTiming()
    {
        var motion = EndpointMotion(new[] { -50, 200, -50 }, new[] { -50, 200, -50 });
        for (int frame = 0; frame < 3; frame++)
        {
            motion.TryGetDelta(frame, out SimVec2 delta, out _);
            Assert.That(delta.X, Is.EqualTo(motion.positionDeltaMmX[frame]));
            Assert.That(delta.Z, Is.EqualTo(motion.positionDeltaMmZ[frame]));
        }
    }

    [TestCase(0, 100)]
    [TestCase(100, 0)]
    [TestCase(-100, -100)]
    public void EndpointSigned_PreservesNetEndpoint(int endX, int endZ)
    {
        var motion = EndpointMotion(new[] { 70, endX - 70 }, new[] { -30, endZ + 30 });
        motion.TryGetDelta(0, out SimVec2 first, out _);
        motion.TryGetDelta(1, out SimVec2 last, out _);
        Assert.That(first.X + last.X, Is.EqualTo(endX));
        Assert.That(first.Z + last.Z, Is.EqualTo(endZ));
        motion.TryGetVisualResidualMm(99, out int rx, out int rz);
        Assert.That(rx, Is.Zero);
        Assert.That(rz, Is.Zero);
    }

    [Test]
    public void EndpointSigned_ClosedPathKeepsAllMotionInVisualResidual()
    {
        var motion = EndpointMotion(new[] { 100, -100 }, new[] { 200, -200 });
        motion.TryGetDelta(0, out SimVec2 first, out _);
        motion.TryGetDelta(1, out SimVec2 last, out _);
        Assert.That(first.X, Is.Zero);
        Assert.That(first.Z, Is.Zero);
        Assert.That(last.X, Is.Zero);
        Assert.That(last.Z, Is.Zero);
        motion.TryGetVisualResidualMm(0, out int rx, out int rz);
        Assert.That(rx, Is.EqualTo(100));
        Assert.That(rz, Is.EqualTo(200));
        motion.TryGetVisualResidualMm(1, out rx, out rz);
        Assert.That(rx, Is.Zero);
        Assert.That(rz, Is.Zero);
    }

    [Test]
    public void EndpointSigned_CumulativeQuantizationReconstructsEveryFrameWithoutEndpointDrift()
    {
        var dx = new int[101];
        var dz = new int[101];
        for (int i = 0; i < dx.Length; i++) dx[i] = 1;
        dz[100] = 37;
        var motion = EndpointMotion(dx, dz);
        int fullX = 0, fullZ = 0, gameX = 0, gameZ = 0;
        for (int i = 0; i < dx.Length; i++)
        {
            motion.TryGetDelta(i, out SimVec2 delta, out _);
            gameX += delta.X;
            gameZ += delta.Z;
            fullX += dx[i];
            fullZ += dz[i];
            motion.TryGetVisualResidualMm(i, out int rx, out int rz);
            Assert.That(gameX + rx, Is.EqualTo(fullX));
            Assert.That(gameZ + rz, Is.EqualTo(fullZ));
            // 累计点到端点轴的误差受每个坐标轴的半毫米取整误差约束。
            Assert.That(System.Math.Abs(gameX * 37L - gameZ * 101L), Is.LessThanOrEqualTo(69));
        }
        Assert.That(gameX, Is.EqualTo(101));
        Assert.That(gameZ, Is.EqualTo(37));
        motion.TryGetDelta(0, out SimVec2 startAgain, out _);
        motion.TryGetDelta(-10, out SimVec2 clampedStart, out _);
        Assert.That(startAgain.X, Is.EqualTo(clampedStart.X));
        Assert.That(startAgain.Z, Is.EqualTo(clampedStart.Z));
    }

    static ActionBakedMotion EndpointMotion(int[] dx, int[] dz) => new()
    {
        frameCount = dx.Length,
        planarMode = ActionMotionPlanarMode.EndpointSigned,
        positionDeltaMmX = dx,
        positionDeltaMmZ = dz,
        yawDeltaMilliDeg = new int[dx.Length],
        bakeStatus = ActionBakedMotionStatus.Ok,
    };

    /// <summary>未烘焙表不得查表。</summary>
    [Test]
    public void TryGetDelta_RejectsNoneStatus()
    {
        var motion = ActionBakedMotion.CreateEmpty();
        Assert.That(motion.TryGetDelta(0, out _, out _), Is.False);
    }

    /// <summary>显式模式互斥：Baked 就绪走表；无 RM 回退。</summary>
    [Test]
    public void RuntimePolicy_Resolve_NeverReturnsAnimatorRootMotion()
    {
        Assert.That(
            ActionMotionRuntimePolicy.Resolve(
                ActionBaseMotionMode.BakedMotion,
                bakedMotionReady: true,
                hasScriptedMovement: false),
            Is.EqualTo(ActionDisplacementSource.BakedMotion));
        Assert.That(
            ActionMotionRuntimePolicy.Resolve(
                ActionBaseMotionMode.BakedMotion,
                bakedMotionReady: false,
                hasScriptedMovement: true),
            Is.EqualTo(ActionDisplacementSource.None));
        Assert.That(
            ActionMotionRuntimePolicy.Resolve(
                ActionBaseMotionMode.ScriptedTimeline,
                bakedMotionReady: false,
                hasScriptedMovement: true),
            Is.EqualTo(ActionDisplacementSource.ScriptedTimeline));
        // 旧 LegacyResolve=0 → None
        Assert.That(
            ActionMotionRuntimePolicy.Resolve(
                (ActionBaseMotionMode)0,
                bakedMotionReady: true,
                hasScriptedMovement: false),
            Is.EqualTo(ActionDisplacementSource.None));
    }
}
