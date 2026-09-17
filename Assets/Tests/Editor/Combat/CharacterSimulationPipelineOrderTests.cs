using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>锁定 CharacterSimulationPipeline 的固定帧与战斗后处理顺序。</summary>
public sealed class CharacterSimulationPipelineOrderTests
{
    /// <summary>CharacterActor 必须只把 SimulationWorld 入口交给唯一 Pipeline。</summary>
    [Test]
    public void CharacterActor_SimulationEntries_DelegateToPipeline()
    {
        string source = ReadScript("Domain/Character/CharacterActor.cs");

        Assert.That(source, Does.Contain("_simulationPipeline.Step("));
        Assert.That(source, Does.Contain("_simulationPipeline.ResolvePostCombat(frameIndex)"));
        Assert.That(source, Does.Not.Contain("_intentProducer.Step();"));
        Assert.That(source, Does.Not.Contain("_actionSim?.ResolvePostCombat();"));
    }

    /// <summary>Step 各阶段必须保持输入、动作、运动、状态、表现和数值的既有先后关系。</summary>
    [Test]
    public void Step_Order_RemainsStable()
    {
        string source = ReadScript("Domain/Character/CharacterSimulationPipeline.cs");

        AssertTokensInOrder(
            source,
            "_vitality?.ClearReplicationEdge();",
            "_presentation.BeginSimulationStep();",
            "_motor?.Sim.TickSoftBodySuppress();",
            "_inputManager.IngestFrame(effectiveInput);",
            "_targetingState.Step(actorId, _motor.Sim, in effectiveInput);",
            "_intentProducer.Step();",
            "_emitExternalIntent();",
            "StepActionClock();",
            "_actionPresentation?.ApplyStep(fixedDeltaSeconds);",
            "_motor.TickGravity(fixedDeltaSeconds);",
            "_stateMachine.Tick(fixedDeltaSeconds);",
            "_visualMotion?.SetLeanRollDegrees(_stateMachine.SprintLeanRollDegrees);",
            "_animation?.Tick(fixedDeltaSeconds);",
            "UpdateActionLateralPeakSample();",
            "_numeric.Step();",
            "_presentation.EndSimulationStep();");
    }

    /// <summary>PostCombat 必须先收敛模拟状态，再派发表现，最后推进 Party 生命周期。</summary>
    [Test]
    public void ResolvePostCombat_Order_RemainsStable()
    {
        string source = ReadScript("Domain/Character/CharacterSimulationPipeline.cs");

        AssertTokensInOrder(
            source,
            "_actionSim?.ResolvePostCombat();",
            "_stateMachine.ResolvePostCombat();",
            "_actionPresentation?.ApplyPostCombat();",
            "_postCombatCompleted?.Invoke();");
    }

    /// <summary>按声明顺序验证源代码令牌，避免结构重排暗改逻辑帧语义。</summary>
    static void AssertTokensInOrder(string source, params string[] tokens)
    {
        int cursor = 0;
        for (int i = 0; i < tokens.Length; i++)
        {
            int index = source.IndexOf(tokens[i], cursor, System.StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), $"缺少顺序令牌：{tokens[i]}");
            cursor = index + tokens[i].Length;
        }
    }

    /// <summary>读取 Assets/Scripts 下的生产脚本。</summary>
    static string ReadScript(string relativePath) =>
        File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", relativePath));
}
