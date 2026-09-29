using NUnit.Framework;
using UnityEditor;

/// <summary>验证旧 Assembly-CSharp 行为树 ManagedReference 在程序集切分后仍能完整恢复。</summary>
public sealed class EnemyBehaviorTreeAssemblyMigrationTests
{
    /// <summary>生产行为树不得含缺失类型，且反序列化后的根节点必须存在。</summary>
    [TestCase("Assets/Data/Characters/Monster/AI/Monster_BehaviorTree.asset")]
    [TestCase("Assets/Data/Characters/UnagiEnemy/AI/UnagiEnemy_BehaviorTree.asset")]
    public void ProductionBehaviorTree_ResolvesLegacyManagedReferences(string assetPath)
    {
        EnemyBehaviorTreeAsset asset =
            AssetDatabase.LoadAssetAtPath<EnemyBehaviorTreeAsset>(assetPath);

        Assert.That(asset, Is.Not.Null, assetPath);
        Assert.That(
            SerializationUtility.HasManagedReferencesWithMissingTypes(asset),
            Is.False,
            $"{assetPath} 仍含无法解析的 SerializeReference 类型。");
        Assert.That(asset.CustomRoot, Is.Not.Null, $"{assetPath} 根节点未恢复。");
    }
}
