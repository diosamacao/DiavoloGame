using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>审查和修复已有根位移配置错误；只修改可确定来源的生成表，保留作者时间轴和位移模式。</summary>
public static class MotionContentRepair
{
    /// <summary>输出输入配对和范围；apply=true 时备份原资产后重建无效生成数据。</summary>
    public static void Run(bool apply)
    {
        string output = Path.GetFullPath(".utmp/motion-content-repair");
        Directory.CreateDirectory(output);
        string backup = Path.Combine(output, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        var report = new StringBuilder(apply ? "Repair\n" : "Read-only plan\n");
        int fixedCount = 0, failed = 0;
        foreach (var entry in ActionDefinitionAuditUtility.AuditProject())
        {
            var action = AssetDatabase.LoadAssetAtPath<ActionDefinition>(entry.AssetPath);
            string folder = ResolveRootFolder(action);
            bool invalid = entry.Issues.Any(i => i.Code == "BAKED_FRAME_COUNT_MISMATCH" || i.Code == "BAKED_MODE_NOT_READY");
            bool stale = action.ExecutionPolicy.BaseMotionMode == ActionBaseMotionMode.BakedMotion
                && ActionMotionDirtyUtility.IsDirty(action, folder, ActionSim.LogicHz);
            if (!invalid && !stale) continue;
            report.AppendLine("\n" + entry.AssetPath + " | RM=" + folder);
            if (folder == null) { report.AppendLine("BLOCKED: 无唯一角色 RM 目录。"); failed++; continue; }
            var original = action;
            action = UnityEngine.Object.Instantiate(original);
            try
            {
                if (!TryRestoreInplaceBindings(action, folder, report, out string bindingError))
                { report.AppendLine("BLOCKED: " + bindingError); failed++; continue; }
                foreach (var segment in action.AnimationSegments)
                {
                    if (MotionClipPairMatcher.TryMatchSingle(segment.clip, folder, out var pair, out string matchError))
                        report.AppendLine($"{segment.clip.name} ({segment.clip.length:F6}s) -> {pair.RootMotionClip.name} ({pair.RootMotionClip.length:F6}s), "
                            + $"range={segment.startFrame}..{segment.endFrame}, {AssetDatabase.GetAssetPath(pair.RootMotionClip)}");
                    else report.AppendLine("MATCH: " + matchError);
                }
                if (!ActionMotionDirtyUtility.TryBuildExpectedFingerprint(action, folder, ActionSim.LogicHz,
                        out _, out _, out int frames, out string error))
                { report.AppendLine("BLOCKED: " + error); failed++; continue; }
                report.AppendLine($"旧表={action.BakedMotion.frameCount}; 目标={frames}; 播放={action.TotalFrames}; mode={action.ExecutionPolicy.BaseMotionMode}; planar={action.BakedMotion.planarMode}");
                if (!apply) continue;
                Backup(entry.AssetPath, backup);
                if (ActionMotionBakeService.BakeAction(action, folder, action.BakedMotion.planarMode, ActionSim.LogicHz, out string result))
                {
                    Undo.RecordObject(original, "Repair Action Motion");
                    for (int i = 0; i < action.AnimationSegments.Length; i++)
                        original.AnimationSegments[i].clip = action.AnimationSegments[i].clip;
                    original.EditorSetBakedMotion(action.BakedMotion);
                    EditorUtility.SetDirty(original);
                    AssetDatabase.SaveAssetIfDirty(original);
                    AssetDatabase.ImportAsset(entry.AssetPath, ImportAssetOptions.ForceUpdate);
                    report.AppendLine("OK: " + result); fixedCount++;
                }
                else { report.AppendLine("FAILED: " + result); failed++; }
            }
            finally { UnityEngine.Object.DestroyImmediate(action); }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterLocomotionProfile"))
        {
            var profile = AssetDatabase.LoadAssetAtPath<CharacterLocomotionProfile>(AssetDatabase.GUIDToAssetPath(guid));
            bool invalid = new[] { AnimationKey.StartEnd, AnimationKey.StopL, AnimationKey.StopR, AnimationKey.PivotTurn }
                .Any(k => profile.IsRootMotionEnabled(k) && profile.TryGetClip(k, out _) && !profile.GetRootMotionTrack(k).IsValid);
            if (!invalid) continue;
            string path = AssetDatabase.GetAssetPath(profile);
            report.AppendLine("\nLocomotion: " + path);
            if (!apply) continue;
            Backup(path, backup);
            try
            {
                report.AppendLine(CharacterLocomotionProfileEditor.BakeRootMotion(profile));
                if (new[] { AnimationKey.StartEnd, AnimationKey.StopL, AnimationKey.StopR, AnimationKey.PivotTurn }
                    .Any(k => profile.IsRootMotionEnabled(k) && profile.TryGetClip(k, out _) && !profile.GetRootMotionTrack(k).IsValid))
                    throw new InvalidOperationException("重烘后仍存在无效轨。");
                EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); fixedCount++;
            }
            catch (Exception e) { report.AppendLine("FAILED: " + e.Message); failed++; }
        }
        report.AppendLine($"\nUpdated={fixedCount}; BlockedOrFailed={failed}; Backup={backup}");
        File.WriteAllText(Path.Combine(output, apply ? "repair.txt" : "plan.txt"), report.ToString());
        Debug.Log($"MotionContentRepair: updated={fixedCount}, blocked/failed={failed}; {output}");
    }

    // 仅恢复误绑定到 RM 目录的播放 Clip：同名 InPlace 唯一、时长相同，且反查回原 RM 才允许替换。
    static bool TryRestoreInplaceBindings(ActionDefinition action, string rootFolder, StringBuilder report, out string error)
    {
        error = null;
        for (int i = 0; i < action.AnimationSegments.Length; i++)
        {
            var source = action.AnimationSegments[i].clip;
            if (MotionClipPairMatcher.TryMatchSingle(source, rootFolder, out _, out _)) continue;
            string path = AssetDatabase.GetAssetPath(source);
            if (source == null || !path.StartsWith(rootFolder + "/", StringComparison.Ordinal))
            { error = $"段[{i}] 不是可确定的 RM 误绑定。"; return false; }
            string owner = "Assets/Art/Characters/" + path.Substring("Assets/Art/Characters/".Length).Split('/')[0];
            string expectedFile = Path.GetFileNameWithoutExtension(path) + "_Inplace";
            var candidates = AssetDatabase.FindAssets("t:AnimationClip", new[] { owner })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .Where(p => Path.GetFileNameWithoutExtension(p).Equals(expectedFile, StringComparison.OrdinalIgnoreCase))
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<AnimationClip>()
                .Where(c => (c.hideFlags & HideFlags.HideInHierarchy) == 0 && Mathf.Abs(c.length - source.length) < 0.0001f
                    && MotionClipPairMatcher.TryMatchSingle(c, rootFolder, out var pair, out _) && pair.RootMotionClip == source)
                .ToArray();
            if (candidates.Length != 1)
            { error = $"段[{i}] 同时长且反查到原 RM 的 InPlace 候选数={candidates.Length}"; return false; }
            report.AppendLine($"REBIND 段[{i}]: {path} -> {AssetDatabase.GetAssetPath(candidates[0])}; duration={source.length:F6}s");
            action.AnimationSegments[i].clip = candidates[0];
        }
        return true;
    }

    static string ResolveRootFolder(ActionDefinition action)
    {
        string path = AssetDatabase.GetAssetPath(action.AnimationSegments.FirstOrDefault().clip);
        const string prefix = "Assets/Art/Characters/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal)) return null;
        string owner = prefix + path.Substring(prefix.Length).Split('/')[0];
        string[] candidates = AssetDatabase.GetSubFolders(owner)
            .Where(p => Path.GetFileName(p).IndexOf("Root", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    static void Backup(string path, string folder)
    {
        string destination = Path.Combine(folder, path);
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.Copy(path, destination, false);
        File.Copy(path + ".meta", destination + ".meta", false);
    }
}
