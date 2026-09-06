using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>本体弹刀 Producer：Parry 键产出独立意图，不走切人语义。</summary>
public sealed class SelfParryProducerTests
{
    /// <summary>Parry 按下边沿产出本体意图，不经过切人协调器语义。</summary>
    [Test]
    public void Producer_ParryPressed_EmitsParryNotAssistParry()
    {
        GameplayIntentProfile profile = ScriptableObject.CreateInstance<GameplayIntentProfile>();
        var input = new InputManager();
        var buffer = new GameplayIntentBuffer(8);
        var producer = new GameplayIntentProducer(
            profile,
            input,
            buffer,
            stateMachine: null,
            locomotion: null,
            actionSim: null);

        try
        {
            StepWithParryPressed(input, producer);

            Assert.That(ContainsIntent(buffer, GameplayIntentType.Parry), Is.True);
            Assert.That(ContainsIntent(buffer, GameplayIntentType.AssistParry), Is.False);
            Assert.That(ContainsIntent(buffer, GameplayIntentType.SwitchIn), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }

    /// <summary>本体弹刀不依赖 Profile；空映射仍能产出。</summary>
    [Test]
    public void Producer_NullProfile_StillEmitsParry()
    {
        var input = new InputManager();
        var buffer = new GameplayIntentBuffer(8);
        var producer = new GameplayIntentProducer(
            profile: null,
            input,
            buffer,
            stateMachine: null,
            locomotion: null,
            actionSim: null);

        StepWithParryPressed(input, producer);

        Assert.That(ContainsIntent(buffer, GameplayIntentType.Parry), Is.True);
    }

    static void StepWithParryPressed(InputManager input, GameplayIntentProducer producer)
    {
        ulong mask = InputButtonMask.Of(InputButton.Parry);
        input.IngestFrame(new InputFrame(
            1,
            new SimActorId(1),
            moveX: 0,
            moveY: 0,
            buttonsPressed: mask,
            buttonsHeld: mask,
            buttonsReleased: 0));
        producer.Step();
    }

    /// <summary>本套 NUnit 的 Does.Contain 只收 string，集合成员用手写扫描。</summary>
    static bool ContainsIntent(GameplayIntentBuffer buffer, GameplayIntentType intent)
    {
        IReadOnlyList<GameplayIntentType> intents = buffer.FrameIntents;
        for (int i = 0; i < intents.Count; i++)
        {
            if (intents[i] == intent)
                return true;
        }

        return false;
    }
}
