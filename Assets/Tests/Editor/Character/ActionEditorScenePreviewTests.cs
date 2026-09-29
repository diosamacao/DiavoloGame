using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>角色上下文不应锁死场景预览，重载与显式切换应遵循作者选择。</summary>
public sealed class ActionEditorScenePreviewTests
{
    ActionEditorWindow window;
    CharacterConfig config;
    GameObject model;
    int savedPreference;
    bool hadPreference;
    const string Preference = "ACTGame.ActionEditor.PreviewCharacter";
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void Setup()
    {
        hadPreference = EditorPrefs.HasKey(Preference);
        savedPreference = EditorPrefs.GetInt(Preference);
        EditorPrefs.DeleteKey(Preference);
        window = ScriptableObject.CreateInstance<ActionEditorWindow>();
        config = ScriptableObject.CreateInstance<CharacterConfig>();
        model = new GameObject("ScenePreviewTestModel");
        typeof(ActionEditorWindow).GetField("_characterContext", PrivateInstance).SetValue(window, config);
    }

    [TearDown]
    public void Cleanup()
    {
        Object.DestroyImmediate(window);
        Object.DestroyImmediate(model);
        Object.DestroyImmediate(config);
        if (hadPreference) EditorPrefs.SetInt(Preference, savedPreference);
        else EditorPrefs.DeleteKey(Preference);
    }

    [Test]
    public void SceneSelection_WithRoleContext_SurvivesContextRestore()
    {
        window.UseScenePreview(model.transform);
        typeof(ActionEditorWindow).GetMethod("RestoreCharacterContext", PrivateInstance).Invoke(window, null);
        Assert.That(typeof(ActionEditorWindow).GetField("_previewCharacter", PrivateInstance).GetValue(window), Is.SameAs(model.transform));
        Assert.That(typeof(ActionEditorWindow).GetField("_characterContext", PrivateInstance).GetValue(window), Is.SameAs(config));
    }

    [Test]
    public void ExplicitIsolatedSwitch_ClearsSceneTarget_AndSceneCanBeReselected()
    {
        window.UseScenePreview(model.transform);
        window.UseIsolatedPreview(); // 无 Prefab 的角色没有隔离实例，但必须停止原场景目标。
        Assert.That(typeof(ActionEditorWindow).GetField("_previewCharacter", PrivateInstance).GetValue(window), Is.Null);
        window.UseScenePreview(model.transform);
        window.UseScenePreview(null);
        typeof(ActionEditorWindow).GetMethod("RestoreCharacterContext", PrivateInstance).Invoke(window, null);
        Assert.That(typeof(ActionEditorWindow).GetField("_previewCharacter", PrivateInstance).GetValue(window), Is.Null);
    }
}
