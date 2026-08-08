using UnityEditor;
using UnityEngine;

/// <summary>将现有 GameplayIntentProfile 迁到 Resources 全局路径，供打包与正式加载。</summary>
public static class GameplayIntentProfileMigrator
{
    const string TargetFolder = "Assets/Resources/ACT";
    const string TargetAssetPath = TargetFolder + "/GameplayIntentProfile.asset";

    [MenuItem("ACTGame/Input/Migrate Intent Profile To Resources")]
    static void MigrateToResources()
    {
        string[] guids = AssetDatabase.FindAssets("t:GameplayIntentProfile");
        if (guids == null || guids.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "GameplayIntentProfile",
                "项目中未找到任何 GameplayIntentProfile 资产。",
                "OK");
            return;
        }

        string sourcePath = AssetDatabase.GUIDToAssetPath(guids[0]);
        if (guids.Length > 1)
        {
            bool proceed = EditorUtility.DisplayDialog(
                "GameplayIntentProfile",
                $"找到 {guids.Length} 个 Intent 资产，将迁移：\n{sourcePath}\n\n其余副本请手动删除以免歧义。",
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
            GameplayIntentSettings.ClearCache();
            EditorUtility.DisplayDialog("GameplayIntentProfile", "已在目标路径，无需迁移。", "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameplayIntentProfile>(TargetAssetPath) != null)
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "GameplayIntentProfile",
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
            // Move 失败时复制再提示删源
            if (!AssetDatabase.CopyAsset(sourcePath, TargetAssetPath))
            {
                EditorUtility.DisplayDialog("GameplayIntentProfile", "迁移失败：\n" + error, "OK");
                return;
            }

            Debug.LogWarning(
                $"GameplayIntentProfileMigrator: Move 失败（{error}），已 Copy 到 {TargetAssetPath}。请手动删除旧文件 {sourcePath}。");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        GameplayIntentSettings.ClearCache();
        EditorUtility.DisplayDialog(
            "GameplayIntentProfile",
            $"已就绪：\n{TargetAssetPath}\n\nCharacterConfig 上的 Intent 槽位已废弃，可忽略残留序列化。",
            "OK");
    }
}
