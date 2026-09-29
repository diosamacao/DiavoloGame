using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>统一执行 Assembly、Architecture 与 Gameplay Content 阻断审计。</summary>
public static class StructureValidationBatch
{
    /// <summary>菜单执行完整只读门禁，不退出 Editor。</summary>
    [MenuItem("ACTGame/Architecture/Validate All Structure And Content")]
    public static void ValidateAllMenu()
    {
        int failures = ValidateAll();
        if (failures == 0)
            Debug.Log("StructureValidation: Assembly、Architecture、Content 全部通过。");
        else
            Debug.LogError($"StructureValidation: 失败项总数={failures}。");
    }

    /// <summary>供 Unity -executeMethod 调用；任一失败均以非零退出码终止 BatchMode。</summary>
    public static void RunAll()
    {
        int failures = ValidateAll();
        if (!Application.isBatchMode)
        {
            Debug.Log($"StructureValidation: 非 BatchMode，仅报告 failures={failures}。");
            return;
        }

        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    /// <summary>聚合所有只读审计并返回失败数；警告不计为失败。</summary>
    public static int ValidateAll()
    {
        int failures = 0;
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string[] structureIssues = StructureAuditRuleSet.AuditProject(projectRoot);
        for (int i = 0; i < structureIssues.Length; i++)
            Debug.LogError($"StructureAudit: {structureIssues[i]}");
        failures += structureIssues.Length;
        failures += ArchitectureBoundaryValidator.AuditBoundaries();

        List<ActionDefinitionAuditEntry> actions = ActionDefinitionAuditUtility.AuditProject();
        for (int i = 0; i < actions.Count; i++)
        {
            ActionDefinitionAuditEntry entry = actions[i];
            ActionDefinition action =
                AssetDatabase.LoadAssetAtPath<ActionDefinition>(entry.AssetPath);
            bool contentValid = action != null && action.ValidateContent(action);
            if (entry.HasError || !contentValid)
            {
                failures++;
                for (int issue = 0; issue < entry.Issues.Count; issue++)
                {
                    ActionDefinitionAuditIssue detail = entry.Issues[issue];
                    if (detail.Severity == ActionDefinitionAuditSeverity.Error)
                    {
                        Debug.LogError(
                            $"ActionAudit: {entry.AssetPath} [{detail.Code}] {detail.Message}",
                            action);
                    }
                }
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:ActionGraph"))
        {
            ActionGraph graph = AssetDatabase.LoadAssetAtPath<ActionGraph>(AssetDatabase.GUIDToAssetPath(guid));
            if (!ActionGraphValidator.ValidateAndLog(graph)) failures++;
        }
        failures += ValidateCharacterConfigs();
        failures += ValidateLocomotionProfiles();
        failures += EnemyBehaviorTreeSetupMenu.AuditProject();
        Debug.Log($"StructureValidation: failures={failures}。");
        return failures;
    }

    /// <summary>深校验全库 CharacterConfig，覆盖 CombatMode、Locomotion 与 Reaction 内容。</summary>
    static int ValidateCharacterConfigs()
    {
        string[] guids = AssetDatabase.FindAssets("t:CharacterConfig");
        Array.Sort(guids, StringComparer.Ordinal);
        int failures = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            CharacterConfig config = AssetDatabase.LoadAssetAtPath<CharacterConfig>(path);
            if (config != null && !config.ValidateGameplayContent(config))
            {
                Debug.LogError($"ContentAudit: CharacterConfig 校验失败：{path}", config);
                failures++;
            }
        }
        return failures;
    }

    /// <summary>校验全库 LocomotionProfile，包括未被当前阵容引用的作者内容。</summary>
    static int ValidateLocomotionProfiles()
    {
        string[] guids = AssetDatabase.FindAssets("t:CharacterLocomotionProfile");
        Array.Sort(guids, StringComparer.Ordinal);
        int failures = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            CharacterLocomotionProfile profile =
                AssetDatabase.LoadAssetAtPath<CharacterLocomotionProfile>(path);
            if (profile != null && !profile.Validate(profile))
            {
                Debug.LogError($"ContentAudit: CharacterLocomotionProfile 校验失败：{path}", profile);
                failures++;
            }
        }
        return failures;
    }
}
