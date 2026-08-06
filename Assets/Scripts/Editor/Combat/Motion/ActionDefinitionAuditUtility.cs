using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Wave 0：扫描全库 ActionDefinition，归类位移源并报告 Hz / 帧越界等问题（不改 Runtime、不改资产）。
/// </summary>
public static class ActionDefinitionAuditUtility
{
    /// <summary>审计单个 Action。</summary>
    public static ActionDefinitionAuditEntry Audit(ActionDefinition action, string assetPath = null)
    {
        var entry = new ActionDefinitionAuditEntry();
        if (action == null)
        {
            entry.ActionName = "(null)";
            entry.AssetPath = assetPath ?? string.Empty;
            entry.AddIssue(ActionDefinitionAuditSeverity.Error, "NULL_ACTION", "ActionDefinition 为空。");
            return entry;
        }

        entry.ActionName = action.name;
        entry.AssetPath = assetPath ?? AssetDatabase.GetAssetPath(action);
        entry.SampleRate = action.SampleRate;
        entry.TotalFrames = action.TotalFrames;
        entry.UseRootMotion = action.ExecutionPolicy.UseRootMotion;

        ActionBakedMotion baked = action.BakedMotion;
        entry.BakedReady = baked != null && baked.IsReady;
        entry.BakedLogicHz = baked != null ? baked.logicHz : 0;
        entry.BakedFrameCount = baked != null ? baked.frameCount : 0;
        entry.HasScriptedMovement = action.Timeline != null && action.Timeline.HasScriptedMovement;
        entry.MotionSourceKind = ActionMotionSourceClassifier.Classify(
            entry.BakedReady,
            entry.HasScriptedMovement);

        CollectIssues(action, entry);
        return entry;
    }

    /// <summary>扫描工程内全部 ActionDefinition。</summary>
    public static List<ActionDefinitionAuditEntry> AuditProject()
    {
        string[] guids = AssetDatabase.FindAssets("t:ActionDefinition");
        Array.Sort(guids, StringComparer.Ordinal);
        var results = new List<ActionDefinitionAuditEntry>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ActionDefinition action = AssetDatabase.LoadAssetAtPath<ActionDefinition>(path);
            if (action == null)
                continue;
            results.Add(Audit(action, path));
        }

        return results;
    }

    /// <summary>生成可读报告文本。</summary>
    public static string BuildReport(IReadOnlyList<ActionDefinitionAuditEntry> entries)
    {
        int baked = 0;
        int scripted = 0;
        int none = 0;
        int conflict = 0;
        int errorEntries = 0;
        var sb = new StringBuilder(2048);

        for (int i = 0; i < entries.Count; i++)
        {
            ActionDefinitionAuditEntry e = entries[i];
            switch (e.MotionSourceKind)
            {
                case ActionMotionSourceKind.Baked: baked++; break;
                case ActionMotionSourceKind.Scripted: scripted++; break;
                case ActionMotionSourceKind.Conflict: conflict++; break;
                default: none++; break;
            }

            if (e.HasError)
                errorEntries++;
        }

        sb.AppendLine("=== Action Motion Source Audit (Wave 0) ===");
        sb.AppendLine(
            $"total={entries.Count} baked={baked} scripted={scripted} none={none} conflict={conflict} entriesWithError={errorEntries}");
        sb.AppendLine();

        AppendSection(sb, entries, ActionMotionSourceKind.Conflict, "CONFLICT");
        AppendSection(sb, entries, ActionMotionSourceKind.Baked, "BAKED");
        AppendSection(sb, entries, ActionMotionSourceKind.Scripted, "SCRIPTED");
        AppendSection(sb, entries, ActionMotionSourceKind.None, "NONE");

        return sb.ToString();
    }

    /// <summary>菜单：校验全库并打印到 Console。</summary>
    [MenuItem("ACTGame/Action/Validate Motion Sources")]
    public static void ValidateMotionSourcesMenu()
    {
        List<ActionDefinitionAuditEntry> entries = AuditProject();
        string report = BuildReport(entries);
        Debug.Log(report);
        ActionDefinitionAuditWindow.ShowReport(report, entries);
    }

    static void CollectIssues(ActionDefinition action, ActionDefinitionAuditEntry entry)
    {
        if (entry.MotionSourceKind == ActionMotionSourceKind.Conflict)
        {
            entry.AddIssue(
                ActionDefinitionAuditSeverity.Error,
                "MOTION_SOURCE_CONFLICT",
                "烘焙运动表与 Timeline Movement 窗口并存。");
        }

        if (entry.UseRootMotion && !entry.BakedReady)
        {
            entry.AddIssue(
                ActionDefinitionAuditSeverity.Warning,
                "USE_ROOT_MOTION_UNBAKED",
                "UseRootMotion=true 且烘焙未就绪，Runtime 仍可能走 Animator RM 回退。");
        }

        ActionBaseMotionMode mode = action.ExecutionPolicy.BaseMotionMode;
        if (mode == ActionBaseMotionMode.LegacyResolve)
        {
            entry.AddIssue(
                ActionDefinitionAuditSeverity.Info,
                "BASE_MOTION_LEGACY",
                "BaseMotionMode=LegacyResolve，请跑 Migrate Base Motion Mode。");
        }

        if (entry.BakedReady
            && action.BakedMotion.planarMode == ActionMotionPlanarMode.ForwardOnly)
        {
            entry.AddIssue(
                ActionDefinitionAuditSeverity.Warning,
                "FORWARD_ONLY_NEEDS_REBAKE",
                "planarMode=ForwardOnly（旧保模长语义）。直线连击请改 ForwardSigned 后重烘焙。");
        }

        if (mode == ActionBaseMotionMode.BakedMotion && !entry.BakedReady)
        {
            entry.AddIssue(
                ActionDefinitionAuditSeverity.Error,
                "BAKED_MODE_NOT_READY",
                "BaseMotionMode=BakedMotion 但运动表未就绪。");
        }

        if (mode == ActionBaseMotionMode.ScriptedTimeline && !entry.HasScriptedMovement)
        {
            entry.AddIssue(
                ActionDefinitionAuditSeverity.Warning,
                "SCRIPTED_MODE_NO_WINDOWS",
                "BaseMotionMode=ScriptedTimeline 但无 Movement 窗口。");
        }

        if (entry.SampleRate != ActionSim.LogicHz)
        {
            entry.AddIssue(
                ActionDefinitionAuditSeverity.Error,
                "SAMPLE_RATE_NOT_60",
                $"sampleRate={entry.SampleRate}，期望 {ActionSim.LogicHz}。");
        }

        if (entry.BakedReady)
        {
            if (entry.BakedLogicHz != ActionSim.LogicHz)
            {
                entry.AddIssue(
                    ActionDefinitionAuditSeverity.Error,
                    "BAKED_HZ_MISMATCH",
                    $"bakedMotion.logicHz={entry.BakedLogicHz}，期望 {ActionSim.LogicHz}。");
            }

            if (entry.TotalFrames > 0 && entry.BakedFrameCount != entry.TotalFrames)
            {
                entry.AddIssue(
                    ActionDefinitionAuditSeverity.Error,
                    "BAKED_FRAME_COUNT_MISMATCH",
                    $"bakedMotion.frameCount={entry.BakedFrameCount}，TotalFrames={entry.TotalFrames}。");
            }
        }

        CollectTimelineBoundsIssues(action, entry);
    }

    static void CollectTimelineBoundsIssues(ActionDefinition action, ActionDefinitionAuditEntry entry)
    {
        int totalFrames = entry.TotalFrames;
        if (totalFrames <= 0)
        {
            if (action.HasAnimation)
            {
                entry.AddIssue(
                    ActionDefinitionAuditSeverity.Warning,
                    "TOTAL_FRAMES_ZERO",
                    "有动画段但 TotalFrames<=0。");
            }

            return;
        }

        int maxFrame = totalFrames - 1;
        ActionTimeline timeline = action.Timeline;
        foreach (ActionNotifyState state in timeline.EnumerateStates())
        {
            if (state == null)
                continue;
            if (state.StartFrame < 0
                || state.EndFrame < state.StartFrame
                || state.EndFrame > maxFrame)
            {
                entry.AddIssue(
                    ActionDefinitionAuditSeverity.Error,
                    "TIMELINE_STATE_OOB",
                    $"{state.GetType().Name} '{state.Id}' 帧 [{state.StartFrame},{state.EndFrame}] 越界（合法 0..{maxFrame}）。");
            }
        }

        foreach (ActionNotify notify in timeline.EnumerateNotifies())
        {
            if (notify == null)
                continue;
            if (notify.StartFrame < 0 || notify.StartFrame > maxFrame)
            {
                entry.AddIssue(
                    ActionDefinitionAuditSeverity.Error,
                    "TIMELINE_NOTIFY_OOB",
                    $"{notify.GetType().Name} '{notify.Id}' 帧 {notify.StartFrame} 越界（合法 0..{maxFrame}）。");
            }
        }
    }

    static void AppendSection(
        StringBuilder sb,
        IReadOnlyList<ActionDefinitionAuditEntry> entries,
        ActionMotionSourceKind kind,
        string title)
    {
        sb.AppendLine($"--- {title} ---");
        bool any = false;
        for (int i = 0; i < entries.Count; i++)
        {
            ActionDefinitionAuditEntry e = entries[i];
            if (e.MotionSourceKind != kind)
                continue;
            any = true;
            sb.AppendLine(
                $"{e.ActionName} | {e.AssetPath} | RM={e.UseRootMotion} baked={e.BakedReady} scripted={e.HasScriptedMovement} frames={e.TotalFrames}");
            for (int j = 0; j < e.Issues.Count; j++)
            {
                ActionDefinitionAuditIssue issue = e.Issues[j];
                sb.AppendLine($"  [{issue.Severity}] {issue.Code}: {issue.Message}");
            }
        }

        if (!any)
            sb.AppendLine("(none)");
        sb.AppendLine();
    }
}
