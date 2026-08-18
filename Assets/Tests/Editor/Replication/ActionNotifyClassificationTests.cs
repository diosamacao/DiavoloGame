using NUnit.Framework;

/// <summary>W6 Notify 通道：VFX/SFX 为表现，其余点事件默认玩法。</summary>
public sealed class ActionNotifyClassificationTests
{
    /// <summary>PlayVfx / PlaySfx 不得在 Headless 当玩法执行。</summary>
    [Test]
    public void Classify_PresentationNotifies()
    {
        Assert.That(
            ActionNotifyClassification.Classify(new PlayVfxNotify()),
            Is.EqualTo(ActionNotifyChannel.Presentation));
        Assert.That(
            ActionNotifyClassification.Classify(new PlaySfxNotify()),
            Is.EqualTo(ActionNotifyChannel.Presentation));
    }

    /// <summary>未知点事件默认 Gameplay，避免漏掉位移或资源。</summary>
    [Test]
    public void Classify_UnknownNotify_DefaultsToGameplay()
    {
        Assert.That(
            ActionNotifyClassification.Classify(new MotionCommandNotify()),
            Is.EqualTo(ActionNotifyChannel.Gameplay));
    }
}
