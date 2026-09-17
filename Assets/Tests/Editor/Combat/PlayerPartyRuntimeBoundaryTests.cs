using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>锁定本机阵容算法由 PlayerPartyRuntime 独占，PlayerController 只保留场景接线。</summary>
public sealed class PlayerPartyRuntimeBoundaryTests
{
    /// <summary>Controller 不得重新持有槽数组、协调器或预测切人算法。</summary>
    [Test]
    public void PlayerController_DoesNotRetainPartyAlgorithms()
    {
        string source = ReadScript("App/Controllers/Gameplay/PlayerController.cs");

        Assert.That(source, Does.Not.Contain("PartyCombatCoordinator"));
        Assert.That(source, Does.Not.Contain("_partyActors"));
        Assert.That(source, Does.Not.Contain("_predictedSwitchFrame"));
        Assert.That(source, Does.Not.Contain("TryResolveSwitch("));
        Assert.That(source, Does.Not.Contain("TryResolveActiveDeath("));
        Assert.That(source, Does.Contain("new PlayerPartyRuntime("));
    }

    /// <summary>Runtime 必须拥有三槽创建、预测推进、权威同步与生命周期释放入口。</summary>
    [Test]
    public void PlayerPartyRuntime_OwnsRequiredResponsibilities()
    {
        string source = ReadScript("App/Controllers/Gameplay/PlayerPartyRuntime.cs");

        Assert.That(source, Does.Contain("CharacterActorFactory.Create("));
        Assert.That(source, Does.Contain("public void StepPrediction("));
        Assert.That(source, Does.Contain("public void SynchronizeActiveSlot("));
        Assert.That(source, Does.Contain("public void SynchronizeMemberState("));
        Assert.That(source, Does.Contain("public void SynchronizePartyWiped("));
        Assert.That(source, Does.Contain("public void Dispose()"));
    }

    /// <summary>Client Gameplay 必须依赖 Party Runtime，不得回调 Controller 旧转发方法。</summary>
    [Test]
    public void ClientGameplay_UsesPartyRuntimeContract()
    {
        string source = ReadScript("App/Networking/Services/OwnerPredictionCoordinator.cs");

        Assert.That(source, Does.Contain("_localPlayer.Party.StepPrediction("));
        Assert.That(source, Does.Contain("_localPlayer.Party.BindSimulationInput("));
        Assert.That(source, Does.Not.Contain("StepPartyPrediction("));
        Assert.That(source, Does.Not.Contain("BindPartySimulationInput("));
        Assert.That(source, Does.Not.Contain("SynchronizeAuthorityPartyState("));
    }

    /// <summary>从 Assets 相对路径读取生产脚本。</summary>
    static string ReadScript(string relativePath)
    {
        string path = Path.Combine(Application.dataPath, "Scripts", relativePath);
        Assert.That(File.Exists(path), Is.True, $"生产脚本不存在：{path}");
        return File.ReadAllText(path);
    }
}
