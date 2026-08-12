using NUnit.Framework;
using UnityEngine;

/// <summary>L-DIR1 DirectionModel：死区与主导轴 cardinal。</summary>
public sealed class LocomotionDirectionModelTests
{
    [Test]
    public void Resolve_BelowEpsilon_IsNone()
    {
        Assert.That(
            LocomotionDirectionModel.Resolve(new Vector2(0.1f, 0.1f), 0.2f),
            Is.EqualTo(MoveCardinal.None));
    }

    [Test]
    public void Resolve_ForwardDominant()
    {
        Assert.That(
            LocomotionDirectionModel.Resolve(new Vector2(0.1f, 0.9f), 0.2f),
            Is.EqualTo(MoveCardinal.Forward));
    }

    [Test]
    public void Resolve_BackDominant()
    {
        Assert.That(
            LocomotionDirectionModel.Resolve(new Vector2(0f, -0.8f), 0.2f),
            Is.EqualTo(MoveCardinal.Back));
    }

    [Test]
    public void Resolve_LeftDominant()
    {
        Assert.That(
            LocomotionDirectionModel.Resolve(new Vector2(-0.9f, 0.2f), 0.2f),
            Is.EqualTo(MoveCardinal.Left));
    }

    [Test]
    public void Resolve_RightDominant()
    {
        Assert.That(
            LocomotionDirectionModel.Resolve(new Vector2(0.9f, 0.2f), 0.2f),
            Is.EqualTo(MoveCardinal.Right));
    }

    [Test]
    public void Resolve_EqualAxes_PrefersForwardBack()
    {
        Assert.That(
            LocomotionDirectionModel.Resolve(new Vector2(0.5f, 0.5f), 0.2f),
            Is.EqualTo(MoveCardinal.Forward));
    }
}
