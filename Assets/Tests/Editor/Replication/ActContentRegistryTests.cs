using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>验证 ACT 动作目录、稳定网络原型与 Unity 角色内容由同一 Registry 精确管理。</summary>
public sealed class ActContentRegistryTests
{
    /// <summary>不同 EnemyDefinition.name 映射到不同 Archetype 与各自 CharacterConfig。</summary>
    [Test]
    public void RegisterEnemy_DifferentDefinitions_ResolveExactConfigs()
    {
        CharacterConfig firstConfig = CreateNamed<CharacterConfig>("Body_A");
        CharacterConfig secondConfig = CreateNamed<CharacterConfig>("Body_B");
        EnemyDefinition first = CreateEnemy("Enemy_A", firstConfig);
        EnemyDefinition second = CreateEnemy("Enemy_B", secondConfig);
        var registry = new ActContentRegistry();

        var firstId = registry.RegisterEnemy(first);
        var secondId = registry.RegisterEnemy(second);

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
        var registry = new ActContentRegistry();

        Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
            () => registry.ResolveCharacterConfig(default));
    }

    /// <summary>不同资产使用同一 Ordinal stableKey 时明确失败。</summary>
    [Test]
    public void RegisterEnemy_DifferentAssetsWithSameName_Throws()
    {
        CharacterConfig firstConfig = CreateNamed<CharacterConfig>("Body_A");
        CharacterConfig secondConfig = CreateNamed<CharacterConfig>("Body_B");
        EnemyDefinition first = CreateEnemy("Duplicated", firstConfig);
        EnemyDefinition second = CreateEnemy("Duplicated", secondConfig);
        var registry = new ActContentRegistry();
        registry.RegisterEnemy(first);

        Assert.Throws<System.InvalidOperationException>(() => registry.RegisterEnemy(second));
        DestroyAll(first, second, firstConfig, secondConfig);
    }

    /// <summary>玩家 stableKey 使用原始资产名，同一对象重复登记保持幂等。</summary>
    [Test]
    public void RegisterPlayer_SameConfig_IsIdempotent()
    {
        CharacterConfig config = CreateNamed<CharacterConfig>("Hero");
        var registry = new ActContentRegistry();

        var first = registry.RegisterPlayer(config);
        var second = registry.RegisterPlayer(config);

        Assert.That(second, Is.EqualTo(first));
        Assert.That(first.Value, Is.GreaterThan(0));
        Object.DestroyImmediate(config);
    }

    /// <summary>动作 Catalog 必须由 Registry 单例持有，登记结果与 ActionCount 使用同一真源。</summary>
    [Test]
    public void Actions_GetOrAdd_UpdatesRegistryActionCount()
    {
        var registry = new ActContentRegistry();
        ActionDefinition action = CreateNamed<ActionDefinition>("Attack_A");

        int actionId = registry.Actions.GetOrAdd(action);

        Assert.That(actionId, Is.GreaterThan(0));
        Assert.That(registry.ActionCount, Is.EqualTo(1));
        Assert.That(registry.Actions.TryGet(actionId, out ActionDefinition restored), Is.True);
        Assert.That(restored, Is.SameAs(action));
        Object.DestroyImmediate(action);
    }

    /// <summary>创建只存在于测试内存中的命名 ScriptableObject，不写入任何资产。</summary>
    static T CreateNamed<T>(string name) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        asset.name = name;
        return asset;
    }

    /// <summary>通过 SerializedObject 模拟 Inspector 绑定私有 CharacterConfig 字段。</summary>
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
