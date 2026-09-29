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
        if (profile == null)
            throw new InvalidOperationException("Timing Baker 需要 Locomotion Profile。");

        var existingTimings = new Dictionary<AnimationKey, LocomotionClipTiming>();
        foreach (LocomotionClipTiming timing in profile.EditorClipTimings)
        {
            if (!timing.IsValid || existingTimings.ContainsKey(timing.Key))
                throw new InvalidOperationException($"{timing.Key}: 既有 Timing 无效或重复，请先修正；本次未写入。");
            existingTimings.Add(timing.Key, timing);
        }

        var timings = new List<LocomotionClipTiming>();
        Array keys = Enum.GetValues(typeof(AnimationKey));
        foreach (AnimationKey key in keys)
        {
            if (key == AnimationKey.HitShake)
                continue;
            if (!profile.TryGetClip(key, out AnimationClip clip) || clip == null)
                continue;

            int durationFrames = Mathf.Max(1, Mathf.CeilToInt(clip.length * ActionSim.LogicHz));
            bool hasExisting = existingTimings.TryGetValue(key, out LocomotionClipTiming existing);
            int exitFrame = hasExisting ? existing.ExitFrame : durationFrames;
            int handoffFrame = hasExisting ? existing.HandoffFrame : ResolveDefaultHandoffFrame(key, durationFrames);
            if (exitFrame > durationFrames || handoffFrame > exitFrame)
                throw new InvalidOperationException($"{key}: 新 Clip 只有 {durationFrames} 帧，既有 exit={exitFrame}/handoff={handoffFrame} 越界。请先调整作者时序；本次未写入任何 Timing。");
            timings.Add(new LocomotionClipTiming(
                key,
                durationFrames,
                clip.isLooping,
                exitFrame,
                handoffFrame));
        }

        profile.SetClipTimings(timings.ToArray());
        return timings.Count;
    }

    /// <summary>无既有 timing 时 Start 在结尾、Pivot 在中点交接，其余键在退出帧交接。</summary>
    static int ResolveDefaultHandoffFrame(AnimationKey key, int durationFrames)
    {
        return Mathf.Clamp(Mathf.RoundToInt(durationFrames * (key == AnimationKey.PivotTurn ? 0.5f : 1f)), 0, durationFrames);
    }
}
