using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>批量规范所选 FBX 内所有动画片段的 Root Transform Rotation 与 Position Y 导入设置。</summary>
public static class SelectedFbxRootTransformImporter
{
    const string MenuPath = "ACTGame/Animation/Set Selected FBX Root Transform to Original";

    /// <summary>将所选 FBX 的 Rotation/Y 烘焙进姿势，并将两项 Based Upon 设置为 Original。</summary>
    [MenuItem(MenuPath)]
    public static void ApplyToSelectedFbx()
    {
        List<string> assetPaths = CollectSelectedFbxPaths();
        int updatedCount = 0;
        int failedCount = 0;

        try
        {
            for (int i = 0; i < assetPaths.Count; i++)
            {
                string assetPath = assetPaths[i];
                EditorUtility.DisplayProgressBar(
                    "设置 FBX Root Transform",
                    assetPath,
                    (float)i / assetPaths.Count);

                try
                {
                    if (ApplySettings(assetPath))
                        updatedCount++;
                }
                catch (Exception exception)
                {
                    failedCount++;
                    Debug.LogError($"设置 FBX Root Transform 失败：{assetPath}\n{exception}");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();
        Debug.Log(
            $"FBX Root Transform 批处理完成：选中 {assetPaths.Count} 个，更新 {updatedCount} 个，失败 {failedCount} 个。");
    }

    /// <summary>仅当 Project 窗口当前选择中包含 FBX 时启用菜单。</summary>
    [MenuItem(MenuPath, true)]
    public static bool ValidateSelection() => CollectSelectedFbxPaths().Count > 0;

    /// <summary>收集并去重当前选择对应的 FBX 资产路径；文件夹和其他资产会被忽略。</summary>
    static List<string> CollectSelectedFbxPaths()
    {
        var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (UnityEngine.Object selectedObject in Selection.objects)
        {
            string assetPath = AssetDatabase.GetAssetPath(selectedObject);
            if (!string.IsNullOrEmpty(assetPath) &&
                assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                uniquePaths.Add(assetPath);
            }
        }

        var paths = new List<string>(uniquePaths);
        paths.Sort(StringComparer.OrdinalIgnoreCase);
        return paths;
    }

    /// <summary>修改一个 FBX 的全部片段；设置已有时不触发重复导入。</summary>
    static bool ApplySettings(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException("所选资产不是可用的 ModelImporter。");

        // 首次自定义导入时 clipAnimations 为空，必须从默认片段开始写回，否则会丢失片段定义。
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        bool changed = false;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            if (!clip.lockRootRotation ||
                !clip.keepOriginalOrientation ||
                !clip.lockRootHeightY ||
                !clip.keepOriginalPositionY)
            {
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
                changed = true;
            }
        }

        if (!changed)
            return false;

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        return true;
    }
}
