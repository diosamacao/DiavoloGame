using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>锁定 Dedicated Authority 的 Guest、Step 与 Replication 三项职责边界。</summary>
public sealed class AuthorityWorldCoordinatorBoundaryTests
{
    /// <summary>Authority World 只能组合并委托三个组件，不得重新持有算法集合。</summary>
    [Test]
    public void DedicatedAuthorityWorld_IsCompositionFacade()
    {
        string source = ReadScript("App/Networking/Services/DedicatedAuthorityWorld.cs");

        Assert.That(source, Does.Contain("AuthorityGuestRegistry"));
        Assert.That(source, Does.Contain("AuthorityStepCoordinator"));
        Assert.That(source, Does.Contain("AuthorityReplicationPublisher"));
        Assert.That(source, Does.Not.Contain("Dictionary<NetConnectionId, ActGameGuest>"));
        Assert.That(source, Does.Not.Contain("ReplicationServer"));
        Assert.That(source, Does.Not.Contain("RoomRemoteInputMerge"));
        Assert.That(source, Does.Not.Contain("PrepareTickDelta("));
    }

    /// <summary>固定帧协调器必须保持 Guest 生命周期提交先于复制发布。</summary>
    [Test]
    public void AuthorityStepCoordinator_PostLogicOrder_RemainsStable()
    {
        string source = ReadScript("App/Networking/Services/AuthorityStepCoordinator.cs");
        int lifecycle = source.IndexOf("_guests.AdvancePostLogicLifecycles();");
        int publish = source.IndexOf("_publisher.PublishFrame(authorityFrame);");

        Assert.That(lifecycle, Is.GreaterThanOrEqualTo(0));
        Assert.That(publish, Is.GreaterThan(lifecycle));
    }

    /// <summary>复制发布器独占连接级基线、Prepare 与可靠事件队列。</summary>
    [Test]
    public void AuthorityReplicationPublisher_OwnsConnectionReplication()
    {
        string source = ReadScript("App/Networking/Services/AuthorityReplicationPublisher.cs");

        Assert.That(source, Does.Contain("Dictionary<NetConnectionId, ReplicationServer>"));
        Assert.That(source, Does.Contain("PrepareTickDelta("));
        Assert.That(source, Does.Contain("ActReplicationEventCodec.Encode("));
        Assert.That(source, Does.Contain("public void Commit("));
        Assert.That(source, Does.Contain("public void Reject("));
    }

    /// <summary>Guest 注册表独占 Headless 阵容创建与销毁。</summary>
    [Test]
    public void AuthorityGuestRegistry_OwnsGuestLifecycle()
    {
        string source = ReadScript("App/Networking/Services/AuthorityGuestRegistry.cs");

        Assert.That(source, Does.Contain("Dictionary<NetConnectionId, ActGameGuest>"));
        Assert.That(source, Does.Contain("TryCreateGuest("));
        Assert.That(source, Does.Contain("DestroyGuest("));
        Assert.That(source, Does.Not.Contain("PrepareTickDelta("));
    }

    /// <summary>从 Assets 相对路径读取生产脚本。</summary>
    static string ReadScript(string relativePath)
    {
        string path = Path.Combine(Application.dataPath, "Scripts", relativePath);
        Assert.That(File.Exists(path), Is.True, $"生产脚本不存在：{path}");
        return File.ReadAllText(path);
    }
}
