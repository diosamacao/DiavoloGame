using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>编辑器架构边界校验：把目录/后缀规范转换为 Unity Console 错误。</summary>
[InitializeOnLoad]
public static class ArchitectureBoundaryValidator
{
    const string AppSystemsPath = "Assets/Scripts/App/Systems";
    const string AppControllersPath = "Assets/Scripts/App/Controllers";
    const string AppEventsPath = "Assets/Scripts/App/Events";
    const string DomainPath = "Assets/Scripts/Domain";

    static ArchitectureBoundaryValidator()
    {
        EditorApplication.delayCall += Validate;
    }

    /// <summary>手动执行架构边界校验，便于迁移阶段在 Unity 菜单中复查。</summary>
    [MenuItem("ACTGame/Architecture/Validate Boundaries")]
    public static void Validate()
    {
        ValidateScripts(AppSystemsPath, "System", typeof(IArchitectureSystem));
        ValidateControllers();
        ValidateScripts(AppEventsPath, "Event", typeof(IArchitectureEvent));
        ValidateDomainDoesNotUseArchitectureSingleton();
    }

    static void ValidateScripts(string searchPath, string suffix, Type requiredInterface)
    {
        foreach (Type type in LoadTypes(searchPath))
        {
            if (!type.Name.EndsWith(suffix, StringComparison.Ordinal))
                continue;

            if (!requiredInterface.IsAssignableFrom(type))
                Debug.LogError($"{type.Name}: {searchPath} 下的 *{suffix} 必须实现 {requiredInterface.Name}。");
        }
    }

    static void ValidateControllers()
    {
        foreach (Type type in LoadTypes(AppControllersPath))
        {
            if (!typeof(MonoBehaviour).IsAssignableFrom(type))
                continue;

            if (!typeof(IArchitectureController).IsAssignableFrom(type))
                Debug.LogError($"{type.Name}: App/Controllers 下的 MonoBehaviour 必须实现 IArchitectureController。");
        }
    }

    static void ValidateDomainDoesNotUseArchitectureSingleton()
    {
        string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { DomainPath });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
                continue;

            string source = File.ReadAllText(assetPath);
            if (source.Contains("ACTGameArchitecture.Interface"))
                Debug.LogError($"{assetPath}: Domain 层禁止直接访问 ACTGameArchitecture.Interface。");
        }
    }

    static Type[] LoadTypes(string searchPath)
    {
        string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { searchPath });
        var types = new System.Collections.Generic.List<Type>(guids.Length);
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
            Type type = script != null ? script.GetClass() : null;
            if (type != null)
                types.Add(type);
        }

        return types.ToArray();
    }
}
