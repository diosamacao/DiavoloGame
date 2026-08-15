using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>幽灵只跟快照位姿并插值；源码路径不含命中收集。</summary>
public sealed class RemoteCharacterProxyTests
{
    /// <summary>连续两帧快照后 Render(0.5) 落在中点，不瞬移到终点。</summary>
    [Test]
    public void ApplySnapshot_ThenRenderHalf_InterpolatesPlanarPose()
    {
        var root = new GameObject("GhostRoot");
        var presentation = new GameObject("GhostPresentation");
        presentation.transform.SetParent(root.transform, false);
        CharacterController controller = root.AddComponent<CharacterController>();
        controller.enabled = false;

        var motorSim = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, radiusMm: 280);
        var motor = new CharacterMotor(
            root.transform,
            controller,
            CharacterMotorConfig.Default,
            new IdleIntent(),
            motorSim);
        var bridge = new CharacterPresentationBridge(root.transform, presentation.transform);
        var proxy = new RemoteCharacterProxy(
            root.transform,
            motor,
            animation: null,
            bridge,
            new ActionReplicationCatalog(),
            Vector3.zero,
            1f / 60f);

        // in 参数必须是可寻址变量，不能直接传方法返回值
        ActorReplicationSnapshot first = CreatePoseSnapshot(0, 0);
        ActorReplicationSnapshot second = CreatePoseSnapshot(2000, 0);
        proxy.ApplySnapshot(in first);
        proxy.ApplySnapshot(in second);
        proxy.Render(0.5f);

        Assert.That(proxy.CollectsHits, Is.False);
        Assert.That(motorSim.PositionMm.X, Is.EqualTo(2000));
        Assert.That(presentation.transform.position.x, Is.EqualTo(1f).Within(0.02f));

        Object.DestroyImmediate(root);
    }

    /// <summary>有招快照后 IsPresentingAction，供相机暂停跟朝向。</summary>
    [Test]
    public void ApplySnapshot_WithAction_MarksPresentingAction()
    {
        var root = new GameObject("GhostActionRoot");
        var presentation = new GameObject("GhostActionPresentation");
        presentation.transform.SetParent(root.transform, false);
        CharacterController controller = root.AddComponent<CharacterController>();
        controller.enabled = false;

        var proxy = new RemoteCharacterProxy(
            root.transform,
            new CharacterMotor(
                root.transform,
                controller,
                CharacterMotorConfig.Default,
                new IdleIntent(),
                new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, radiusMm: 280)),
            animation: null,
            new CharacterPresentationBridge(root.transform, presentation.transform),
            new ActionReplicationCatalog(),
            Vector3.zero,
            1f / 60f);

        ActorReplicationSnapshot idle = CreatePoseSnapshot(0, 0);
        proxy.ApplySnapshot(in idle);
        Assert.That(proxy.IsPresentingAction, Is.False);

        ActorReplicationSnapshot dodge = idle.WithAction(7, 3);
        proxy.ApplySnapshot(in dodge);
        Assert.That(proxy.IsPresentingAction, Is.True);

        Object.DestroyImmediate(root);
    }

    /// <summary>快照 moveV* 还原为幽灵黄箭 wish，与位姿同一 Tick。</summary>
    [Test]
    public void ApplySnapshot_CopiesWishFromMoveVelocityFields()
    {
        var root = new GameObject("GhostWishRoot");
        var presentation = new GameObject("GhostWishPresentation");
        presentation.transform.SetParent(root.transform, false);
        CharacterController controller = root.AddComponent<CharacterController>();
        controller.enabled = false;

        var motor = new CharacterMotor(
            root.transform,
            controller,
            CharacterMotorConfig.Default,
            new IdleIntent(),
            new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, radiusMm: 280));
        var proxy = new RemoteCharacterProxy(
            root.transform,
            motor,
            animation: null,
            new CharacterPresentationBridge(root.transform, presentation.transform),
            new ActionReplicationCatalog(),
            Vector3.zero,
            1f / 60f);

        ActorReplicationSnapshot snapshot = new ActorReplicationSnapshot(
            new SimActorId(1),
            1,
            ReplicationActorKind.Player,
            0,
            0,
            0,
            0,
            moveVxMm: 1000,
            moveVzMm: 0,
            0,
            0,
            0,
            0,
            string.Empty,
            0,
            0,
            SimActorId.Invalid,
            100000,
            0,
            VitalityReplicationEdge.None);
        proxy.ApplySnapshot(in snapshot);

        Assert.That(proxy.HasFacingDebugPose, Is.True);
        Assert.That(proxy.FacingDebugWishWorld.x, Is.EqualTo(1f).Within(0.02f));
        Assert.That(proxy.FacingDebugWishWorld.z, Is.EqualTo(0f).Within(0.02f));

        Object.DestroyImmediate(root);
    }

    /// <summary>预测预览传入的 lean 写到 VisualMotionRoot Roll，不改权威根朝向。</summary>
    [Test]
    public void ApplySnapshot_WithLean_WritesVisualMotionRootRoll()
    {
        var root = new GameObject("GhostLeanRoot");
        var presentation = new GameObject("GhostLeanPresentation");
        presentation.transform.SetParent(root.transform, false);
        var visual = new GameObject("GhostLeanVisual");
        visual.transform.SetParent(presentation.transform, false);
        CharacterController controller = root.AddComponent<CharacterController>();
        controller.enabled = false;

        var motor = new CharacterMotor(
            root.transform,
            controller,
            CharacterMotorConfig.Default,
            new IdleIntent(),
            new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, radiusMm: 280));
        var proxy = new RemoteCharacterProxy(
            root.transform,
            motor,
            animation: null,
            new CharacterPresentationBridge(root.transform, presentation.transform),
            new ActionReplicationCatalog(),
            Vector3.zero,
            1f / 60f,
            visual.transform);

        ActorReplicationSnapshot snapshot = CreatePoseSnapshot(0, 0);
        proxy.ApplySnapshot(in snapshot, leanRollDegrees: -8f);

        float visualRoll = visual.transform.localEulerAngles.z;
        if (visualRoll > 180f)
            visualRoll -= 360f;
        Assert.That(visualRoll, Is.EqualTo(-8f).Within(0.2f));
        Assert.That(root.transform.eulerAngles.z, Is.EqualTo(0f).Within(0.2f));

        Object.DestroyImmediate(root);
    }

    /// <summary>Proxy 只把刀光/音效当表现通知，位移与判定命令排除。</summary>
    [Test]
    public void IsPresentationNotify_OnlyVfxAndSfx()
    {
        Assert.That(RemoteCharacterProxy.IsPresentationNotify(new PlayVfxNotify()), Is.True);
        Assert.That(RemoteCharacterProxy.IsPresentationNotify(new PlaySfxNotify()), Is.True);
        Assert.That(RemoteCharacterProxy.IsPresentationNotify(new MotionCommandNotify()), Is.False);
        Assert.That(RemoteCharacterProxy.IsPresentationNotify(null), Is.False);
    }

    /// <summary>无 Catalog 时不得假装能还原受击 Feedback。</summary>
    [Test]
    public void TryResolveFeedback_NullCatalog_ReturnsFalse()
    {
        Assert.That(HitImpactCuePlayer.TryResolveFeedback(null, 1, 0, out _), Is.False);
    }

    /// <summary>连续受击：Hit 边沿或同一招动作帧回绕须重播；同招帧前进不重播。</summary>
    [Test]
    public void ShouldForceActionRestart_HitEdgeOrFrameRewind()
    {
        Assert.That(
            RemoteCharacterProxy.ShouldForceActionRestart(
                VitalityReplicationEdge.Hit, 4, 8, 4, 0),
            Is.True);
        Assert.That(
            RemoteCharacterProxy.ShouldForceActionRestart(
                VitalityReplicationEdge.Death, 4, 3, 4, 4),
            Is.True);
        Assert.That(
            RemoteCharacterProxy.ShouldForceActionRestart(
                VitalityReplicationEdge.None, 4, 8, 4, 0),
            Is.True);
        Assert.That(
            RemoteCharacterProxy.ShouldForceActionRestart(
                VitalityReplicationEdge.None, 4, 3, 4, 4),
            Is.False);
        Assert.That(
            RemoteCharacterProxy.ShouldForceActionRestart(
                VitalityReplicationEdge.None, 0, 0, 4, 0),
            Is.False);
    }

    /// <summary>幽灵与工厂源码不得引用 Hitbox 收集或 EnemyBrain。</summary>
    [Test]
    public void GhostSource_HasNoHitboxCollectOrBrain()
    {
        string[] relativePaths =
        {
            "Assets/Scripts/Domain/Character/Replication/RemoteCharacterProxy.cs",
            "Assets/Scripts/Domain/Character/Replication/RemoteCharacterProxyFactory.cs",
            "Assets/Scripts/App/Controllers/Gameplay/RemoteGhostViewController.cs",
            "Assets/Scripts/App/Controllers/Gameplay/PredictedClientPreviewController.cs",
            "Assets/Scripts/Domain/Simulation/Prediction/PredictedActionDriver.cs"
        };

        for (int i = 0; i < relativePaths.Length; i++)
        {
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePaths[i]));
            Assert.That(File.Exists(fullPath), Is.True, relativePaths[i]);
            string text = File.ReadAllText(fullPath);
            Assert.That(text, Does.Not.Contain("HitboxFrameConsumer"));
            Assert.That(text, Does.Not.Contain("hitPipeline.Collect"));
            Assert.That(text, Does.Not.Contain("EnemyBrain"));
        }
    }

    static ActorReplicationSnapshot CreatePoseSnapshot(int xMm, int zMm) =>
        new ActorReplicationSnapshot(
            new SimActorId(1),
            1,
            ReplicationActorKind.Player,
            xMm,
            zMm,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            string.Empty,
            0,
            0,
            SimActorId.Invalid,
            100000,
            0,
            VitalityReplicationEdge.None);

    sealed class IdleIntent : IMoveIntentSource
    {
        public Vector2 MoveIntent => Vector2.zero;
        public float MoveMagnitude => 0f;
        public bool HasMoveIntent => false;
        public Vector2 BufferedMoveIntent => Vector2.zero;
        public ushort MoveReferenceYawQuantized => 0;
    }
}
