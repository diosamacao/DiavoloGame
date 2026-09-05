using System.Reflection;
using NUnit.Framework;

/// <summary>弹刀卡肉帧数：勾了 UseHitStop 用盒值，否则默认 8。</summary>
public sealed class AssistParryHitStopTests
{
    /// <summary>空 Feedback 走默认 8。</summary>
    [Test]
    public void ResolveFrames_NullFeedback_UsesDefault()
    {
        Assert.That(AssistParryHitStop.ResolveFrames(null), Is.EqualTo(AssistParryHitStop.DefaultFrames));
    }

    /// <summary>未勾 UseHitStop 走默认 8。</summary>
    [Test]
    public void ResolveFrames_DisabledHitStop_UsesDefault()
    {
        HitFeedbackSettings feedback = CreateFeedback(useHitStop: false, frames: 12);
        Assert.That(AssistParryHitStop.ResolveFrames(feedback), Is.EqualTo(AssistParryHitStop.DefaultFrames));
    }

    /// <summary>勾了但帧数为 0 时 UseHitStop 为假，仍走默认 8。</summary>
    [Test]
    public void ResolveFrames_ZeroConfiguredFrames_UsesDefault()
    {
        HitFeedbackSettings feedback = CreateFeedback(useHitStop: true, frames: 0);
        Assert.That(feedback.UseHitStop, Is.False);
        Assert.That(AssistParryHitStop.ResolveFrames(feedback), Is.EqualTo(AssistParryHitStop.DefaultFrames));
    }

    /// <summary>勾了且帧数有效时用盒上的帧。</summary>
    [Test]
    public void ResolveFrames_EnabledHitStop_UsesBoxFrames()
    {
        HitFeedbackSettings feedback = CreateFeedback(useHitStop: true, frames: 5);
        Assert.That(AssistParryHitStop.ResolveFrames(feedback), Is.EqualTo(5));
    }

    static HitFeedbackSettings CreateFeedback(bool useHitStop, int frames)
    {
        var feedback = new HitFeedbackSettings();
        SetField(feedback, "useHitStop", useHitStop);
        SetField(feedback, "hitStopFrames", frames);
        return feedback;
    }

    static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(target, value);
    }
}
