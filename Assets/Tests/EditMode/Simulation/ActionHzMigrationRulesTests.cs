using NUnit.Framework;

/// <summary>验证 30Hz 到 60Hz 迁移的点事件、闭区间与重复执行门禁。</summary>
public sealed class ActionHzMigrationRulesTests
{
    /// <summary>点事件映射后保持相同时间位置。</summary>
    [Test]
    public void MapPointFrame_DoublesFrame()
    {
        Assert.That(ActionHzMigrationRules.MapPointFrame(4), Is.EqualTo(8));
    }

    /// <summary>闭区间映射后帧数翻倍并保持两端覆盖语义。</summary>
    [Test]
    public void MapClosedInterval_PreservesDuration()
    {
        ActionHzMigrationRules.MapClosedInterval(5, 8, out int start, out int end);

        Assert.That(start, Is.EqualTo(10));
        Assert.That(end, Is.EqualTo(17));
        Assert.That(end - start + 1, Is.EqualTo(8));
    }

    /// <summary>只有 30Hz 可迁移，已完成的 60Hz 资产必须跳过。</summary>
    [Test]
    public void ShouldMigrate_RejectsRepeatedMigration()
    {
        Assert.That(ActionHzMigrationRules.ShouldMigrate(30), Is.True);
        Assert.That(ActionHzMigrationRules.ShouldMigrate(60), Is.False);
    }
}
