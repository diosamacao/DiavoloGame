using NUnit.Framework;

/// <summary>验证 SimActorId 的有效性、排序与稳定值语义。</summary>
public sealed class SimActorIdTests
{
    /// <summary>默认值必须保留为无效注册标识。</summary>
    [Test]
    public void DefaultValue_IsInvalid()
    {
        Assert.That(SimActorId.Invalid.IsValid, Is.False);
    }

    /// <summary>较小整数 Id 必须稳定排在较大 Id 之前。</summary>
    [Test]
    public void CompareTo_OrdersByIntegerValue()
    {
        var first = new SimActorId(1);
        var second = new SimActorId(2);

        Assert.That(first.CompareTo(second), Is.LessThan(0));
        Assert.That(second.CompareTo(first), Is.GreaterThan(0));
    }

    /// <summary>相同整数值必须具备相同相等性和哈希。</summary>
    [Test]
    public void Equality_UsesStableIntegerValue()
    {
        var left = new SimActorId(7);
        var right = new SimActorId(7);

        Assert.That(left, Is.EqualTo(right));
        Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
    }
}
