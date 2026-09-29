using System;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>验证清单迁移的 GUID、源内容保护、整批预检和失败回滚。</summary>
public sealed class CharacterAssetMigrationTests
{
    string folder;
    CharacterConfig config;

    [SetUp]
    public void Setup()
    {
        folder = "Assets/Data/__LayoutTest_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets/Data", Path.GetFileName(folder));
        config = ScriptableObject.CreateInstance<CharacterConfig>();
        AssetDatabase.CreateAsset(config, folder + "/Source.asset");
        AssetDatabase.SaveAssetIfDirty(config);
    }

    [TearDown]
    public void Cleanup() => AssetDatabase.DeleteAsset(folder);

    CharacterAssetMigrationEntry Entry(string source, string target)
    {
        using var sha = SHA256.Create();
        return new CharacterAssetMigrationEntry
        {
            oldPath = source, newPath = target, guid = AssetDatabase.AssetPathToGUID(source),
            oldName = AssetDatabase.LoadMainAssetAtPath(source).name,
            newName = Path.GetFileNameWithoutExtension(target),
            sha256 = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(source))).Replace("-", "").ToLowerInvariant(),
        };
    }

    [Test]
    public void Move_PreservesGuidAndReferences()
    {
        var identity = ScriptableObject.CreateInstance<CharacterDefinition>();
        AssetDatabase.CreateAsset(identity, folder + "/Identity.asset");
        var so = new SerializedObject(identity);
        so.FindProperty("characterConfig").objectReferenceValue = config;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(identity);
        var item = Entry(folder + "/Source.asset", folder + "/Config/Hero_Config.asset");
        string before = File.ReadAllText(item.oldPath);
        CharacterAssetMigration.Execute(new CharacterAssetMigrationManifest { entries = new[] { item } });
        Assert.That(AssetDatabase.AssetPathToGUID(item.newPath), Is.EqualTo(item.guid));
        Assert.That(File.Exists(item.oldPath), Is.False);
        Assert.That(identity.CharacterConfig, Is.SameAs(config));
        Assert.That(config.name, Is.EqualTo("Hero_Config"));
        Assert.That(File.ReadAllText(item.newPath), Is.EqualTo(before.Replace("  m_Name: " + item.oldName, "  m_Name: " + item.newName)));
    }

    [Test]
    public void ChangedSource_IsRejectedBeforeCreatingDestination()
    {
        var item = Entry(folder + "/Source.asset", folder + "/Config/Hero_Config.asset");
        config.name = "AuthorChanged";
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
        Assert.Throws<InvalidOperationException>(() => CharacterAssetMigration.Execute(new CharacterAssetMigrationManifest { entries = new[] { item } }));
        Assert.That(config.name, Is.EqualTo("AuthorChanged"));
        Assert.That(AssetDatabase.IsValidFolder(folder + "/Config"), Is.False);
    }

    [Test]
    public void OccupiedDestination_RejectsWholeBatch()
    {
        var first = Entry(folder + "/Source.asset", folder + "/Config/Hero_Config.asset");
        var secondAsset = ScriptableObject.CreateInstance<CharacterConfig>();
        AssetDatabase.CreateAsset(secondAsset, folder + "/Second.asset");
        var second = Entry(folder + "/Second.asset", folder + "/Source.asset");
        Assert.Throws<IOException>(() => CharacterAssetMigration.Execute(new CharacterAssetMigrationManifest { entries = new[] { first, second } }));
        Assert.That(AssetDatabase.GetAssetPath(config), Is.EqualTo(first.oldPath));
        Assert.That(AssetDatabase.IsValidFolder(folder + "/Config"), Is.False);
    }

    [Test]
    public void MidBatchFolderFailure_RollsBackEarlierMoves()
    {
        var secondAsset = ScriptableObject.CreateInstance<CharacterConfig>();
        AssetDatabase.CreateAsset(secondAsset, folder + "/Second.asset");
        var blocker = ScriptableObject.CreateInstance<CharacterConfig>();
        AssetDatabase.CreateAsset(blocker, folder + "/Block.asset");
        var first = Entry(folder + "/Source.asset", folder + "/Config/Hero_Config.asset");
        var second = Entry(folder + "/Second.asset", folder + "/Block.asset/Invalid.asset");
        Assert.Throws<AggregateException>(() => CharacterAssetMigration.Execute(new CharacterAssetMigrationManifest { entries = new[] { first, second } }));
        Assert.That(AssetDatabase.AssetPathToGUID(first.oldPath), Is.EqualTo(first.guid));
        Assert.That(config.name, Is.EqualTo(first.oldName));
        Assert.That(AssetDatabase.IsValidFolder(folder + "/Config"), Is.False);
        Assert.That(AssetDatabase.GetAssetPath(secondAsset), Is.EqualTo(second.oldPath));
    }

    [Test]
    public void IndependentActionCreation_UsesPurposeDirectoryAndIgnoresPreviousNames()
    {
        Assert.That(ActionDefinitionCreateUtility.ResolveActionDefinitionFolder(folder, false), Is.EqualTo(folder + "/Actions"));
        Assert.That(ActionDefinitionCreateUtility.ResolveActionDefinitionFolder(folder + "/Reactions", false), Is.EqualTo(folder + "/Reactions"));
        var clip = new AnimationClip { name = "OldCharacter_Attack" };
        try
        {
            Assert.That(ActionDefinitionCreateUtility.BuildDefaultFileName(folder, folder, clip), Is.EqualTo(Path.GetFileName(folder) + "_Action_01"));
        }
        finally { UnityEngine.Object.DestroyImmediate(clip); }
    }

    [Test]
    public void ProductionActions_HaveUniqueStableNetworkIds()
    {
        var ids = new System.Collections.Generic.HashSet<int>();
        var catalog = new ActionReplicationCatalog();
        foreach (string guid in AssetDatabase.FindAssets("t:ActionDefinition", new[] { "Assets/Data" }))
        {
            var action = AssetDatabase.LoadAssetAtPath<ActionDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.That(ids.Add(catalog.GetOrAdd(action)), Is.True, "重复动作身份：" + action.name);
        }
    }

    [Test]
    public void EnemyBodyInStandardLayout_UsesRoleKeyWithoutConfigSuffix()
    {
        var item = Entry(folder + "/Source.asset", folder + "/Hero/Config/Hero_Config.asset");
        CharacterAssetMigration.Execute(new CharacterAssetMigrationManifest { entries = new[] { item } });
        Assert.That(CharacterAssetLayout.ActionName(config, "Attack_01"), Is.EqualTo("Hero_Attack_01"));
        Assert.That(CharacterAssetLayout.ActionFolder(config, false), Is.EqualTo(folder + "/Hero/Actions"));
    }

    [Test]
    public void BaseFolders_AreIdempotentAndDoNotCreatePlaceholderAssets()
    {
        string before = File.ReadAllText(folder + "/Source.asset");
        CharacterAssetLayout.EnsureBaseFolders(folder);
        string guid = AssetDatabase.AssetPathToGUID(folder + "/Locomotion");
        CharacterAssetLayout.EnsureBaseFolders(folder);
        foreach (string sub in CharacterAssetLayout.Subfolders)
            Assert.That(AssetDatabase.IsValidFolder(folder + "/" + sub), Is.True);
        Assert.That(AssetDatabase.AssetPathToGUID(folder + "/Locomotion"), Is.EqualTo(guid));
        Assert.That(Directory.GetFiles(folder, "*.asset", SearchOption.AllDirectories).Length, Is.EqualTo(1));
        Assert.That(File.ReadAllText(folder + "/Source.asset"), Is.EqualTo(before));
    }

    [Test]
    public void SharedSource_IsDetectedByReferencesOutsideSharedDirectory()
    {
        var profile = ScriptableObject.CreateInstance<CharacterLocomotionProfile>();
        AssetDatabase.CreateAsset(profile, folder + "/Movement.asset");
        var other = ScriptableObject.CreateInstance<CharacterConfig>();
        AssetDatabase.CreateAsset(other, folder + "/Other.asset");
        foreach (var body in new[] { config, other })
        {
            var so = new SerializedObject(body);
            var modes = so.FindProperty("combatModes.entries");
            modes.arraySize = 1;
            modes.GetArrayElementAtIndex(0).FindPropertyRelative("locomotionProfile").objectReferenceValue = profile;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var owners = CharacterAuthoringService.FindOwners(profile);
        Assert.That(owners, Is.EquivalentTo(new[] { config, other }));
        Assert.That(CharacterConfigSourcePanel.Describe(profile, owners.Count), Does.Contain("多角色共用"));
        Assert.That(CharacterConfigSourcePanel.Describe(null, 0), Is.EqualTo("尚未配置"));
    }
}
