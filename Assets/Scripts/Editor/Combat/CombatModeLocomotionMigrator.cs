using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将 AnimationProfile 写入 LocomotionProfile，并把 CombatMode 条目从挂 Anim 改为挂 LocomotionProfile。
/// 数据来自 CharacterConfig 上残留的 defaultLocomotionProfile + locomotionProfile 配对。
/// </summary>
[InitializeOnLoad]
public static class CombatModeLocomotionMigrator
{
    const string SessionKey = "ACTGame.CombatModeLocomotionMerged";

    static readonly Regex DefaultAnim = new Regex(
        @"defaultLocomotionProfile:\s*\{fileID:\s*\d+,\s*guid:\s*([a-fA-F0-9]+),\s*type:\s*2\}",
        RegexOptions.Compiled);
    static readonly Regex ConfigLoco = new Regex(
        @"locomotionProfile:\s*\{fileID:\s*\d+,\s*guid:\s*([a-fA-F0-9]+),\s*type:\s*2\}",
        RegexOptions.Compiled);
    static readonly Regex CombatProfile = new Regex(
        @"combatProfile:\s*\{fileID:\s*\d+,\s*guid:\s*([a-fA-F0-9]+),\s*type:\s*2\}",
        RegexOptions.Compiled);

    static CombatModeLocomotionMigrator()
    {
        EditorApplication.delayCall += AutoMigrateOnce;
    }

    static void AutoMigrateOnce()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        int n = MigrateAll(silent: true);
        SessionState.SetBool(SessionKey, true);
        if (n > 0)
            Debug.Log($"CombatModeLocomotionMigrator: 已处理 {n} 项（Anim→Loco / Mode 挂 Loco）。");
    }

    [MenuItem("ACTGame/Combat/Migrate Animation Into Locomotion Profile")]
    static void MigrateMenu()
    {
        int n = MigrateAll(silent: false);
        EditorUtility.DisplayDialog(
            "Locomotion Migration",
            n > 0
                ? $"已更新 {n} 处。请检查 LocomotionProfile.Animation Profile 与 CombatMode 条目。"
                : "无需迁移或未找到 Config 中的 Anim/Loco 配对。",
            "OK");
    }

    /// <summary>执行迁移；返回改写次数。</summary>
    public static int MigrateAll(bool silent)
    {
        int changes = 0;
        var combatToLoco = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string[] configGuids = AssetDatabase.FindAssets("t:CharacterConfig");
        foreach (string configGuid in configGuids)
        {
            string configPath = AssetDatabase.GUIDToAssetPath(configGuid);
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
                continue;

            string yaml = File.ReadAllText(configPath);
            Match animMatch = DefaultAnim.Match(yaml);
            Match locoMatch = ConfigLoco.Match(yaml);
            Match combatMatch = CombatProfile.Match(yaml);
            if (!locoMatch.Success)
                continue;

            string locoGuid = locoMatch.Groups[1].Value;
            string locoPath = AssetDatabase.GUIDToAssetPath(locoGuid);
            if (string.IsNullOrEmpty(locoPath) || !File.Exists(locoPath))
                continue;

            if (animMatch.Success)
            {
                string animGuid = animMatch.Groups[1].Value;
                if (InjectAnimationProfile(locoPath, animGuid))
                {
                    changes++;
                    if (!silent)
                        Debug.Log($"Locomotion←Anim: {locoPath} ← {animGuid}");
                }
            }

            if (combatMatch.Success)
                combatToLoco[combatMatch.Groups[1].Value] = locoGuid;
        }

        string[] modeGuids = AssetDatabase.FindAssets("t:CombatModeProfile");
        foreach (string modeGuid in modeGuids)
        {
            string modePath = AssetDatabase.GUIDToAssetPath(modeGuid);
            if (string.IsNullOrEmpty(modePath) || !File.Exists(modePath))
                continue;

            if (!combatToLoco.TryGetValue(modeGuid, out string locoGuid))
                continue;

            string modeYaml = File.ReadAllText(modePath);
            string updated = ReplaceModeEntryProfileWithLoco(modeYaml, locoGuid, out int replaced);
            if (replaced <= 0 || updated == modeYaml)
                continue;

            File.WriteAllText(modePath, updated);
            changes += replaced;
            if (!silent)
                Debug.Log($"CombatMode→Loco: {modePath} ×{replaced}");
        }

        if (changes > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        return changes;
    }

    static bool InjectAnimationProfile(string locoPath, string animGuid)
    {
        string yaml = File.ReadAllText(locoPath);
        if (Regex.IsMatch(yaml, @"animationProfile:\s*\{fileID:\s*\d+,\s*guid:\s*[a-fA-F0-9]+"))
            return false;

        // 插在 m_EditorClassIdentifier 后或第一个 Header 字段前
        const string marker = "m_EditorClassIdentifier: ";
        int idx = yaml.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
            return false;

        int lineEnd = yaml.IndexOf('\n', idx);
        if (lineEnd < 0)
            return false;

        string inject =
            "\n  animationProfile: {fileID: 11400000, guid: " + animGuid + ", type: 2}";
        string updated = yaml.Insert(lineEnd, inject);
        File.WriteAllText(locoPath, updated);
        return true;
    }

    /// <summary>把条目上的 animationProfile / 旧 locomotionProfile(Anim) 换成 LocomotionProfile 引用。</summary>
    static string ReplaceModeEntryProfileWithLoco(string yaml, string locoGuid, out int replaced)
    {
        // lambda 不能捕获 out 参数，用局部计数再写回
        int count = 0;
        string locoLine =
            "locomotionProfile: {fileID: 11400000, guid: " + locoGuid + ", type: 2}";

        string result = Regex.Replace(
            yaml,
            @"(?m)^(\s*)animationProfile:\s*\{fileID:\s*\d+,\s*guid:\s*[a-fA-F0-9]+,\s*type:\s*2\}",
            m =>
            {
                count++;
                return m.Groups[1].Value + locoLine;
            });

        // 仍写着 locomotionProfile 但指向 Anim 时：若 guid 不是 locoGuid 则替换
        result = Regex.Replace(
            result,
            @"(?m)^(\s*)locomotionProfile:\s*\{fileID:\s*\d+,\s*guid:\s*([a-fA-F0-9]+),\s*type:\s*2\}",
            m =>
            {
                string guid = m.Groups[2].Value;
                if (string.Equals(guid, locoGuid, StringComparison.OrdinalIgnoreCase))
                    return m.Value;

                count++;
                return m.Groups[1].Value + locoLine;
            });

        replaced = count;
        return result;
    }
}
