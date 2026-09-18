using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>在 CS2B 拆分前把行为树 SerializeReference 类型统一重写到当前粗程序集。</summary>
public static class BehaviorTreeAssemblyMigrationTool
{
    const string LegacyAssemblyToken = "asm: Assembly-CSharp";
    const string CurrentAssemblyToken = "asm: ACTGame.Domain.Gameplay";

    /// <summary>重序列化全部行为树资产，并在保存后拒绝任何仍指向 Assembly-CSharp 的节点。</summary>
    [MenuItem("ACTGame/Architecture/Prepare Behavior Trees For CS2B")]
    public static void PrepareForCs2B()
    {
        string[] paths = CollectBehaviorTreePaths();
        if (paths.Length == 0)
            throw new InvalidOperationException("CS2B: 项目中没有 EnemyBehaviorTreeAsset。");

        // 必须在旧 MovedFrom 仍指向 Assembly-CSharp 时执行，确保旧节点先成功反序列化再落盘。
        for (int i = 0; i < paths.Length; i++)
        {
            EnemyBehaviorTreeAsset asset =
                AssetDatabase.LoadAssetAtPath<EnemyBehaviorTreeAsset>(paths[i]);
            if (asset == null)
                throw new InvalidOperationException($"CS2B: 无法加载行为树资产 '{paths[i]}'。");
            ReassignManagedReferenceRoot(asset);
            EditorUtility.SetDirty(asset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ForceReserializeAssets(
            paths,
            ForceReserializeAssetsOptions.ReserializeAssets);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string[] notReady = CollectNotReadyAssetPaths(paths);
        if (notReady.Length > 0)
        {
            throw new InvalidOperationException(
                "CS2B: 以下行为树未统一到 ACTGame.Domain.Gameplay：\n"
                + string.Join("\n", notReady));
        }

        Debug.Log(
            $"CS2B: 已将 {paths.Length} 份行为树资产统一重序列化到 ACTGame.Domain.Gameplay；"
            + "现在可以继续切换到 ACTGame.Domain.Enemy。");
    }

    /// <summary>只读审计当前行为树资产，返回仍含旧 Assembly-CSharp 类型记录的路径。</summary>
    [MenuItem("ACTGame/Architecture/Audit Behavior Trees For CS2B")]
    public static void AuditForCs2B()
    {
        string[] notReady = CollectNotReadyAssetPaths(CollectBehaviorTreePaths());
        if (notReady.Length == 0)
        {
            Debug.Log("CS2B: 行为树 SerializeReference 已统一到当前程序集。");
            return;
        }

        Debug.LogError(
            "CS2B: 以下行为树尚未迁移：\n" + string.Join("\n", notReady));
    }

    /// <summary>检查 YAML 是否仍含旧程序集记录；供迁移门禁测试复用。</summary>
    public static bool ContainsLegacyAssemblyReference(string yaml) =>
        !string.IsNullOrEmpty(yaml)
        && yaml.IndexOf(LegacyAssemblyToken, StringComparison.Ordinal) >= 0;

    /// <summary>只有不含旧程序集且至少记录一个当前节点类型时，资产才可进入下一次程序集迁移。</summary>
    public static bool IsReadyForCs2B(string yaml) =>
        !ContainsLegacyAssemblyReference(yaml)
        && !string.IsNullOrEmpty(yaml)
        && yaml.IndexOf(CurrentAssemblyToken, StringComparison.Ordinal) >= 0;

    /// <summary>按 GUID 稳定顺序收集全部行为树资产路径。</summary>
    static string[] CollectBehaviorTreePaths()
    {
        string[] guids = AssetDatabase.FindAssets("t:EnemyBehaviorTreeAsset");
        Array.Sort(guids, StringComparer.Ordinal);
        var paths = new string[guids.Length];
        for (int i = 0; i < guids.Length; i++)
            paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
        return paths;
    }

    /// <summary>
    /// 先从 SerializedProperty 脱离旧 managed-reference 注册项，再用同一运行时对象重新登记；
    /// 普通 ForceReserialize 不会改写 MovedFrom 解析出的历史程序集名。
    /// </summary>
    static void ReassignManagedReferenceRoot(EnemyBehaviorTreeAsset asset)
    {
        var serialized = new SerializedObject(asset);
        SerializedProperty rootProperty = serialized.FindProperty("customRoot");
        object root = rootProperty?.managedReferenceValue;
        if (root == null)
        {
            throw new InvalidOperationException(
                $"CS2B: 行为树 '{asset.name}' 的 customRoot 无法反序列化，已停止迁移。");
        }

        rootProperty.managedReferenceValue = null;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        serialized.Update();

        rootProperty = serialized.FindProperty("customRoot");
        rootProperty.managedReferenceValue = root;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        asset.PrepareForGraphEditor();
    }

    /// <summary>读取序列化正文并返回仍未迁移的资产，不依赖对象加载后的内存表现。</summary>
    static string[] CollectNotReadyAssetPaths(IReadOnlyList<string> paths)
    {
        var legacy = new List<string>();
        for (int i = 0; i < paths.Count; i++)
        {
            string absolutePath = Path.GetFullPath(paths[i]);
            string yaml = File.ReadAllText(absolutePath);
            if (!IsReadyForCs2B(yaml))
                legacy.Add(paths[i]);
        }

        return legacy.ToArray();
    }
}
