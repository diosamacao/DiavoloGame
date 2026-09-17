using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>锁定 Client Gameplay 的 Owner、Observer 与反馈三席位唯一协调器。</summary>
public sealed class ClientGameplayCoordinatorBoundaryTests
{
    /// <summary>Room Gameplay 只能装配并委托三个协调器，不得重新持有具体算法集合。</summary>
    [Test]
    public void ActClientRoomGameplay_IsCompositionFacade()
    {
        string source = ReadScript("App/Networking/Services/ActClientRoomGameplay.cs");

        Assert.That(source, Does.Contain("OwnerPredictionCoordinator"));
        Assert.That(source, Does.Contain("ObserverReplicationCoordinator"));
        Assert.That(source, Does.Contain("ReplicatedFeedbackCoordinator"));
        Assert.That(source, Does.Not.Contain("ReplicationClient"));
        Assert.That(source, Does.Not.Contain("List<ClientCommand>"));
        Assert.That(source, Does.Not.Contain("OwnerAssistParryEventQueue"));
        Assert.That(source, Does.Not.Contain("AutonomousSoftBodySolver"));
    }

    /// <summary>Owner 协调器独占命令冗余、预测推进和 Party Meta 纠正。</summary>
    [Test]
    public void OwnerPredictionCoordinator_OwnsOwnerPath()
    {
        string source = ReadScript("App/Networking/Services/OwnerPredictionCoordinator.cs");

        Assert.That(source, Does.Contain("List<ClientCommand>"));
        Assert.That(source, Does.Contain("public bool TryBuildCommand("));
        Assert.That(source, Does.Contain("public void StepPrediction("));
        Assert.That(source, Does.Contain("public void ApplyAuthorityMeta("));
        Assert.That(source, Does.Contain("_owner.ApplySnapshot("));
    }

    /// <summary>Observer 协调器独占 V2 生命周期、Snapshot、Meta 原子提交和播放时钟。</summary>
    [Test]
    public void ObserverReplicationCoordinator_OwnsReplicationPath()
    {
        string source = ReadScript("App/Networking/Services/ObserverReplicationCoordinator.cs");
        int meta = source.IndexOf("_owner.ApplyAuthorityMeta(_appliedMeta);");
        int updates = source.IndexOf("_observer.ApplyUpdates(");
        int owner = source.IndexOf("_owner.ApplySelfSnapshot(");

        Assert.That(source, Does.Contain("new ReplicationClient("));
        Assert.That(source, Does.Contain("public ActClientReplicationApplyStatus ApplyLifecycle("));
        Assert.That(source, Does.Contain("public ActClientReplicationApplyStatus ApplySnapshot("));
        Assert.That(meta, Is.GreaterThanOrEqualTo(0));
        Assert.That(updates, Is.GreaterThan(meta));
        Assert.That(owner, Is.GreaterThan(updates));
    }

    /// <summary>反馈协调器独占命中去重、弹刀竞态与本机软体分离。</summary>
    [Test]
    public void ReplicatedFeedbackCoordinator_OwnsFeedbackPath()
    {
        string source = ReadScript("App/Networking/Services/ReplicatedFeedbackCoordinator.cs");

        Assert.That(source, Does.Contain("HashSet<SimHitKey>"));
        Assert.That(source, Does.Contain("OwnerAssistParryEventQueue"));
        Assert.That(source, Does.Contain("HitImpactCuePlayer.TryPlay("));
        Assert.That(source, Does.Contain("AutonomousSoftBodySolver.TrySeparateLocal("));
    }

    /// <summary>Recovery 必须先释放 Proxy，再重置 Owner/协议，最后清理跨通道反馈。</summary>
    [Test]
    public void Recovery_Order_RemainsStable()
    {
        string source = ReadScript("App/Networking/Services/ActClientRoomGameplay.cs");
        int disposeViews = source.IndexOf("_observer.DisposeViewsForRecovery();");
        int resetOwner = source.IndexOf("_owner.ResetForRecovery();");
        int resetClient = source.IndexOf("_observer.ResetClientForRecovery();");
        int resetFeedback = source.IndexOf("_feedback.ResetForRecovery();");

        Assert.That(disposeViews, Is.GreaterThanOrEqualTo(0));
        Assert.That(resetOwner, Is.GreaterThan(disposeViews));
        Assert.That(resetClient, Is.GreaterThan(resetOwner));
        Assert.That(resetFeedback, Is.GreaterThan(resetClient));
    }

    /// <summary>从 Assets 相对路径读取生产脚本。</summary>
    static string ReadScript(string relativePath)
    {
        string path = Path.Combine(Application.dataPath, "Scripts", relativePath);
        Assert.That(File.Exists(path), Is.True, $"生产脚本不存在：{path}");
        return File.ReadAllText(path);
    }
}
