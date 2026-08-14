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

    /// <summary>幽灵与工厂源码不得引用 Hitbox 收集或 EnemyBrain。</summary>
    [Test]
    public void GhostSource_HasNoHitboxCollectOrBrain()
    {
        string[] relativePaths =
        {
            "Assets/Scripts/Domain/Character/Replication/RemoteCharacterProxy.cs",
            "Assets/Scripts/Domain/Character/Replication/RemoteCharacterProxyFactory.cs",
            "Assets/Scripts/App/Controllers/Gameplay/RemoteGhostViewController.cs",
            "Assets/Scripts/App/Controllers/Gameplay/PredictedClientPreviewController.cs"
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
