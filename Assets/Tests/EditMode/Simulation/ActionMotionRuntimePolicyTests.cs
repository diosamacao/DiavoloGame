using NUnit.Framework;

/// <summary>Wave 1：验证 BaseMotionMode 解析与 Legacy 回退。</summary>
public sealed class ActionMotionRuntimePolicyTests
{
    [Test]
    public void Resolve_BakedMode_RequiresReadyTable()
    {
        Assert.That(
            ActionMotionRuntimePolicy.Resolve(
                ActionBaseMotionMode.BakedMotion,
                useRootMotionPolicy: true,
                bakedMotionReady: true,
                hasScriptedMovement: false),
            Is.EqualTo(ActionDisplacementSource.BakedMotion));

        Assert.That(
            ActionMotionRuntimePolicy.Resolve(
                ActionBaseMotionMode.BakedMotion,
                useRootMotionPolicy: true,
                bakedMotionReady: false,
                hasScriptedMovement: true),
            Is.EqualTo(ActionDisplacementSource.None));
    }

    [Test]
    public void Resolve_Legacy_PrefersBakedThenScriptedThenRm()
    {
        Assert.That(
            ActionMotionRuntimePolicy.Resolve(
                ActionBaseMotionMode.LegacyResolve,
                useRootMotionPolicy: true,
                bakedMotionReady: true,
                hasScriptedMovement: true),
            Is.EqualTo(ActionDisplacementSource.BakedMotion));

        Assert.That(
            ActionMotionRuntimePolicy.Resolve(
                ActionBaseMotionMode.LegacyResolve,
                useRootMotionPolicy: false,
                bakedMotionReady: false,
                hasScriptedMovement: true),
            Is.EqualTo(ActionDisplacementSource.ScriptedTimeline));

        Assert.That(
            ActionMotionRuntimePolicy.Resolve(
                ActionBaseMotionMode.LegacyResolve,
                useRootMotionPolicy: true,
                bakedMotionReady: false,
                hasScriptedMovement: false),
            Is.EqualTo(ActionDisplacementSource.AnimatorRootMotion));
    }

    [Test]
    public void Resolve_ExplicitNone_BlocksAnimatorRm()
    {
        Assert.That(
            ActionMotionRuntimePolicy.Resolve(
                ActionBaseMotionMode.None,
                useRootMotionPolicy: true,
                bakedMotionReady: false,
                hasScriptedMovement: false),
            Is.EqualTo(ActionDisplacementSource.None));
    }
}
