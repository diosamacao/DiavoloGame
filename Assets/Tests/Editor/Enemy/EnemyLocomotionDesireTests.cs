using NUnit.Framework;
using UnityEngine;

/// <summary>移动命令源分层：AI Desire 与玩家 InputManager 各自实现 IMoveIntentSource。</summary>
public sealed class EnemyLocomotionDesireTests
{
    [Test]
    public void DesireBuffer_SetPeek_PreservesLocalMoveAndFace()
    {
        var buffer = new LocomotionDesireBuffer();
        buffer.Set(new LocomotionDesire(new Vector2(0.3f, 0.9f), faceTarget: true));

        Assert.That(buffer.TryPeek(out LocomotionDesire desire), Is.True);
        Assert.That(desire.LocalMove.y, Is.GreaterThan(0.8f));
        Assert.That(desire.FaceTarget, Is.True);
        Assert.That(desire.HasMove, Is.True);
        Assert.That(buffer.HasMoveIntent, Is.True);
        Assert.That(buffer.MoveMagnitude, Is.GreaterThan(0.8f));
    }

    [Test]
    public void DesireBuffer_Clear_StopsMove()
    {
        var buffer = new LocomotionDesireBuffer();
        buffer.Set(new LocomotionDesire(Vector2.up, faceTarget: true));
        buffer.Clear();

        buffer.TryPeek(out LocomotionDesire desire);
        Assert.That(desire.HasMove, Is.False);
        Assert.That(desire.FaceTarget, Is.False);
        Assert.That(buffer.HasMoveIntent, Is.False);
        Assert.That(buffer.BufferedMoveIntent, Is.EqualTo(Vector2.up));
    }

    [Test]
    public void DesireSource_EmptyInputFrame_StillHasForwardMove()
    {
        // EnemyHandle 写空帧；移动系统直接读取独立 Desire source
        var input = new InputManager();
        input.IngestFrame(InputFrame.Empty(1, default));
        Assert.That(input.HasMoveIntent, Is.False);

        IMoveIntentSource source = new LocomotionDesireBuffer();
        ((LocomotionDesireBuffer)source).Set(
            new LocomotionDesire(new Vector2(0f, 1f), faceTarget: true));

        Assert.That(source.HasMoveIntent, Is.True);
        Assert.That(source.MoveIntent.y, Is.GreaterThan(0.9f));
        Assert.That(source.MoveMagnitude, Is.GreaterThan(0.9f));
    }

    [Test]
    public void DesireBuffer_ZeroDesire_StopsAfterPriorMove()
    {
        var source = new LocomotionDesireBuffer();
        source.Set(new LocomotionDesire(Vector2.up, faceTarget: true));
        Assert.That(source.HasMoveIntent, Is.True);

        // Action freeze 提交零 Desire；不依赖 InputManager 覆盖生命周期
        source.Set(new LocomotionDesire(Vector2.zero, faceTarget: true));
        Assert.That(source.HasMoveIntent, Is.False);
        Assert.That(source.MoveIntent, Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void PlayerPath_NoOverride_UsesFrameAxesOnly()
    {
        // 玩家把 InputManager 自身作为 IMoveIntentSource
        var input = new InputManager();
        sbyte q = InputQuantizer.QuantizeAxis(1f);
        var frame = new InputFrame(1, default, 0, q, 0ul, 0ul, 0ul);
        input.IngestFrame(frame);

        IMoveIntentSource source = input;
        Assert.That(source.HasMoveIntent, Is.True);
        Assert.That(source.MoveIntent.y, Is.GreaterThan(0.9f));
    }
}
