using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>将匹配到的 RM Clip 烘焙为 ActionBakedMotion 并写回引用对应 InPlace 的 ActionDefinition。</summary>
public static class ActionMotionBakeService
{
    const float MaxAccumulatedErrorMeters = 0.02f;
    const float MaxSingleFrameErrorMeters = 0.005f;

    /// <summary>文件夹批烘结果摘要。</summary>
    public sealed class BakeReport
    {
        public int PairCount;
        public int ActionsUpdated;
        public int PairsWithoutConsumer;
        public int FailedPairs;
        public readonly List<string> Lines = new(32);

        public override string ToString()
        {
            var sb = new StringBuilder(256);
            sb.AppendLine(
                $"配对={PairCount}, 更新 Action={ActionsUpdated}, 无引用={PairsWithoutConsumer}, 失败={FailedPairs}");
            for (int i = 0; i < Lines.Count; i++)
                sb.AppendLine(Lines[i]);
            return sb.ToString();
        }
    }

    /// <summary>预览匹配，不写资产。</summary>
    public static string PreviewMatches(string inplaceFolder, string rootMotionFolder)
    {
        var pairs = new List<MotionClipBakePair>(64);
        var issues = new List<MotionClipMatchIssue>(64);
        MotionClipPairMatcher.BuildPairs(inplaceFolder, rootMotionFolder, pairs, issues);

        var sb = new StringBuilder(512);
        sb.AppendLine($"匹配成功：{pairs.Count}");
        for (int i = 0; i < pairs.Count; i++)
        {
            MotionClipBakePair p = pairs[i];
            sb.AppendLine(
                $"  [P{p.Priority}] {p.InplaceClip.name} ↔ {p.RootMotionClip.name}  (stem={p.Stem})");
        }

        sb.AppendLine($"问题：{issues.Count}");
        for (int i = 0; i < issues.Count; i++)
        {
            MotionClipMatchIssue issue = issues[i];
            sb.AppendLine($"  {issue.InplaceName}: {issue.Reason}");
        }

        return sb.ToString();
    }

    /// <summary>对两文件夹内匹配成功的对烘焙，并写回引用 InPlace 的 ActionDefinition。</summary>
    public static BakeReport BakeFromFolders(
        string inplaceFolder,
        string rootMotionFolder,
        ActionMotionPlanarMode planarMode,
        int logicHz)
    {
        var report = new BakeReport();
        var pairs = new List<MotionClipBakePair>(64);
        var issues = new List<MotionClipMatchIssue>(64);
        MotionClipPairMatcher.BuildPairs(inplaceFolder, rootMotionFolder, pairs, issues);

        report.PairCount = pairs.Count;
        for (int i = 0; i < issues.Count; i++)
            report.Lines.Add($"MATCH: {issues[i].InplaceName}: {issues[i].Reason}");

        Dictionary<AnimationClip, List<ActionDefinition>> consumers = BuildInplaceConsumers();

        for (int i = 0; i < pairs.Count; i++)
        {
            MotionClipBakePair pair = pairs[i];
            ActionBakedMotion table = RootMotionBakeUtility.BakeClip(
                pair.RootMotionClip,
                logicHz,
                planarMode,
                pair.RootMotionClip.name,
                RootMotionBakeUtility.ComputeClipContentHash(pair.InplaceClip),
                RootMotionBakeUtility.ComputeClipContentHash(pair.RootMotionClip));

            if (!table.IsReady)
            {
                report.FailedPairs++;
                report.Lines.Add($"FAIL bake: {pair.InplaceClip.name} ← {pair.RootMotionClip.name}");
                continue;
            }

            bool ok = RootMotionBakeUtility.ValidateAgainstTrack(
                pair.RootMotionClip,
                table,
                MaxAccumulatedErrorMeters,
                MaxSingleFrameErrorMeters,
                out string validateReport);
            if (!ok)
            {
                table.bakeStatus = ActionBakedMotionStatus.Failed;
                report.FailedPairs++;
                report.Lines.Add($"FAIL validate: {pair.InplaceClip.name} — {validateReport}");
                continue;
            }

            if (!consumers.TryGetValue(pair.InplaceClip, out List<ActionDefinition> actions)
                || actions == null
                || actions.Count == 0)
            {
                report.PairsWithoutConsumer++;
                report.Lines.Add(
                    $"OK no-consumer: {pair.InplaceClip.name} ↔ {pair.RootMotionClip.name} ({validateReport})");
                continue;
            }

            for (int a = 0; a < actions.Count; a++)
            {
                WriteBakedMotion(actions[a], table);
                report.ActionsUpdated++;
            }

            report.Lines.Add(
                $"OK: {pair.InplaceClip.name} ↔ {pair.RootMotionClip.name} → {actions.Count} Action(s); {validateReport}");
        }

        AssetDatabase.SaveAssets();
        return report;
    }

    /// <summary>按 Action 各段 InPlace 匹配 RM 后拼接写回单招（多段顺序追加帧；不含偏航）。</summary>
    public static bool BakeAction(
        ActionDefinition action,
        string rootMotionFolder,
        ActionMotionPlanarMode planarMode,
        int logicHz,
        out string message)
    {
        message = string.Empty;
        if (action == null)
        {
            message = "Action 为空";
            return false;
        }

        ActionAnimationSegment[] segments = action.AnimationSegments;
        if (segments == null || segments.Length == 0)
        {
            message = "无动画段";
            return false;
        }

        var dx = new List<int>(256);
        var dz = new List<int>(256);
        var matchedNames = new List<string>(segments.Length);
        string inplaceHash = string.Empty;
        string rmHash = string.Empty;

        for (int i = 0; i < segments.Length; i++)
        {
            AnimationClip inplace = segments[i].clip;
            if (inplace == null)
                continue;

            if (!MotionClipPairMatcher.TryMatchSingle(
                    inplace,
                    rootMotionFolder,
                    out MotionClipBakePair pair,
                    out string error))
            {
                message = $"段[{i}] {inplace.name}: {error}";
                return false;
            }

            ActionBakedMotion part = RootMotionBakeUtility.BakeClip(
                pair.RootMotionClip,
                logicHz,
                planarMode,
                pair.RootMotionClip.name,
                RootMotionBakeUtility.ComputeClipContentHash(inplace),
                RootMotionBakeUtility.ComputeClipContentHash(pair.RootMotionClip));

            if (!part.IsReady)
            {
                message = $"段[{i}] 烘焙失败: {pair.RootMotionClip.name}";
                return false;
            }

            // 按段帧窗口截取；endFrame=-1 表示整段表
            int start = Mathf.Max(0, segments[i].startFrame);
            int end = segments[i].endFrame < 0
                ? part.frameCount - 1
                : Mathf.Min(segments[i].endFrame, part.frameCount - 1);
            if (end < start)
            {
                message = $"段[{i}] 帧区间无效";
                return false;
            }

            for (int f = start; f <= end; f++)
            {
                dx.Add(part.positionDeltaMmX[f]);
                dz.Add(part.positionDeltaMmZ[f]);
            }

            matchedNames.Add(pair.RootMotionClip.name);
            inplaceHash += part.inplaceContentHash + ";";
            rmHash += part.rootMotionContentHash + ";";
        }

        if (dx.Count == 0)
        {
            message = "未产出任何帧";
            return false;
        }

        var yaw = new int[dx.Count];
        var table = new ActionBakedMotion
        {
            logicHz = Mathf.Max(1, logicHz),
            frameCount = dx.Count,
            planarMode = planarMode,
            positionDeltaMmX = dx.ToArray(),
            positionDeltaMmZ = dz.ToArray(),
            yawDeltaMilliDeg = yaw,
            inplaceContentHash = inplaceHash,
            rootMotionContentHash = rmHash,
            matchedRootMotionName = string.Join(" | ", matchedNames),
            bakeStatus = ActionBakedMotionStatus.Ok,
        };

        WriteBakedMotion(action, table);
        AssetDatabase.SaveAssets();
        message = $"已写回 {action.name}: frames={table.frameCount}, rm={table.matchedRootMotionName}";
        return true;
    }

    static void WriteBakedMotion(ActionDefinition action, ActionBakedMotion table)
    {
        Undo.RecordObject(action, "Bake Action Motion");
        action.EditorSetBakedMotion(table);
        EditorUtility.SetDirty(action);
    }

    static Dictionary<AnimationClip, List<ActionDefinition>> BuildInplaceConsumers()
    {
        var map = new Dictionary<AnimationClip, List<ActionDefinition>>();
        string[] guids = AssetDatabase.FindAssets("t:ActionDefinition");
        Array.Sort(guids, StringComparer.Ordinal);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ActionDefinition action = AssetDatabase.LoadAssetAtPath<ActionDefinition>(path);
            if (action == null)
                continue;

            ActionAnimationSegment[] segments = action.AnimationSegments;
            for (int s = 0; s < segments.Length; s++)
            {
                AnimationClip clip = segments[s].clip;
                if (clip == null)
                    continue;

                if (!map.TryGetValue(clip, out List<ActionDefinition> list))
                {
                    list = new List<ActionDefinition>(2);
                    map.Add(clip, list);
                }

                if (!list.Contains(action))
                    list.Add(action);
            }
        }

        return map;
    }
}
