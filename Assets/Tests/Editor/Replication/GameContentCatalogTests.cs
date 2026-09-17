using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>验证冻结 Gameplay Catalog 的稳定网络原型、动作查询与未知内容失败语义。</summary>
public sealed class GameContentCatalogTests
{
    /// <summary>不同 EnemyDefinition 映射到不同 Archetype 与各自 CharacterConfig。</summary>
    [Test]
    public void AddEnemy_DifferentDefinitions_ResolveExactConfigs()
    {
        CharacterConfig firstConfig = CreateNamed<CharacterConfig>("Body_A");
        CharacterConfig secondConfig = CreateNamed<CharacterConfig>("Body_B");
        EnemyDefinition first = CreateEnemy("Enemy_A", firstConfig);
        EnemyDefinition second = CreateEnemy("Enemy_B", secondConfig);
        var catalog = new GameContentCatalog();

        catalog.AddEnemy(first);
        catalog.AddEnemy(second);
        NetArchetypeId firstId = catalog.GetArchetypeId(first);
        NetArchetypeId secondId = catalog.GetArchetypeId(second);

        Assert.That(firstId, Is.Not.EqualTo(secondId));
        Assert.That(catalog.ResolveCharacterConfig(firstId), Is.SameAs(firstConfig));
        Assert.That(catalog.ResolveCharacterConfig(secondId), Is.SameAs(secondConfig));
        DestroyAll(first, second, firstConfig, secondConfig);
    }

    /// <summary>未知 ArchetypeId 必须抛错，不能回退到任意敌人配置。</summary>
    [Test]
    public void ResolveCharacterConfig_UnknownId_ThrowsWithoutFallback()
    {
        var catalog = new GameContentCatalog();

        Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
            () => catalog.ResolveCharacterConfig(default));
    }

    /// <summary>不同资产使用同一 Ordinal stable key 时必须在 Build 阶段失败。</summary>
    [Test]
    public void AddEnemy_DifferentAssetsWithSameName_Throws()
    {
        CharacterConfig firstConfig = CreateNamed<CharacterConfig>("Body_A");
        CharacterConfig secondConfig = CreateNamed<CharacterConfig>("Body_B");
        EnemyDefinition first = CreateEnemy("Duplicated", firstConfig);
        EnemyDefinition second = CreateEnemy("Duplicated", secondConfig);
        var catalog = new GameContentCatalog();
        catalog.AddEnemy(first);

        Assert.Throws<System.InvalidOperationException>(() => catalog.AddEnemy(second));
        DestroyAll(first, second, firstConfig, secondConfig);
    }

    /// <summary>动作目录冻结后只允许 Require 已登记动作，未知动作不得运行时加入。</summary>
    [Test]
    public void Actions_Freeze_BlocksRuntimeRegistration()
    {
        var catalog = new GameContentCatalog();
        ActionDefinition known = CreateNamed<ActionDefinition>("Attack_A");
        ActionDefinition unknown = CreateNamed<ActionDefinition>("Attack_B");
        int knownId = catalog.Actions.GetOrAdd(known);
        catalog.Actions.Freeze();

        Assert.That(catalog.Actions.RequireId(known), Is.EqualTo(knownId));
        Assert.Throws<System.InvalidOperationException>(
            () => catalog.Actions.GetOrAdd(unknown));
        Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
            () => catalog.Actions.RequireId(unknown));
        DestroyAll(known, unknown);
    }

    /// <summary>创建只存在于测试内存中的命名 ScriptableObject。</summary>
    static T CreateNamed<T>(string name) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        asset.name = name;
        return asset;
    }

    /// <summary>通过 SerializedObject 模拟 Inspector 绑定 CharacterConfig。</summary>
    static EnemyDefinition CreateEnemy(string name, CharacterConfig config)
    {
        EnemyDefinition definition = CreateNamed<EnemyDefinition>(name);
        var serialized = new SerializedObject(definition);
        serialized.FindProperty("characterConfig").objectReferenceValue = config;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    /// <summary>对称销毁测试创建的临时 Unity 对象。</summary>
    static void DestroyAll(params Object[] objects)
    {
        for (int i = 0; i < objects.Length; i++)
            Object.DestroyImmediate(objects[i]);
    }
}
