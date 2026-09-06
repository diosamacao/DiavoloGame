using NUnit.Framework;

/// <summary>本体弹刀意图优先级：与切人 Guard 同级，低于 Success。</summary>
public sealed class SelfParryIntentTests
{
    /// <summary>Parry 与切人 Guard 同取消优先级，低于 Success。</summary>
    [Test]
    public void CancelPriority_Parry_MatchesAssistParry()
    {
        Assert.That(
            GameplayIntentCancelPriority.Get(GameplayIntentType.Parry),
            Is.EqualTo(GameplayIntentCancelPriority.Get(GameplayIntentType.AssistParry)));
        Assert.That(
            GameplayIntentCancelPriority.Get(GameplayIntentType.Parry),
            Is.LessThan(GameplayIntentCancelPriority.Get(GameplayIntentType.AssistParrySuccess)));
    }
}
