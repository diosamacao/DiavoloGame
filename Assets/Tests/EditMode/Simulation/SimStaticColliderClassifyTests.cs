using NUnit.Framework;

/// <summary>验证地面薄板与墙体分类，避免 Floor 被误烘成水平硬挡。</summary>
public sealed class SimStaticColliderClassifyTests
{
    /// <summary>大型薄地板判为地面。</summary>
    [Test]
    public void IsFloorLikeBounds_ThinLargeSlab_True()
    {
        Assert.That(
            SimStaticColliderClassify.IsFloorLikeBounds(40f, 0.2f, 40f),
            Is.True);
    }

    /// <summary>竖直墙体不判为地面。</summary>
    [Test]
    public void IsFloorLikeBounds_Wall_False()
    {
        Assert.That(
            SimStaticColliderClassify.IsFloorLikeBounds(0.5f, 3f, 10f),
            Is.False);
    }

    /// <summary>名称 Floor/Ground 识别。</summary>
    [Test]
    public void IsFloorLikeName_MatchesCommonTokens()
    {
        Assert.That(SimStaticColliderClassify.IsFloorLikeName("Floor"), Is.True);
        Assert.That(SimStaticColliderClassify.IsFloorLikeName("Arena_Ground_01"), Is.True);
        Assert.That(SimStaticColliderClassify.IsFloorLikeName("Wall_A"), Is.False);
    }
}
