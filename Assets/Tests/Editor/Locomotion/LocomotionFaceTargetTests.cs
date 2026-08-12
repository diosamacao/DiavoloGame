using NUnit.Framework;
using UnityEngine;

/// <summary>L-DIR3：世界 wish→本地意图；FacingMode→Motor 映射。</summary>
public sealed class LocomotionFaceTargetTests
{
    [Test]
    public void ToLocalMoveIntent_WishAlongFacing_IsForward()
    {
        Vector2 local = LocomotionDirectionModel.ToLocalMoveIntent(
            Vector3.forward,
            Vector3.forward);
        Assert.That(local.y, Is.GreaterThan(0.9f));
        Assert.That(Mathf.Abs(local.x), Is.LessThan(0.1f));
    }

    [Test]
    public void ToLocalMoveIntent_WishToRightOfFacing_IsRight()
    {
        Vector2 local = LocomotionDirectionModel.ToLocalMoveIntent(
            Vector3.right,
            Vector3.forward);
        Assert.That(local.x, Is.GreaterThan(0.9f));
        Assert.That(Mathf.Abs(local.y), Is.LessThan(0.1f));
    }

    [Test]
    public void ToLocalMoveIntent_WishBehindFacing_IsBack()
    {
        Vector2 local = LocomotionDirectionModel.ToLocalMoveIntent(
            Vector3.back,
            Vector3.forward);
        Assert.That(local.y, Is.LessThan(-0.9f));
    }

    [Test]
    public void ToMotorRotationMode_FaceTarget_Maps()
    {
        Assert.That(
            CharacterLocomotionProfile.ToMotorRotationMode(LocomotionFacingMode.FaceTarget),
            Is.EqualTo(LocomotionRotationMode.FaceTarget));
    }

    [Test]
    public void Cardinal_FromLocalStrafeRight_IsRight()
    {
        Assert.That(
            LocomotionDirectionModel.Resolve(new Vector2(1f, 0f), 0.2f),
            Is.EqualTo(MoveCardinal.Right));
    }
}
