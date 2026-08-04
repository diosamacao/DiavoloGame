using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 运动表脏检测：InPlace/RM 内容 hash、logicHz、段帧窗口任一变化即视为 Dirty。
/// </summary>
public static class ActionMotionDirtyUtility
{
    /// <summary>单招是否相对当前文件夹/Hz 过期或未就绪。</summary>
    public static bool IsDirty(ActionDefinition action, string rootMotionFolder, int logicHz)
    {
        if (action == null)
            return false;

        ActionBakedMotion baked = action.BakedMotion;
        if (baked == null || baked.bakeStatus != ActionBakedMotionStatus.Ok || !baked.IsReady)
            return true;
        if (baked.logicHz != logicHz)
            return true;

        if (!TryBuildExpectedFingerprint(
                action,
                rootMotionFolder,
                logicHz,
                out string inplaceHash,
                out string rmHash,
                out int expectedFrames,
                out _))
        {
            // 无法匹配 RM 时：已烘焙也算脏（源丢失）
            return true;
        }

        if (!string.Equals(baked.inplaceContentHash, inplaceHash, StringComparison.Ordinal))
            return true;
        if (!string.Equals(baked.rootMotionContentHash, rmHash, StringComparison.Ordinal))
            return true;
        if (baked.frameCount != expectedFrames)
            return true;

        return false;
    }

    /// <summary>扫描工程内全部 Action，列出 Dirty / Failed / 未匹配摘要。</summary>
    public static string ValidateProject(string rootMotionFolder, int logicHz)
    {
        var sb = new StringBuilder(512);
        int dirty = 0;
        int failed = 0;
        int ok = 0;
        int none = 0;

        string[] guids = AssetDatabase.FindAssets("t:ActionDefinition");
        Array.Sort(guids, StringComparer.Ordinal);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ActionDefinition action = AssetDatabase.LoadAssetAtPath<ActionDefinition>(path);
            if (action == null)
                continue;

            ActionBakedMotion baked = action.BakedMotion;
            if (baked != null && baked.bakeStatus == ActionBakedMotionStatus.Failed)
            {
                failed++;
                sb.AppendLine($"FAILED: {action.name} ({path})");
                continue;
            }

            if (baked == null || baked.bakeStatus == ActionBakedMotionStatus.None)
            {
                // 无动画段的占位招不算强制脏列表噪音
                if (action.AnimationSegments == null || action.AnimationSegments.Length == 0)
                {
                    none++;
                    continue;
                }

                dirty++;
                sb.AppendLine($"DIRTY(none): {action.name} ({path})");
                continue;
            }

            if (IsDirty(action, rootMotionFolder, logicHz))
            {
                dirty++;
                sb.AppendLine($"DIRTY: {action.name} ({path})");
            }
            else
            {
                ok++;
            }
        }

        sb.Insert(
            0,
            $"Motion Dirty Validate: ok={ok}, dirty={dirty}, failed={failed}, placeholderNone={none}\n");
        return sb.ToString();
    }

    /// <summary>按与 BakeAction 相同规则重建指纹；失败返回 false。</summary>
    public static bool TryBuildExpectedFingerprint(
        ActionDefinition action,
        string rootMotionFolder,
        int logicHz,
        out string inplaceHash,
        out string rmHash,
        out int expectedFrames,
        out string error)
    {
        inplaceHash = string.Empty;
        rmHash = string.Empty;
        expectedFrames = 0;
        error = string.Empty;

        ActionAnimationSegment[] segments = action.AnimationSegments;
        if (segments == null || segments.Length == 0)
        {
            error = "无动画段";
            return false;
        }

        var inplaceParts = new List<string>(segments.Length);
        var rmParts = new List<string>(segments.Length);
        int frames = 0;

        for (int i = 0; i < segments.Length; i++)
        {
            AnimationClip inplace = segments[i].clip;
            if (inplace == null)
                continue;

            if (!MotionClipPairMatcher.TryMatchSingle(
                    inplace,
                    rootMotionFolder,
                    out MotionClipBakePair pair,
                    out string matchError))
            {
                error = $"段[{i}] {inplace.name}: {matchError}";
                return false;
            }

            ActionBakedMotion part = RootMotionBakeUtility.BakeClip(
                pair.RootMotionClip,
                logicHz,
                ActionMotionPlanarMode.FullPlanar,
                pair.RootMotionClip.name,
                RootMotionBakeUtility.ComputeClipContentHash(inplace),
                RootMotionBakeUtility.ComputeClipContentHash(pair.RootMotionClip));
            if (!part.IsReady)
            {
                error = $"段[{i}] 无法采样 RM: {pair.RootMotionClip.name}";
                return false;
            }

            int start = Mathf.Max(0, segments[i].startFrame);
            int end = segments[i].endFrame < 0
                ? part.frameCount - 1
                : Mathf.Min(segments[i].endFrame, part.frameCount - 1);
            if (end < start)
            {
                error = $"段[{i}] 帧区间无效";
                return false;
            }

            frames += end - start + 1;
            inplaceParts.Add(part.inplaceContentHash);
            rmParts.Add(part.rootMotionContentHash);
        }

        if (frames <= 0)
        {
            error = "未产出任何帧";
            return false;
        }

        inplaceHash = string.Join(";", inplaceParts) + ";";
        rmHash = string.Join(";", rmParts) + ";";
        expectedFrames = frames;
        return true;
    }
}
