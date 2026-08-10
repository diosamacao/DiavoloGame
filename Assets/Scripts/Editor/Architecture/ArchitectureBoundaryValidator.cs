using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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
    const string CharacterPath = "Assets/Scripts/Domain/Character";
    const string CombatPath = "Assets/Scripts/Domain/Combat";
    const string EnemyPath = "Assets/Scripts/Domain/Enemy";

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
        ValidateLowerLayersDoNotReferenceEnemyTypes();
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

    /// <summary>
    /// Character / Combat 是 Enemy 的下层，禁止源码引用 Enemy 目录声明的具体类型。
    /// 先去掉注释与字符串，避免文档说明或错误文本造成误报。
    /// </summary>
    static void ValidateLowerLayersDoNotReferenceEnemyTypes()
    {
        HashSet<string> enemyTypeNames = CollectTypeNames(EnemyPath);
        ValidateNoTypeReferences(CharacterPath, enemyTypeNames);
        ValidateNoTypeReferences(CombatPath, enemyTypeNames);
    }

    /// <summary>从源码声明收集类型名；即使 Unity 尚未完成编译也可执行边界检查。</summary>
    static HashSet<string> CollectTypeNames(string searchPath)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { searchPath });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
                continue;

            string source = StripCommentsAndStrings(File.ReadAllText(assetPath));
            MatchCollection declarations = Regex.Matches(
                source,
                @"\b(?:class|interface|struct|enum)\s+([A-Za-z_]\w*)");
            foreach (Match declaration in declarations)
                names.Add(declaration.Groups[1].Value);
        }

        return names;
    }

    /// <summary>扫描单个下层目录，报告对禁用具体类型的源码引用。</summary>
    static void ValidateNoTypeReferences(string searchPath, HashSet<string> forbiddenTypeNames)
    {
        string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { searchPath });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
                continue;

            string source = StripCommentsAndStrings(File.ReadAllText(assetPath));
            foreach (string typeName in forbiddenTypeNames)
            {
                if (!Regex.IsMatch(source, $@"\b{Regex.Escape(typeName)}\b"))
                    continue;

                Debug.LogError(
                    $"{assetPath}: {searchPath} 禁止引用 Enemy 类型 {typeName}；请上提共享契约或改为接口注入。");
            }
        }
    }

    /// <summary>移除 C# 注释与字符串字面量，仅保留可参与类型引用判断的源码。</summary>
    static string StripCommentsAndStrings(string source)
    {
        if (string.IsNullOrEmpty(source))
            return string.Empty;

        string withoutBlockComments = Regex.Replace(
            source,
            @"/\*.*?\*/",
            string.Empty,
            RegexOptions.Singleline);
        string withoutLineComments = Regex.Replace(
            withoutBlockComments,
            @"//.*?$",
            string.Empty,
            RegexOptions.Multiline);
        return Regex.Replace(
            withoutLineComments,
            @"""(?:\\.|[^""\\])*""",
            "\"\"");
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
