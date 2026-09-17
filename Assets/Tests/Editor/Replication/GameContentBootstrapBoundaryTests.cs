using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>锁定 CS5 内容单入口、旧扫描删除与运行时 Catalog 冻结边界。</summary>
public sealed class GameContentBootstrapBoundaryTests
{
    /// <summary>旧 Registry、Server Probe 与 Client Prefill 三条入口必须同步删除。</summary>
    [Test]
    public void LegacyContentEntryPoints_AreDeleted()
    {
        Assert.That(File.Exists(ScriptPath("App/Networking/ActContentRegistry.cs")), Is.False);
        Assert.That(
            File.Exists(ScriptPath("App/Networking/Content/ActServerContentProbe.cs")),
            Is.False);
        Assert.That(
            File.Exists(ScriptPath("App/Networking/Services/ActContentPrefillService.cs")),
            Is.False);
    }

    /// <summary>CombatWorld 单次 Build 后把同一 Catalog 注入 Authority 与 Client 组合链。</summary>
    [Test]
    public void CombatWorld_OwnsSingleContentBuild()
    {
        string world = ReadScript("App/Controllers/Combat/CombatWorldController.cs");
        string clientGameplay =
            ReadScript("App/Networking/Services/ActClientRoomGameplay.cs");

        int startIndex = world.IndexOf("void Start()", System.StringComparison.Ordinal);
        int buildIndex = world.IndexOf(
            "GameContentBootstrap.ValidateAndBuild(this)",
            System.StringComparison.Ordinal);
        Assert.That(startIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(buildIndex, Is.GreaterThan(startIndex));
        Assert.That(world, Does.Contain("GameContentBootstrap.ValidateAndBuild(this)"));
        Assert.That(world, Does.Contain("EnsureDedicatedBootstrap(_contentCatalog)"));
        Assert.That(world, Does.Contain("EnsureListenBootstrap(_contentCatalog)"));
        Assert.That(world, Does.Contain("client.Configure(this, TryCreateClientSession(sessionConfig), _contentCatalog)"));
        Assert.That(clientGameplay, Does.Not.Contain("new GameContentCatalog"));
        Assert.That(clientGameplay, Does.Not.Contain("InitializeFromScene"));
    }

    /// <summary>生产 Capture 与 Join 只能查询冻结 Id，禁止再次登记内容。</summary>
    [Test]
    public void RuntimeReplication_DoesNotMutateContentCatalog()
    {
        string schema = ReadScript("App/Networking/Schema/ActCharacterSnapshotSchema.cs");
        string owner = ReadScript("App/Networking/Adapters/ActOwnerReplicationAdapter.cs");
        string authority =
            ReadScript("App/Networking/Adapters/ActAuthorityReplicationAdapter.cs");
        string session = ReadScript("App/Networking/Adapters/ActGameSessionHandler.cs");

        Assert.That(schema, Does.Contain("Actions.RequireId"));
        Assert.That(owner, Does.Contain("Actions.RequireId"));
        Assert.That(authority, Does.Contain("GetArchetypeId(definition)"));
        Assert.That(session, Does.Contain("GetArchetypeId(config)"));
        Assert.That(schema + owner + authority + session, Does.Not.Contain(".GetOrAdd("));
        Assert.That(authority + session, Does.Not.Contain(".PrefillActions("));
    }

    /// <summary>Build 必须覆盖 Action、Graph、Timing 与 RootMotion，不能只登记网络 Id。</summary>
    [Test]
    public void ContentBuild_InvokesDeepGameplayValidation()
    {
        string bootstrap =
            ReadScript("App/Networking/Content/GameContentBootstrap.cs");
        string combatModes =
            ReadScript("Domain/Combat/CombatModeProfile.cs");
        string locomotion =
            ReadScript("Domain/Character/Locomotion/CharacterLocomotionProfile.cs");

        Assert.That(bootstrap, Does.Contain("ValidateContent(context)"));
        Assert.That(combatModes, Does.Contain("ValidateGraphActions"));
        Assert.That(locomotion, Does.Contain("ValidateRequiredTiming"));
        Assert.That(locomotion, Does.Contain("GetRootMotionTrack(key).IsValid"));
    }

    /// <summary>从 Assets 相对路径读取生产脚本。</summary>
    static string ReadScript(string relativePath) =>
        File.ReadAllText(ScriptPath(relativePath));

    /// <summary>构造 Assets/Scripts 下的绝对脚本路径。</summary>
    static string ScriptPath(string relativePath) =>
        Path.Combine(Application.dataPath, "Scripts", relativePath);
}
