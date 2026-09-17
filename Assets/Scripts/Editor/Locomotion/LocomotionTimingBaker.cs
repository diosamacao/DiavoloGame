using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>仅在人工点击后把 Clip 时长与旧比例迁成 60Hz LocomotionClipTiming。</summary>
public static class LocomotionTimingBaker
{
    /// <summary>烘焙选定 Profile；调用方负责 Undo、SetDirty 与保存确认。</summary>
    public static int Bake(CharacterLocomotionProfile profile)
    {
        if (profile == null || profile.AnimationProfile == null)
            throw new InvalidOperationException("Timing Baker 需要已绑定 AnimationProfile 的 Locomotion Profile。");

        profile.GetLegacyHandoffRatios(out float startRatio, out float pivotRatio);
        var timings = new List<LocomotionClipTiming>();
        Array keys = Enum.GetValues(typeof(AnimationKey));
        foreach (AnimationKey key in keys)
        {
            if (key == AnimationKey.HitShake)
                continue;
            if (!profile.AnimationProfile.TryGetClip(key, out AnimationClip clip) || clip == null)
                continue;

            int durationFrames = Mathf.Max(1, Mathf.CeilToInt(clip.length * ActionSim.LogicHz));
            int handoffFrame = ResolveHandoffFrame(
                key,
                durationFrames,
                startRatio,
                pivotRatio);
            timings.Add(new LocomotionClipTiming(
                key,
                durationFrames,
                clip.isLooping,
                durationFrames,
                handoffFrame));
        }

        profile.SetClipTimings(timings.ToArray());
        profile.BakeLegacyFrameSettings();
        profile.BakeLegacyFootMarkers();
        return timings.Count;
    }

    /// <summary>Start 使用旧 Start 比例，Pivot 使用旧交接比例，其余键在退出帧交接。</summary>
    static int ResolveHandoffFrame(
        AnimationKey key,
        int durationFrames,
        float startRatio,
        float pivotRatio)
    {
        float ratio = key == AnimationKey.PivotTurn
            ? pivotRatio
            : IsStartKey(key)
                ? startRatio
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
