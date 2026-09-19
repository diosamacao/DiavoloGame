using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>仅在人工点击后把 Clip 时长烘焙为 60Hz LocomotionClipTiming。</summary>
public static class LocomotionTimingBaker
{
    /// <summary>烘焙选定 Profile；调用方负责 Undo、SetDirty 与保存确认。</summary>
    public static int Bake(CharacterLocomotionProfile profile)
    {
        if (profile == null || profile.AnimationProfile == null)
            throw new InvalidOperationException("Timing Baker 需要已绑定 AnimationProfile 的 Locomotion Profile。");

        var timings = new List<LocomotionClipTiming>();
        Array keys = Enum.GetValues(typeof(AnimationKey));
        foreach (AnimationKey key in keys)
        {
            if (key == AnimationKey.HitShake)
                continue;
            if (!profile.AnimationProfile.TryGetClip(key, out AnimationClip clip) || clip == null)
                continue;

            int durationFrames = Mathf.Max(1, Mathf.CeilToInt(clip.length * ActionSim.LogicHz));
            int handoffFrame = profile.TryGetClipTiming(key, out LocomotionClipTiming existing)
                ? Mathf.Clamp(existing.HandoffFrame, 0, durationFrames)
                : ResolveDefaultHandoffFrame(key, durationFrames);
            timings.Add(new LocomotionClipTiming(
                key,
                durationFrames,
                clip.isLooping,
                durationFrames,
                handoffFrame));
        }

        profile.SetClipTimings(timings.ToArray());
        return timings.Count;
    }

    /// <summary>无既有 timing 时 Start 在结尾、Pivot 在中点交接，其余键在退出帧交接。</summary>
    static int ResolveDefaultHandoffFrame(AnimationKey key, int durationFrames)
    {
        float ratio = key == AnimationKey.PivotTurn
            ? 0.5f
            : IsStartKey(key)
                ? 1f
                : 1f;
        return Mathf.Clamp(
            Mathf.RoundToInt(durationFrames * ratio),
            0,
            durationFrames);
    }

    /// <summary>识别 AnimSet 可选的所有起步键。</summary>
    static bool IsStartKey(AnimationKey key) =>
        key == AnimationKey.Start
        || key == AnimationKey.WalkStart
        || key == AnimationKey.WalkStartLeft
        || key == AnimationKey.WalkStartRight;
}
