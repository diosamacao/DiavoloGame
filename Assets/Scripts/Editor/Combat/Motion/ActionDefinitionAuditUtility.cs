using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

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
        entry.BaseMotionMode = action.ExecutionPolicy.BaseMotionMode;

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

        ActionBaseMotionMode mode = entry.BaseMotionMode;
        // 0 = 已删除的 LegacyResolve；序列化残留时按非法模式报错
        if ((int)mode == 0)
        {
            entry.AddIssue(
                ActionDefinitionAuditSeverity.Error,
                "BASE_MOTION_LEGACY_RESIDUAL",
                "BaseMotionMode 仍为已删除的 LegacyResolve(0)，请设为 None/Baked/Scripted。");
        }

        if (entry.BakedReady && (int)action.BakedMotion.planarMode == 1)
        {
            entry.AddIssue(
                ActionDefinitionAuditSeverity.Error,
                "FORWARD_ONLY_RESIDUAL",
                "planarMode 仍为已删除的 ForwardOnly(1)，请改 ForwardSigned/FullPlanar 后重烘焙。");
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
            CollectStateBoundsIssue(state, maxFrame, entry);
        }

        // Camera 窗故意不进 EnumerateStates/Sim Runner，但资产审计仍必须覆盖。
        foreach (CameraShotNotifyState state in timeline.CameraShotStates)
        {
            CollectStateBoundsIssue(state, maxFrame, entry);
            CollectCameraSplineIssues(state, entry);
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

    /// <summary>检查一个区间窗口是否超出动作闭区间帧范围。</summary>
    static void CollectStateBoundsIssue(
        ActionNotifyState state,
        int maxFrame,
        ActionDefinitionAuditEntry entry)
    {
        if (state == null)
            return;
        if (state.StartFrame >= 0
            && state.EndFrame >= state.StartFrame
            && state.EndFrame <= maxFrame)
        {
            return;
        }

        entry.AddIssue(
            ActionDefinitionAuditSeverity.Error,
            "TIMELINE_STATE_OOB",
            $"{state.GetType().Name} '{state.Id}' 帧 [{state.StartFrame},{state.EndFrame}] 越界（合法 0..{maxFrame}）。");
    }

    /// <summary>检查 Camera Spline 的 Knot 数、有限值与 Binding 基本契约。</summary>
    static void CollectCameraSplineIssues(
        CameraShotNotifyState shot,
        ActionDefinitionAuditEntry entry)
    {
        if (shot == null || !shot.OverrideCameraPose)
            return;

        Spline spline = shot.PositionSpline;
        if (!CameraSplineEvaluator.IsValid(spline))
        {
            entry.AddIssue(
                ActionDefinitionAuditSeverity.Error,
                "CAMERA_SPLINE_INVALID",
                $"CameraShot '{shot.Id}' 开启机位覆盖，但 PositionSpline 少于 2 个 Knot。");
            return;
        }

        for (int i = 0; i < spline.Count; i++)
        {
            var knot = spline[i];
            bool finite = IsFinite(knot.Position.x)
                && IsFinite(knot.Position.y)
                && IsFinite(knot.Position.z)
                && IsFinite(knot.TangentIn.x)
                && IsFinite(knot.TangentIn.y)
                && IsFinite(knot.TangentIn.z)
                && IsFinite(knot.TangentOut.x)
                && IsFinite(knot.TangentOut.y)
                && IsFinite(knot.TangentOut.z);
            if (finite)
                continue;

            entry.AddIssue(
                ActionDefinitionAuditSeverity.Error,
                "CAMERA_SPLINE_NON_FINITE",
                $"CameraShot '{shot.Id}' Knot[{i}] 含 NaN/Infinity。");
        }

        CollectBindingIssue(shot, shot.ReferenceBinding, "Reference", entry);
        CollectBindingIssue(shot, shot.LookAtBinding, "LookAt", entry);
    }

    /// <summary>World Binding 不接收 AnchorId；其它 Binding 对象必须存在。</summary>
    static void CollectBindingIssue(
        CameraShotNotifyState shot,
        CameraTransformBinding binding,
        string bindingName,
        ActionDefinitionAuditEntry entry)
    {
        if (binding == null)
        {
            entry.AddIssue(
                ActionDefinitionAuditSeverity.Error,
                "CAMERA_BINDING_NULL",
                $"CameraShot '{shot.Id}' {bindingName}Binding 为空。");
            return;
        }

        if (binding.Source == CameraBindingSource.World
            && !string.IsNullOrWhiteSpace(binding.AnchorId))
        {
            entry.AddIssue(
                ActionDefinitionAuditSeverity.Warning,
                "CAMERA_WORLD_BINDING_ANCHOR_UNUSED",
                $"CameraShot '{shot.Id}' {bindingName}Binding=World，AnchorId 不会被使用。");
        }
    }

    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

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
                $"{e.ActionName} | {e.AssetPath} | mode={e.BaseMotionMode} baked={e.BakedReady} scripted={e.HasScriptedMovement} frames={e.TotalFrames}");
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
