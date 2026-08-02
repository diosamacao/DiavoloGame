using NUnit.Framework;

/// <summary>验证 InPlace stem 剥离与 RootMotion 匹配优先级。</summary>
public sealed class MotionClipNameRulesTests
{
    /// <summary>标准 _Inplace 后缀得到 stem。</summary>
    [Test]
    public void TryGetInplaceStem_StripsSuffix()
    {
        Assert.That(MotionClipNameRules.TryGetInplaceStem("Attack_01_Inplace", out string stem), Is.True);
        Assert.That(stem, Is.EqualTo("Attack_01"));
    }

    /// <summary>无后缀不得作为 InPlace 自动匹配。</summary>
    [Test]
    public void TryGetInplaceStem_RejectsMissingSuffix()
    {
        Assert.That(MotionClipNameRules.TryGetInplaceStem("Attack_01", out _), Is.False);
    }

    /// <summary>Unagi|Attack_01 以 P1 命中 stem Attack_01。</summary>
    [Test]
    public void GetMatchPriority_PrefersPipePrefixedClipName()
    {
        int priority = MotionClipNameRules.GetMatchPriority(
            "Attack_01",
            "Unagi|Attack_01",
            "Attack_01");
        Assert.That(priority, Is.EqualTo(1));
    }

    /// <summary>Attack_01 不得模糊命中 Attack_01_End。</summary>
    [Test]
    public void GetMatchPriority_DoesNotMatchLongerStem()
    {
        int priority = MotionClipNameRules.GetMatchPriority(
            "Attack_01",
            "Unagi|Attack_01_End",
            "Attack_01_End");
        Assert.That(priority, Is.EqualTo(-1));
    }

    /// <summary>文件名精确相等为 P2。</summary>
    [Test]
    public void GetMatchPriority_FileNameExactIsP2()
    {
        int priority = MotionClipNameRules.GetMatchPriority(
            "Attack_01",
            "SomeOtherTake",
            "Attack_01");
        Assert.That(priority, Is.EqualTo(2));
    }
}
