using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>验证稳定网络原型到 Unity 角色内容的精确绑定与冲突拒绝。</summary>
public sealed class CharacterReplicationContentRegistryTests
{
    /// <summary>不同 EnemyDefinition.name 映射到不同 Archetype 与各自 CharacterConfig。</summary>
    [Test]
    public void RegisterEnemy_DifferentDefinitions_ResolveExactConfigs()
    {
        CharacterConfig firstConfig = CreateNamed<CharacterConfig>("Body_A");
        CharacterConfig secondConfig = CreateNamed<CharacterConfig>("Body_B");
        EnemyDefinition first = CreateEnemy("Enemy_A", firstConfig);
        EnemyDefinition second = CreateEnemy("Enemy_B", secondConfig);
        var registry = new CharacterReplicationContentRegistry();

        NetArchetypeId firstId = registry.RegisterEnemy(first);
        NetArchetypeId secondId = registry.RegisterEnemy(second);

        Assert.That(firstId, Is.Not.EqualTo(secondId));
        Assert.That(registry.ResolveCharacterConfig(firstId), Is.SameAs(firstConfig));
        Assert.That(registry.ResolveCharacterConfig(secondId), Is.SameAs(secondConfig));
        Assert.That(registry.GetArchetypeId(first), Is.EqualTo(firstId));
        Assert.That(registry.RegisterEnemy(first), Is.EqualTo(firstId));
        DestroyAll(first, second, firstConfig, secondConfig);
    }

    /// <summary>未知 ArchetypeId 必须抛错，不能回退到任意敌人配置。</summary>
    [Test]
    public void ResolveCharacterConfig_UnknownId_ThrowsWithoutFallback()
    {
        var registry = new CharacterReplicationContentRegistry();

        Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
            () => registry.ResolveCharacterConfig(new NetArchetypeId(12345)));
    }

    /// <summary>不同资产使用同一 Ordinal stableKey 时明确失败。</summary>
    [Test]
    public void RegisterEnemy_DifferentAssetsWithSameName_Throws()
    {
        CharacterConfig firstConfig = CreateNamed<CharacterConfig>("Body_A");
        CharacterConfig secondConfig = CreateNamed<CharacterConfig>("Body_B");
        EnemyDefinition first = CreateEnemy("Duplicated", firstConfig);
        EnemyDefinition second = CreateEnemy("Duplicated", secondConfig);
        var registry = new CharacterReplicationContentRegistry();
        registry.RegisterEnemy(first);

        Assert.Throws<System.InvalidOperationException>(() => registry.RegisterEnemy(second));
        DestroyAll(first, second, firstConfig, secondConfig);
    }

    /// <summary>玩家 stableKey 使用原始资产名，同一对象重复登记保持幂等。</summary>
    [Test]
    public void RegisterPlayer_SameConfig_IsIdempotent()
    {
        CharacterConfig config = CreateNamed<CharacterConfig>("Hero");
        var registry = new CharacterReplicationContentRegistry();

        NetArchetypeId first = registry.RegisterPlayer(config);
        NetArchetypeId second = registry.RegisterPlayer(config);

        Assert.That(second, Is.EqualTo(first));
        Assert.That(
            first.Value,
            Is.EqualTo(CharacterArchetypeCatalog.ComputeStableId("player/Hero")));
        Object.DestroyImmediate(config);
    }

    // 创建只存在于测试内存中的命名 ScriptableObject，不写入任何资产。
    static T CreateNamed<T>(string name) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        asset.name = name;
        return asset;
    }

    // 通过 SerializedObject 模拟 Inspector 绑定私有 CharacterConfig 字段。
    static EnemyDefinition CreateEnemy(string name, CharacterConfig config)
    {
        EnemyDefinition definition = CreateNamed<EnemyDefinition>(name);
        var serialized = new SerializedObject(definition);
        serialized.FindProperty("characterConfig").objectReferenceValue = config;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    // 对称销毁测试创建的临时 Unity 对象。
    static void DestroyAll(params Object[] objects)
    {
        for (int i = 0; i < objects.Length; i++)
            Object.DestroyImmediate(objects[i]);
    }
}
