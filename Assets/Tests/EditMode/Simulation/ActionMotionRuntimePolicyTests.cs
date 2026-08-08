using NUnit.Framework;

/// <summary>Wave 2.5：验证 BaseMotionMode 解析，无 Legacy / Animator RM 回退。</summary>
public sealed class ActionMotionRuntimePolicyTests
{
    [Test]
    public void Resolve_BakedMode_RequiresReadyTable()
    {
        Assert.That(
            ActionMotionRuntimePolicy.Resolve(
                ActionBaseMotionMode.BakedMotion,
                bakedMotionReady: true,
                hasScriptedMovement: false),
            Is.EqualTo(ActionDisplacementSource.BakedMotion));

        Assert.That(
            ActionMotionRuntimePolicy.Resolve(
                ActionBaseMotionMode.BakedMotion,
                bakedMotionReady: false,
                hasScriptedMovement: true),
            Is.EqualTo(ActionDisplacementSource.None));
    }

    [Test]
    public void Resolve_ScriptedMode_RequiresMovementWindows()
    {
        Assert.That(
            ActionMotionRuntimePolicy.Resolve(
                ActionBaseMotionMode.ScriptedTimeline,
                bakedMotionReady: false,
                hasScriptedMovement: true),
            Is.EqualTo(ActionDisplacementSource.ScriptedTimeline));

        Assert.That(
            ActionMotionRuntimePolicy.Resolve(
                ActionBaseMotionMode.ScriptedTimeline,
                bakedMotionReady: true,
                hasScriptedMovement: false),
            Is.EqualTo(ActionDisplacementSource.None));
    }

    [Test]
    public void Resolve_NoneAndObsoleteLegacy_YieldNone()
    {
        Assert.That(
            ActionMotionRuntimePolicy.Resolve(
                ActionBaseMotionMode.None,
                bakedMotionReady: true,
                hasScriptedMovement: true),
            Is.EqualTo(ActionDisplacementSource.None));

        // 已删除的 LegacyResolve=0
        Assert.That(
            ActionMotionRuntimePolicy.Resolve(
                (ActionBaseMotionMode)0,
                bakedMotionReady: true,
                hasScriptedMovement: false),
            Is.EqualTo(ActionDisplacementSource.None));
    }
}
