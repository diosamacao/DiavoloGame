using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>守卫 Autonomous 预测职责只由 CharacterPredictionRuntime 实现。</summary>
public sealed class CharacterPredictionRuntimeBoundaryTests
{
    /// <summary>CharacterActor 不得继续实现 Replay 接口或保留旧预测方法。</summary>
    [Test]
    public void CharacterActor_DoesNotRetainPredictionImplementation()
    {
        string source = ReadScript("Domain/Character/CharacterActor.cs");

        Assert.That(
            source,
            Does.Contain("public CharacterPredictionRuntime Prediction => _prediction;"));
        Assert.That(source, Does.Not.Contain("IPredictedLocomotionReplay"));
        Assert.That(source, Does.Not.Contain("public void StopAutonomousAction("));
        Assert.That(source, Does.Not.Contain("public void RestoreFromAuthority("));
        Assert.That(source, Does.Not.Contain("public void ReplayTick("));
    }

    /// <summary>Owner 适配器必须把取消和 Replay 都定向到独立预测运行时。</summary>
    [Test]
    public void OwnerAdapter_UsesPredictionRuntimeContract()
    {
        string source = ReadScript(
            "App/Networking/Adapters/ActOwnerReplicationAdapter.cs");

        Assert.That(source, Does.Contain("actor.Prediction.StopAutonomousAction();"));
        Assert.That(source, Does.Contain("replay: actor.Prediction"));
        Assert.That(source, Does.Not.Contain("actor.StopAutonomousAction();"));
        Assert.That(source, Does.Not.Contain("replay: actor,"));
    }

    /// <summary>读取 Assets/Scripts 下的生产脚本。</summary>
    static string ReadScript(string relativePath) =>
        File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", relativePath));
}
