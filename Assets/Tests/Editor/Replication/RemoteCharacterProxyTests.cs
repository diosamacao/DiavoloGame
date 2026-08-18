using System.Collections.Generic;
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

    /// <summary>快照写入只读索敌身份；OnHit 不改血。</summary>
    [Test]
    public void ApplySnapshot_BindsReadOnlyTargetable_OnHitDoesNotChangeHealth()
    {
        var root = new GameObject("GhostTargetRoot");
        var presentation = new GameObject("GhostTargetPresentation");
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

        ActorReplicationSnapshot snapshot = CreatePoseSnapshot(2000, 0);
        proxy.ApplySnapshot(in snapshot);

        Assert.That(proxy.CollectsHits, Is.False);
        Assert.That(proxy.SimulationId.Value, Is.EqualTo(1));
        Assert.That(proxy.TeamId, Is.EqualTo(1));
        Assert.That(proxy.IsAlive, Is.True);
        Assert.That(proxy.CurrentHealth, Is.EqualTo(100f).Within(0.01f));

        float healthBefore = proxy.CurrentHealth;
        proxy.OnHit(default);
        Assert.That(proxy.CurrentHealth, Is.EqualTo(healthBefore));

        Object.DestroyImmediate(root);
    }

    /// <summary>Autonomous Targeting 能从 Proxy 花名册自动选中范围内异阵营目标。</summary>
    [Test]
    public void TargetingState_AcquiresProxyInRange()
    {
        var root = new GameObject("GhostAcquireRoot");
        var presentation = new GameObject("GhostAcquirePresentation");
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
        ActorReplicationSnapshot snapshot = CreatePoseSnapshot(2000, 0);
        proxy.ApplySnapshot(in snapshot);

        var roster = new List<IHurtboxTarget> { proxy };
        var targeting = new CharacterTargetingState(
            teamId: 0,
            acquireRangeMm: 10000,
            retainRangeMm: 10000,
            () => roster);
        var requesterMotor = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, radiusMm: 280);
        InputFrame input = InputFrame.Empty(0, new SimActorId(99));
        targeting.Step(new SimActorId(99), requesterMotor, in input);

        Assert.That(targeting.Snapshot.HasSelectedTarget, Is.True);
        Assert.That(targeting.Snapshot.SelectedTargetId.Value, Is.EqualTo(1));
        Assert.That(targeting.TryGetSelectedTarget(out ITargetable selected), Is.True);
        Assert.That(selected, Is.SameAs(proxy));

        Object.DestroyImmediate(root);
    }

    /// <summary>他人/敌人幽灵与工厂源码不得引用 Hitbox 收集或 EnemyBrain。</summary>
    [Test]
    public void GhostSource_HasNoHitboxCollectOrBrain()
    {
        string[] relativePaths =
        {
            "Assets/Scripts/Domain/Character/Replication/RemoteCharacterProxy.cs",
            "Assets/Scripts/App/Networking/Adapters/ActRemoteProxyFactory.cs",
            "Assets/Scripts/App/Networking/Adapters/ActObserverReplicationAdapter.cs"
        };

        for (int i = 0; i < relativePaths.Length; i++)
        {
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePaths[i]));
            Assert.That(File.Exists(fullPath), Is.True, relativePaths[i]);
            string text = File.ReadAllText(fullPath);
            Assert.That(text, Does.Not.Contain("HitboxFrameConsumer"));
            Assert.That(text, Does.Not.Contain("hitPipeline.Collect"));
            Assert.That(text, Does.Not.Contain("EnemyBrain"));
            Assert.That(text, Does.Not.Contain("CharacterActorFactory"));
            if (relativePaths[i].EndsWith("ActRemoteProxyFactory.cs"))
            {
                Assert.That(text, Does.Contain("public static class ActRemoteProxyFactory"));
                Assert.That(text, Does.Not.Contain("class RemoteCharacterProxyFactory"));
            }
        }
    }

    /// <summary>本机 Runner 已删除；预测改走 CharacterActor Autonomous 座位。</summary>
    [Test]
    public void AutonomousRunners_AreDeleted()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        Assert.That(
            File.Exists(Path.Combine(root, "Assets/Scripts/Domain/Character/Replication/AutonomousActionRunner.cs")),
            Is.False);
        Assert.That(
            File.Exists(Path.Combine(root, "Assets/Scripts/Domain/Character/Replication/AutonomousLocomotionRunner.cs")),
            Is.False);
    }

    /// <summary>Host 同机 ±2m 预览已删除；验收走 ParrelSync 真客机。</summary>
    [Test]
    public void HostSameProcessPreviewControllers_AreDeleted()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        Assert.That(
            File.Exists(Path.Combine(root, "Assets/Scripts/App/Controllers/Gameplay/RemoteGhostViewController.cs")),
            Is.False);
        Assert.That(
            File.Exists(Path.Combine(root, "Assets/Scripts/App/Controllers/Gameplay/PredictedClientPreviewController.cs")),
            Is.False);
    }

    /// <summary>Hitbox 只在 Authority 分支注册，避免 Autonomous 双伤。</summary>
    [Test]
    public void FactorySource_RegistersHitboxOnlyForAuthority()
    {
        string path = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            "Assets/Scripts/Domain/Character/CharacterActorFactory.cs"));
        string text = File.ReadAllText(path);
        int seatGate = text.IndexOf(
            "if (seat == ReplicationSeat.Authority)",
            System.StringComparison.Ordinal);
        int register = text.IndexOf(
            "RegisterFrameConsumer(hitboxFrameConsumer)",
            System.StringComparison.Ordinal);
        Assert.That(seatGate, Is.GreaterThan(0));
        Assert.That(register, Is.GreaterThan(seatGate));
    }

    /// <summary>WorldQuery 在 Hitbox 门禁外创建，客机才能跑 Adhesion / Relocate。</summary>
    [Test]
    public void FactorySource_InjectsWorldQueryForBothSeats()
    {
        string path = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            "Assets/Scripts/Domain/Character/CharacterActorFactory.cs"));
        string text = File.ReadAllText(path);
        int query = text.IndexOf("new ActionMotionWorldQuery", System.StringComparison.Ordinal);
        int hitboxRegister = text.IndexOf(
            "RegisterFrameConsumer(hitboxFrameConsumer)",
            System.StringComparison.Ordinal);
        Assert.That(query, Is.GreaterThan(0));
        Assert.That(query, Is.LessThan(hitboxRegister));
        Assert.That(text, Does.Not.Contain("Autonomous 不注入 WorldQuery"));
        Assert.That(text, Does.Contain("new PredictedHitStopConsumer"));
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
