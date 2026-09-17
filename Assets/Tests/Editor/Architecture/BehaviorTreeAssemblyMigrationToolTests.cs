using NUnit.Framework;

/// <summary>锁定 CS2B 行为树多次程序集迁移前的 YAML 门禁。</summary>
public sealed class BehaviorTreeAssemblyMigrationToolTests
{
    /// <summary>仍记录 Assembly-CSharp 的行为树不得进入终态程序集切换。</summary>
    [Test]
    public void IsReadyForCs2B_LegacyAssembly_ReturnsFalse()
    {
        const string yaml =
            "type: {class: SequenceNodeDef, ns: , asm: Assembly-CSharp}";

        Assert.That(
            BehaviorTreeAssemblyMigrationTool.IsReadyForCs2B(yaml),
            Is.False);
    }

    /// <summary>统一记录为当前粗程序集后才允许开始第二次迁移。</summary>
    [Test]
    public void IsReadyForCs2B_CurrentGameplayAssembly_ReturnsTrue()
    {
        const string yaml =
            "type: {class: SequenceNodeDef, ns: , asm: ACTGame.Domain.Gameplay}";

        Assert.That(
            BehaviorTreeAssemblyMigrationTool.IsReadyForCs2B(yaml),
            Is.True);
    }
}
