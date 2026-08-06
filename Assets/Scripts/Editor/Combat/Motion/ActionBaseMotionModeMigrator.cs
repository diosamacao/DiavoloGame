using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Wave 1：把无冲突 Action 的 BaseMotionMode 从 LegacyResolve 迁到 Baked/Scripted/None。
/// Conflict（表+Movement 并存）只报告，不自动写入。
/// </summary>
public static class ActionBaseMotionModeMigrator
{
    /// <summary>菜单：迁移无冲突资产并打印报告。</summary>
    [MenuItem("ACTGame/Action/Migrate Base Motion Mode")]
    public static void MigrateMenu()
    {
        string report = MigrateProject(dryRun: false);
        Debug.Log(report);
        EditorUtility.DisplayDialog("Migrate Base Motion Mode", TrimForDialog(report), "OK");
    }

    /// <summary>菜单：仅报告将如何迁移，不写资产。</summary>
    [MenuItem("ACTGame/Action/Dry-Run Base Motion Mode Migration")]
    public static void DryRunMenu()
    {
        string report = MigrateProject(dryRun: true);
        Debug.Log(report);
        EditorUtility.DisplayDialog("Dry-Run Base Motion Mode", TrimForDialog(report), "OK");
    }

    /// <summary>扫描并迁移（或 dry-run）。</summary>
    public static string MigrateProject(bool dryRun)
    {
        string[] guids = AssetDatabase.FindAssets("t:ActionDefinition");
        Array.Sort(guids, StringComparer.Ordinal);

        int migrated = 0;
        int skippedLegacyOk = 0;
        int conflict = 0;
        int alreadySet = 0;
        var conflictLines = new List<string>(16);
        var migratedLines = new List<string>(32);

        if (!dryRun)
            AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ActionDefinition action = AssetDatabase.LoadAssetAtPath<ActionDefinition>(path);
                if (action == null)
                    continue;

                ActionExecutionPolicy policy = action.ExecutionPolicy;
                if (policy.BaseMotionMode != ActionBaseMotionMode.LegacyResolve)
                {
                    alreadySet++;
                    continue;
                }

                bool baked = action.BakedMotion != null && action.BakedMotion.IsReady;
                bool scripted = action.Timeline != null && action.Timeline.HasScriptedMovement;
                ActionMotionSourceKind kind = ActionMotionSourceClassifier.Classify(baked, scripted);
                if (kind == ActionMotionSourceKind.Conflict)
                {
                    conflict++;
                    if (conflictLines.Count < 24)
                        conflictLines.Add($"CONFLICT: {action.name} ({path})");
                    continue;
                }

                ActionBaseMotionMode target = kind switch
                {
                    ActionMotionSourceKind.Baked => ActionBaseMotionMode.BakedMotion,
                    ActionMotionSourceKind.Scripted => ActionBaseMotionMode.ScriptedTimeline,
                    _ => ActionBaseMotionMode.None,
                };

                migratedLines.Add($"{action.name}: LegacyResolve → {target}");
                if (!dryRun)
                {
                    // 经 SerializedObject 写嵌套字段，避免 ExecutionPolicy getter 临时实例丢改动
                    var so = new SerializedObject(action);
                    SerializedProperty modeProp = so.FindProperty("executionPolicy")
                        ?.FindPropertyRelative("baseMotionMode");
                    if (modeProp != null)
                    {
                        modeProp.enumValueIndex = (int)target;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(action);
                    }
                }

                migrated++;
            }
        }
        finally
        {
            if (!dryRun)
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }
        }

        var sb = new StringBuilder(1024);
        sb.AppendLine(dryRun
            ? "=== Dry-Run Base Motion Mode Migration ==="
            : "=== Base Motion Mode Migration ===");
        sb.AppendLine(
            $"migrated={migrated} alreadySet={alreadySet} conflictSkipped={conflict} unchangedLegacyOk={skippedLegacyOk}");
        sb.AppendLine();
        sb.AppendLine("--- Migrated / Would migrate ---");
        if (migratedLines.Count == 0)
            sb.AppendLine("(none)");
        else
        {
            int limit = Math.Min(migratedLines.Count, 40);
            for (int i = 0; i < limit; i++)
                sb.AppendLine(migratedLines[i]);
            if (migratedLines.Count > limit)
                sb.AppendLine($"… +{migratedLines.Count - limit} more");
        }

        sb.AppendLine();
        sb.AppendLine("--- Conflicts (manual decision required) ---");
        if (conflictLines.Count == 0)
            sb.AppendLine("(none)");
        else
        {
            for (int i = 0; i < conflictLines.Count; i++)
                sb.AppendLine(conflictLines[i]);
            if (conflict > conflictLines.Count)
                sb.AppendLine($"… +{conflict - conflictLines.Count} more");
        }

        return sb.ToString();
    }

    static string TrimForDialog(string report)
    {
        if (string.IsNullOrEmpty(report))
            return string.Empty;
        return report.Length <= 1500 ? report : report.Substring(0, 1500) + "\n…";
    }
}
