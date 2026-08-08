using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>将现有 InputActionAsset 迁到 Resources 全局路径。</summary>
public static class GameInputActionsMigrator
{
    const string TargetFolder = "Assets/Resources/ACT";
    const string TargetAssetPath = TargetFolder + "/GameInputActions.inputactions";

    [MenuItem("ACTGame/Input/Migrate Input Actions To Resources")]
    static void MigrateToResources()
    {
        string[] guids = AssetDatabase.FindAssets("t:InputActionAsset");
        if (guids == null || guids.Length == 0)
        {
            EditorUtility.DisplayDialog("InputActionAsset", "项目中未找到 InputActionAsset。", "OK");
            return;
        }

        string sourcePath = AssetDatabase.GUIDToAssetPath(guids[0]);
        if (guids.Length > 1)
        {
            bool proceed = EditorUtility.DisplayDialog(
                "InputActionAsset",
                $"找到 {guids.Length} 个输入资产，将迁移：\n{sourcePath}\n\n其余请手动合并后删除。",
                "继续",
                "取消");
            if (!proceed)
                return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(TargetFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "ACT");

        if (sourcePath == TargetAssetPath)
        {
            GameInputSettings.ClearCache();
            EditorUtility.DisplayDialog("InputActionAsset", "已在目标路径，无需迁移。", "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<InputActionAsset>(TargetAssetPath) != null)
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "InputActionAsset",
                $"目标已存在：\n{TargetAssetPath}\n是否用\n{sourcePath}\n覆盖？",
                "覆盖",
                "取消");
            if (!overwrite)
                return;

            AssetDatabase.DeleteAsset(TargetAssetPath);
        }

        string error = AssetDatabase.MoveAsset(sourcePath, TargetAssetPath);
        if (!string.IsNullOrEmpty(error))
        {
            if (!AssetDatabase.CopyAsset(sourcePath, TargetAssetPath))
            {
                EditorUtility.DisplayDialog("InputActionAsset", "迁移失败：\n" + error, "OK");
                return;
            }

            Debug.LogWarning(
                $"GameInputActionsMigrator: Move 失败（{error}），已 Copy 到 {TargetAssetPath}。请手动删除 {sourcePath}。");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        GameInputSettings.ClearCache();
        EditorUtility.DisplayDialog(
            "InputActionAsset",
            $"已就绪：\n{TargetAssetPath}\n\nCharacterConfig 上的 InputActions 槽位已废弃。",
            "OK");
    }
}
