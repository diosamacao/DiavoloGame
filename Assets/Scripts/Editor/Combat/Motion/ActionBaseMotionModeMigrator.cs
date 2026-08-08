using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Wave 2.5：LegacyResolve 已删除；本工具改为校验 BaseMotionMode，不再写入兼容回退。
/// </summary>
public static class ActionBaseMotionModeMigrator
{
    /// <summary>菜单：校验全库 BaseMotionMode（非法 0 / 模式与数据不一致）。</summary>
    [MenuItem("ACTGame/Action/Validate Base Motion Mode")]
    public static void ValidateMenu()
    {
        string report = ValidateProject();
        Debug.Log(report);
        EditorUtility.DisplayDialog("Validate Base Motion Mode", TrimForDialog(report), "OK");
    }

    /// <summary>扫描并报告非法或未就绪的 BaseMotionMode。</summary>
    public static string ValidateProject()
    {
        string[] guids = AssetDatabase.FindAssets("t:ActionDefinition");
        Array.Sort(guids, StringComparer.Ordinal);

        int ok = 0;
        int legacyResidual = 0;
        int bakedNotReady = 0;
        int scriptedNoWindows = 0;
        var lines = new List<string>(32);

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ActionDefinition action = AssetDatabase.LoadAssetAtPath<ActionDefinition>(path);
            if (action == null)
                continue;

            ActionBaseMotionMode mode = action.ExecutionPolicy.BaseMotionMode;
            bool baked = action.BakedMotion != null && action.BakedMotion.IsReady;
            bool scripted = action.Timeline != null && action.Timeline.HasScriptedMovement;

            if ((int)mode == 0)
            {
                legacyResidual++;
                lines.Add($"LEGACY(0): {action.name} ({path})");
                continue;
            }

            if (mode == ActionBaseMotionMode.BakedMotion && !baked)
            {
                bakedNotReady++;
                lines.Add($"BAKED_NOT_READY: {action.name} ({path})");
                continue;
            }

            if (mode == ActionBaseMotionMode.ScriptedTimeline && !scripted)
            {
                scriptedNoWindows++;
                lines.Add($"SCRIPTED_NO_WINDOWS: {action.name} ({path})");
                continue;
            }

            ok++;
        }

        var sb = new StringBuilder(1024);
        sb.AppendLine("=== Validate Base Motion Mode (Wave 2.5) ===");
        sb.AppendLine(
            $"ok={ok} legacyResidual={legacyResidual} bakedNotReady={bakedNotReady} scriptedNoWindows={scriptedNoWindows}");
        sb.AppendLine();
        sb.AppendLine("--- Issues ---");
        if (lines.Count == 0)
            sb.AppendLine("(none)");
        else
        {
            int limit = Math.Min(lines.Count, 48);
            for (int i = 0; i < limit; i++)
                sb.AppendLine(lines[i]);
            if (lines.Count > limit)
                sb.AppendLine($"… +{lines.Count - limit} more");
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
