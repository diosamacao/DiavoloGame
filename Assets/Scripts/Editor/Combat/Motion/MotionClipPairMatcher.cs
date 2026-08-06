using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>单条 InPlace↔RootMotion 匹配结果。</summary>
public readonly struct MotionClipBakePair
{
    public MotionClipBakePair(
        AnimationClip inplaceClip,
        AnimationClip rootMotionClip,
        string stem,
        int priority,
        string inplacePath,
        string rootMotionPath)
    {
        InplaceClip = inplaceClip;
        RootMotionClip = rootMotionClip;
        Stem = stem;
        Priority = priority;
        InplacePath = inplacePath;
        RootMotionPath = rootMotionPath;
    }

    public AnimationClip InplaceClip { get; }
    public AnimationClip RootMotionClip { get; }
    public string Stem { get; }
    public int Priority { get; }
    public string InplacePath { get; }
    public string RootMotionPath { get; }
}

/// <summary>匹配失败条目。</summary>
public readonly struct MotionClipMatchIssue
{
    public MotionClipMatchIssue(string inplacePath, string inplaceName, string reason)
    {
        InplacePath = inplacePath;
        InplaceName = inplaceName;
        Reason = reason;
    }

    public string InplacePath { get; }
    public string InplaceName { get; }
    public string Reason { get; }
}

/// <summary>在指定 InPlace / RootMotion 文件夹内按命名规则自动配对。</summary>
public static class MotionClipPairMatcher
{
    static readonly Dictionary<string, List<ClipEntry>> s_folderClipCache =
        new(StringComparer.Ordinal);
    static bool s_projectChangedHooked;

    /// <summary>扫描两文件夹并构建配对；issues 含不合规、未匹配与歧义。</summary>
    public static void BuildPairs(
        string inplaceFolder,
        string rootMotionFolder,
        List<MotionClipBakePair> pairs,
        List<MotionClipMatchIssue> issues)
    {
        pairs?.Clear();
        issues?.Clear();
        if (pairs == null || issues == null)
            return;

        if (string.IsNullOrEmpty(inplaceFolder) || string.IsNullOrEmpty(rootMotionFolder))
        {
            issues.Add(new MotionClipMatchIssue(string.Empty, string.Empty, "未指定 InPlace 或 RootMotion 文件夹"));
            return;
        }

        List<ClipEntry> inplaceClips = CollectClipsUnderFolder(inplaceFolder);
        List<ClipEntry> rmClips = CollectClipsUnderFolder(rootMotionFolder);
        if (inplaceClips.Count == 0)
        {
            issues.Add(new MotionClipMatchIssue(inplaceFolder, string.Empty, "InPlace 文件夹内无 AnimationClip"));
            return;
        }

        for (int i = 0; i < inplaceClips.Count; i++)
        {
            ClipEntry inplace = inplaceClips[i];
            if (!MotionClipNameRules.TryGetInplaceStem(inplace.Clip.name, out string stem)
                && !MotionClipNameRules.TryGetInplaceStem(inplace.FileName, out stem))
            {
                issues.Add(new MotionClipMatchIssue(
                    inplace.Path,
                    inplace.Clip.name,
                    "命名不合规：缺少 _Inplace 后缀"));
                continue;
            }

            int bestPriority = int.MaxValue;
            var candidates = new List<ClipEntry>(4);

            for (int j = 0; j < rmClips.Count; j++)
            {
                ClipEntry rm = rmClips[j];
                int priority = MotionClipNameRules.GetMatchPriority(stem, rm.Clip.name, rm.FileName);
                if (priority < 0)
                    continue;

                if (priority < bestPriority)
                {
                    bestPriority = priority;
                    candidates.Clear();
                    candidates.Add(rm);
                }
                else if (priority == bestPriority)
                {
                    candidates.Add(rm);
                }
            }

            if (candidates.Count == 0)
            {
                issues.Add(new MotionClipMatchIssue(
                    inplace.Path,
                    inplace.Clip.name,
                    $"未匹配 RootMotion（stem={stem}）"));
                continue;
            }

            if (candidates.Count > 1)
            {
                issues.Add(new MotionClipMatchIssue(
                    inplace.Path,
                    inplace.Clip.name,
                    $"歧义：P{bestPriority} 命中 {candidates.Count} 个 RM（stem={stem}）"));
                continue;
            }

            ClipEntry matched = candidates[0];
            pairs.Add(new MotionClipBakePair(
                inplace.Clip,
                matched.Clip,
                stem,
                bestPriority,
                inplace.Path,
                matched.Path));
        }
    }

    /// <summary>在 RootMotion 集合中为单个 InPlace Clip 找唯一配对。</summary>
    public static bool TryMatchSingle(
        AnimationClip inplaceClip,
        string rootMotionFolder,
        out MotionClipBakePair pair,
        out string error)
    {
        pair = default;
        error = string.Empty;
        if (inplaceClip == null)
        {
            error = "InPlace Clip 为空";
            return false;
        }

        // 直接在 RM 文件夹内按 stem 匹配该 InPlace Clip
        if (!MotionClipNameRules.TryGetInplaceStem(inplaceClip.name, out string stem))
        {
            string path = AssetDatabase.GetAssetPath(inplaceClip);
            MotionClipNameRules.TryGetInplaceStem(System.IO.Path.GetFileNameWithoutExtension(path), out stem);
        }

        if (string.IsNullOrEmpty(stem))
        {
            error = "InPlace 命名不合规";
            return false;
        }

        List<ClipEntry> rmClips = CollectClipsUnderFolder(rootMotionFolder);
        int bestPriority = int.MaxValue;
        var candidates = new List<ClipEntry>(4);
        for (int j = 0; j < rmClips.Count; j++)
        {
            ClipEntry rm = rmClips[j];
            int priority = MotionClipNameRules.GetMatchPriority(stem, rm.Clip.name, rm.FileName);
            if (priority < 0)
                continue;
            if (priority < bestPriority)
            {
                bestPriority = priority;
                candidates.Clear();
                candidates.Add(rm);
            }
            else if (priority == bestPriority)
            {
                candidates.Add(rm);
            }
        }

        if (candidates.Count != 1)
        {
            error = candidates.Count == 0
                ? $"未匹配 RootMotion（stem={stem}）"
                : $"歧义命中 {candidates.Count} 个 RM（stem={stem}）";
            return false;
        }

        string inplacePath = AssetDatabase.GetAssetPath(inplaceClip);
        pair = new MotionClipBakePair(
            inplaceClip,
            candidates[0].Clip,
            stem,
            bestPriority,
            inplacePath,
            candidates[0].Path);
        return true;
    }

    /// <summary>工程变更时清空文件夹 Clip 缓存，避免匹配到已删除/改名资产。</summary>
    static void EnsureProjectChangedHook()
    {
        if (s_projectChangedHooked)
            return;

        s_projectChangedHooked = true;
        EditorApplication.projectChanged += ClearFolderClipCache;
    }

    /// <summary>清空 InPlace/RM 文件夹扫描缓存。</summary>
    public static void ClearFolderClipCache() => s_folderClipCache.Clear();

    /// <summary>
    /// 扫描文件夹下全部 AnimationClip；结果按路径缓存。
    /// Dirty / TryMatchSingle 热路径依赖此缓存，避免每次 FindAssets + LoadAllAssetsAtPath。
    /// </summary>
    static List<ClipEntry> CollectClipsUnderFolder(string folder)
    {
        EnsureProjectChangedHook();

        if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            return new List<ClipEntry>(0);

        if (s_folderClipCache.TryGetValue(folder, out List<ClipEntry> cached))
            return cached;

        var result = new List<ClipEntry>(64);
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folder });
        Array.Sort(guids, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            // FindAssets 对 FBX 子 Clip 可能重复；按 path+name 去重
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int a = 0; a < assets.Length; a++)
            {
                if (assets[a] is not AnimationClip clip || clip == null)
                    continue;
                // 跳过预览之类隐藏 clip
                if ((clip.hideFlags & HideFlags.HideInHierarchy) != 0)
                    continue;

                string key = path + "::" + clip.name;
                if (!seen.Add(key))
                    continue;

                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                result.Add(new ClipEntry(clip, path, fileName));
            }
        }

        s_folderClipCache[folder] = result;
        return result;
    }

    readonly struct ClipEntry
    {
        public ClipEntry(AnimationClip clip, string path, string fileName)
        {
            Clip = clip;
            Path = path;
            FileName = fileName;
        }

        public AnimationClip Clip { get; }
        public string Path { get; }
        public string FileName { get; }
    }
}
