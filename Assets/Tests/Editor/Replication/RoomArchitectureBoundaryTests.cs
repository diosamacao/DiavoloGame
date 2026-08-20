using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>架构边界守卫：冻结薄 Facade、纯 ACTNet 与 Authority/Owner/Observer 单向映射。</summary>
public sealed class RoomArchitectureBoundaryTests
{
    static readonly string[] RoomForbiddenGameplaySymbols =
    {
        "CharacterConfig",
        "PlayerController",
        "EnemySpawnController",
        "RemoteCharacterProxy",
        "CharacterActor",
        "HitImpactCuePlayer",
        "HitStopController",
        "FindObjectsOfType",
        "ActContentRegistry",
        "ActCharacterSnapshotSchema",
    };

    /// <summary>旧 Listen Host Room 与特殊 Host Gameplay 必须删除，禁止双轨。</summary>
    [Test]
    public void DeletedHostPath_IsRemoved()
    {
        Assert.That(
            File.Exists(ScriptPath("App/Controllers/Gameplay/ReplicationRoomHost.cs")),
            Is.False);
        Assert.That(
            File.Exists(ScriptPath("App/Networking/Services/ActHostRoomGameplay.cs")),
            Is.False);
    }

    /// <summary>Listen / Client Facade 只能调 Session 与 Runtime Facade，不再实现具体 Gameplay。</summary>
    [Test]
    public void RoomFacades_DoNotContainGameplayImplementationTypes()
    {
        string listen = ReadScript("App/Controllers/Gameplay/ListenServerBootstrap.cs");
        string client = ReadScript("App/Controllers/Gameplay/ReplicationRoomClient.cs");
        string local = ReadScript("App/Networking/Services/LocalClientRuntime.cs");

        for (int i = 0; i < RoomForbiddenGameplaySymbols.Length; i++)
        {
            Assert.That(listen, Does.Not.Contain(RoomForbiddenGameplaySymbols[i]));
            Assert.That(client, Does.Not.Contain(RoomForbiddenGameplaySymbols[i]));
        }

        Assert.That(listen, Does.Contain("DedicatedServerRuntime"));
        Assert.That(listen, Does.Contain("LocalClientRuntime"));
        Assert.That(client, Does.Contain("LocalClientRuntime"));
        Assert.That(local, Does.Contain("ActClientRoomGameplay"));
    }

    /// <summary>Observer 只能创建 Remote Proxy，不得回流 CharacterActor 或权威 Hitbox Consumer。</summary>
    [Test]
    public void ObserverPath_DoesNotCreateAuthorityActorOrHitboxConsumer()
    {
        string observer = ReadScript("App/Networking/Adapters/ActObserverReplicationAdapter.cs");
        string factory = ReadScript("App/Networking/Adapters/ActRemoteProxyFactory.cs");
        string combined = observer + factory;

        Assert.That(combined, Does.Not.Contain("CharacterActorFactory"));
        Assert.That(combined, Does.Not.Contain("new CharacterActor("));
        Assert.That(combined, Does.Not.Contain("HitboxFrameConsumer"));
        Assert.That(combined, Does.Not.Contain("RegisterFrameConsumer"));
    }

    /// <summary>Owner 预测入口不得注册权威 Hitbox Consumer；命中仍由 Authority 独占。</summary>
    [Test]
    public void OwnerPath_DoesNotRegisterAuthorityHitboxConsumer()
    {
        string room = ReadScript("App/Controllers/Gameplay/ReplicationRoomClient.cs");
        string local = ReadScript("App/Networking/Services/LocalClientRuntime.cs");
        string gameplay = ReadScript("App/Networking/Services/ActClientRoomGameplay.cs");
        string owner = ReadScript("App/Networking/Adapters/ActOwnerReplicationAdapter.cs");
        string combined = room + local + gameplay + owner;

        Assert.That(combined, Does.Not.Contain("HitboxFrameConsumer"));
        Assert.That(combined, Does.Not.Contain("RegisterFrameConsumer"));
    }

    /// <summary>ACTNet 五层保持零 Unity 与零 ACT Gameplay 反向依赖。</summary>
    [Test]
    public void ActNetSources_DoNotReferenceUnityOrActGameplayTypes()
    {
        string root = Path.Combine(Application.dataPath, "Scripts", "Framework", "ACTNet");
        string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
        string[] forbidden =
        {
            "UnityEngine",
            "CharacterActor",
            "ActionDefinition",
            "CharacterConfig",
            "CombatHitPipeline",
            "RemoteCharacterProxy",
        };

        Assert.That(files.Length, Is.GreaterThan(0));
        for (int f = 0; f < files.Length; f++)
        {
            string source = File.ReadAllText(files[f]);
            for (int i = 0; i < forbidden.Length; i++)
                Assert.That(source, Does.Not.Contain(forbidden[i]), files[f]);
        }
    }

    /// <summary>Dedicated Server 程序集不得引用客户端 HUD / Input / Camera / 本机玩家 / Listen Facade。</summary>
    [Test]
    public void DedicatedServerSources_DoNotReferenceClientPresentationTypes()
    {
        string root = Path.Combine(Application.dataPath, "Scripts", "App", "Server");
        string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
        string[] forbidden =
        {
            "PlayerController",
            "InputReader",
            "CameraManager",
            "CombatDebugHud",
            "FeedbackController",
            "ReplicationRoomHost",
            "ReplicationRoomClient",
            "ListenServerBootstrap",
            "LocalClientRuntime",
            "ActClientRoomGameplay",
        };

        Assert.That(files.Length, Is.GreaterThan(0));
        for (int f = 0; f < files.Length; f++)
        {
            string source = File.ReadAllText(files[f]);
            for (int i = 0; i < forbidden.Length; i++)
                Assert.That(source, Does.Not.Contain(forbidden[i]), files[f]);
        }
    }

    /// <summary>Headless 工厂必须走 Null 播放后端，且不得在该分支实例化 Model。</summary>
    [Test]
    public void CharacterActorFactory_HeadlessPath_UsesNullPlaybackWithoutModelSpawn()
    {
        string factory = ReadScript("Domain/Character/CharacterActorFactory.cs");
        Assert.That(factory, Does.Contain("CharacterPresentationMode.AuthorityHeadless"));
        Assert.That(factory, Does.Contain("new NullAnimationPlayback()"));
        Assert.That(factory, Does.Contain("if (!headless)"));
        Assert.That(factory, Does.Contain("SpawnModelInstance("));
        Assert.That(factory, Does.Contain("presentationEnabled: !headless"));
    }

    /// <summary>从 Assets 相对路径读取真实生产脚本。</summary>
    static string ReadScript(string relativePath)
    {
        string path = ScriptPath(relativePath);
        Assert.That(File.Exists(path), Is.True, $"生产脚本不存在：{path}");
        return File.ReadAllText(path);
    }

    static string ScriptPath(string relativePath) =>
        Path.Combine(Application.dataPath, "Scripts", relativePath);
}
