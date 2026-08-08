using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将 CombatModeProfile 条目中的旧 actionSet 引用改写为直挂 actionGraph，并可选删除 PlayerActionSet 资产。
/// 脚本编译后字段已无 actionSet，故通过 YAML 文本迁移。
/// </summary>
[InitializeOnLoad]
public static class CombatModeActionSetMigrator
{
    const string SessionKey = "ACTGame.CombatModeActionSetMigrated";

    static readonly Regex ActionSetLine = new Regex(
        @"(?m)^(\s*)actionSet:\s*\{fileID:\s*(\d+),\s*guid:\s*([a-fA-F0-9]+),\s*type:\s*2\}",
        RegexOptions.Compiled);

    static CombatModeActionSetMigrator()
    {
        EditorApplication.delayCall += AutoMigrateOnce;
    }

    static void AutoMigrateOnce()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        int count = MigrateAll(deleteOrphanSets: false, silent: true);
        SessionState.SetBool(SessionKey, true);
        if (count > 0)
            Debug.Log($"CombatModeActionSetMigrator: 已自动迁移 {count} 处 actionSet → actionGraph。");
    }

    [MenuItem("ACTGame/Combat/Migrate ActionSet To Mode Graph")]
    static void MigrateMenu()
    {
        int count = MigrateAll(deleteOrphanSets: false, silent: false);
        EditorUtility.DisplayDialog(
            "CombatMode Migration",
            count > 0
                ? $"已迁移 {count} 处 actionSet → actionGraph。\n可再执行 Delete Orphan PlayerActionSet Assets 清理空壳。"
                : "未发现需要迁移的 actionSet 字段（可能已完成）。",
            "OK");
    }

    [MenuItem("ACTGame/Combat/Delete Orphan PlayerActionSet Assets")]
    static void DeleteOrphanSetsMenu()
    {
        // 类已删除后，残留资产脚本丢失；按文件名/YAML 中 actionGraph 字段识别旧 Set
        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Data" });
        int deleted = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                continue;

            string text = File.ReadAllText(path);
            // 旧 PlayerActionSet 仅含 actionGraph 字段且无 nodes/entries 战斗图结构
            bool looksLikeActionSet =
                text.Contains("m_Name:")
                && text.Contains("\n  actionGraph:")
                && !text.Contains("\n  entries:")
                && !text.Contains("\n  nodes:")
                && !text.Contains("\n  defaultMode:");

            if (!looksLikeActionSet)
                continue;

            // 确认没有任何 CombatMode 仍引用此 guid 作为 actionSet
            if (IsGuidStillReferencedAsActionSet(guid))
            {
                Debug.LogWarning($"跳过仍被 actionSet 引用的资产：{path}（请先 Migrate）。");
                continue;
            }

            if (AssetDatabase.DeleteAsset(path))
                deleted++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Delete Orphan ActionSet",
            deleted > 0 ? $"已删除 {deleted} 个疑似 PlayerActionSet 资产。" : "未找到可删的孤儿 ActionSet。",
            "OK");
    }

    /// <summary>扫描并改写所有仍含 actionSet 行的 CombatModeProfile YAML。</summary>
    public static int MigrateAll(bool deleteOrphanSets, bool silent)
    {
        string[] profileGuids = AssetDatabase.FindAssets("t:CombatModeProfile");
        int migratedFields = 0;

        foreach (string profileGuid in profileGuids)
        {
            string profilePath = AssetDatabase.GUIDToAssetPath(profileGuid);
            if (string.IsNullOrEmpty(profilePath))
                continue;

            string yaml = File.ReadAllText(profilePath);
            if (!yaml.Contains("actionSet:"))
                continue;

            string updated = ActionSetLine.Replace(yaml, match =>
            {
                string indent = match.Groups[1].Value;
                string fileId = match.Groups[2].Value;
                string setGuid = match.Groups[3].Value;
                if (fileId == "0" || setGuid.Length < 8)
                    return $"{indent}actionGraph: {{fileID: 0}}";

                string setPath = AssetDatabase.GUIDToAssetPath(setGuid);
                if (string.IsNullOrEmpty(setPath) || !File.Exists(setPath))
                {
                    Debug.LogError($"CombatModeActionSetMigrator: 找不到 ActionSet {setGuid}（{profilePath}）。");
                    return match.Value;
                }

                if (!TryReadActionGraphGuid(setPath, out string graphGuid, out string graphFileId))
                {
                    Debug.LogError($"CombatModeActionSetMigrator: {setPath} 无 actionGraph 引用。");
                    return match.Value;
                }

                migratedFields++;
                return $"{indent}actionGraph: {{fileID: {graphFileId}, guid: {graphGuid}, type: 2}}";
            });

            if (!ReferenceEquals(updated, yaml) && updated != yaml)
            {
                File.WriteAllText(profilePath, updated);
                if (!silent)
                    Debug.Log($"CombatModeActionSetMigrator: 已更新 {profilePath}");
            }
        }

        if (migratedFields > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        if (deleteOrphanSets)
            DeleteOrphanSetsMenu();

        return migratedFields;
    }

    static bool TryReadActionGraphGuid(string actionSetPath, out string graphGuid, out string fileId)
    {
        graphGuid = null;
        fileId = "11400000";
        string text = File.ReadAllText(actionSetPath);
        Match m = Regex.Match(
            text,
            @"actionGraph:\s*\{fileID:\s*(\d+),\s*guid:\s*([a-fA-F0-9]+),\s*type:\s*2\}");
        if (!m.Success)
            return false;

        fileId = m.Groups[1].Value;
        graphGuid = m.Groups[2].Value;
        return !string.IsNullOrEmpty(graphGuid);
    }

    static bool IsGuidStillReferencedAsActionSet(string guid)
    {
        string[] profileGuids = AssetDatabase.FindAssets("t:CombatModeProfile");
        foreach (string profileGuid in profileGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(profileGuid);
            if (string.IsNullOrEmpty(path))
                continue;

            string text = File.ReadAllText(path);
            if (text.Contains($"actionSet:") && text.Contains(guid))
                return true;
        }

        return false;
    }
}
