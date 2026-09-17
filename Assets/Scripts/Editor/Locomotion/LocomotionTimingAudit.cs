using System;
using UnityEditor;
using UnityEngine;

/// <summary>只读审计 Locomotion Profile 的 Clip→整数帧 timing 完整性。</summary>
public static class LocomotionTimingAudit
{
    static readonly AnimationKey[] s_rootMotionKeys =
    {
        AnimationKey.StartEnd,
        AnimationKey.StopL,
        AnimationKey.StopR,
        AnimationKey.PivotTurn,
    };

    /// <summary>检查已绑定的每个 Clip 是否存在唯一有效 timing；不修改任何资产。</summary>
    public static bool Validate(CharacterLocomotionProfile profile, bool logSuccess)
    {
        if (profile == null || profile.AnimationProfile == null)
        {
            Debug.LogError("LocomotionTimingAudit: 缺少 Profile 或 AnimationProfile。", profile);
            return false;
        }

        bool valid = true;
        foreach (AnimationKey key in Enum.GetValues(typeof(AnimationKey)))
        {
            if (key == AnimationKey.HitShake)
                continue;
            if (!profile.AnimationProfile.TryGetClip(key, out AnimationClip clip) || clip == null)
                continue;
            if (profile.TryGetClipTiming(key, out _))
                continue;

            Debug.LogError(
                $"LocomotionTimingAudit:「{profile.name}」的 {key} Clip 缺少唯一有效 timing。",
                profile);
            valid = false;
        }

        // Timing 迁移与根运动轨格式迁移必须同时完成，否则特殊相位会正常播 Clip 但角色原地不动。
        for (int i = 0; i < s_rootMotionKeys.Length; i++)
        {
            AnimationKey key = s_rootMotionKeys[i];
            if (!profile.IsRootMotionEnabled(key)
                || !profile.AnimationProfile.TryGetClip(key, out AnimationClip clip)
                || clip == null
                || profile.GetRootMotionTrack(key).IsValid)
            {
                continue;
            }

            Debug.LogError(
                $"LocomotionTimingAudit:「{profile.name}」的 {key} 已启用根运动，但轨道无效；请执行 Root Motion Bake。",
                profile);
            valid = false;
        }

        if (valid && logSuccess)
            Debug.Log($"LocomotionTimingAudit:「{profile.name}」Timing 与 Root Motion 均通过。", profile);
        return valid;
    }

    /// <summary>菜单仅审计当前选中的 Locomotion Profile。</summary>
    [MenuItem("Tools/ACT/Locomotion/Audit Selected Timing")]
    public static void AuditSelected()
    {
        CharacterLocomotionProfile profile = Selection.activeObject as CharacterLocomotionProfile;
        if (profile == null)
        {
            Debug.LogError("请先在 Project 选中 CharacterLocomotionProfile。");
            return;
        }

        Validate(profile, logSuccess: true);
    }
}
