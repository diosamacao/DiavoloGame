using System;
using UnityEngine;

/// <summary>招式内一段可播放动画；多段按数组顺序拼接为同一 Action 的连续逻辑时间轴。</summary>
[Serializable]
public struct ActionAnimationSegment
{
    [Tooltip("本段播放的 AnimationClip。")]
    public AnimationClip clip;

    [Tooltip("相对 Clip 的起始逻辑帧（含），按 Action 的 sampleRate 换算。")]
    public int startFrame;

    [Tooltip("相对 Clip 的结束逻辑帧（含）；小于 0 表示用到 Clip 末尾。")]
    public int endFrame;

    [Tooltip("为 true 时使用本段 crossFadeDuration（含 0=硬切）；为 false 时用招式默认淡入。")]
    public bool hasCrossFadeOverride;

    [Tooltip("切入本段时的淡入秒数；仅 hasCrossFadeOverride 时生效，0 表示硬切。")]
    public float crossFadeDuration;

    /// <summary>按采样率解析本段在 Clip 内的有效起止帧（含）。</summary>
    public bool TryGetFrameRange(float sampleRate, out int startInclusive, out int endInclusive)
    {
        startInclusive = 0;
        endInclusive = 0;
        if (clip == null)
            return false;

        float rate = sampleRate > 0f ? sampleRate : ActionSim.LogicHz;
        int clipLastFrame = Mathf.Max(0, Mathf.RoundToInt(clip.length * rate) - 1);
        startInclusive = Mathf.Clamp(startFrame, 0, clipLastFrame);
        endInclusive = endFrame < 0 ? clipLastFrame : Mathf.Clamp(endFrame, startInclusive, clipLastFrame);
        return true;
    }

    /// <summary>本段贡献的逻辑帧数（至少 1）。</summary>
    public int GetFrameCount(float sampleRate)
    {
        if (!TryGetFrameRange(sampleRate, out int startInclusive, out int endInclusive))
            return 0;

        return Mathf.Max(1, endInclusive - startInclusive + 1);
    }

    /// <summary>本段贡献的秒数。</summary>
    public float GetDurationSeconds(float sampleRate)
    {
        float rate = sampleRate > 0f ? sampleRate : ActionSim.LogicHz;
        return GetFrameCount(sampleRate) / rate;
    }

    /// <summary>将全局段内帧偏移换算为 Clip 采样时间（秒）。</summary>
    public float GetLocalTimeSeconds(int frameOffsetInSegment, float sampleRate)
    {
        if (!TryGetFrameRange(sampleRate, out int startInclusive, out _))
            return 0f;

        float rate = sampleRate > 0f ? sampleRate : ActionSim.LogicHz;
        int localFrame = startInclusive + Mathf.Max(0, frameOffsetInSegment);
        return localFrame / rate;
    }
}
