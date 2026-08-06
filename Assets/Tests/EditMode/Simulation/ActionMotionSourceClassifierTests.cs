using NUnit.Framework;

/// <summary>Wave 0：验证位移源三源互斥归类。</summary>
public sealed class ActionMotionSourceClassifierTests
{
    [Test]
    public void Classify_None_WhenNeitherSource()
    {
        Assert.That(
            ActionMotionSourceClassifier.Classify(bakedReady: false, hasScriptedMovement: false),
            Is.EqualTo(ActionMotionSourceKind.None));
    }

    [Test]
    public void Classify_Baked_WhenOnlyBakedReady()
    {
        Assert.That(
            ActionMotionSourceClassifier.Classify(bakedReady: true, hasScriptedMovement: false),
            Is.EqualTo(ActionMotionSourceKind.Baked));
    }

    [Test]
    public void Classify_Scripted_WhenOnlyScripted()
    {
        Assert.That(
            ActionMotionSourceClassifier.Classify(bakedReady: false, hasScriptedMovement: true),
            Is.EqualTo(ActionMotionSourceKind.Scripted));
    }

    [Test]
    public void Classify_Conflict_WhenBakedAndScripted()
    {
        Assert.That(
            ActionMotionSourceClassifier.Classify(bakedReady: true, hasScriptedMovement: true),
            Is.EqualTo(ActionMotionSourceKind.Conflict));
    }
}
