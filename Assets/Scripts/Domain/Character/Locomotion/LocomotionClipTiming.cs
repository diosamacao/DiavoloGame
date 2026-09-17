using System;
using UnityEngine;

/// <summary>单个 Locomotion 动画键的 60Hz 整数帧时序配置。</summary>
[Serializable]
public struct LocomotionClipTiming
{
    [SerializeField] AnimationKey key;
    [SerializeField, Min(1)] int durationFrames;
    [SerializeField] bool loop;
    [SerializeField, Min(1)] int exitFrame;
    [SerializeField, Min(0)] int handoffFrame;

    /// <summary>创建严格帧时序；非法范围由调用方校验并拒绝。</summary>
    public LocomotionClipTiming(
        AnimationKey key,
        int durationFrames,
        bool loop,
        int exitFrame,
        int handoffFrame)
    {
        this.key = key;
        this.durationFrames = durationFrames;
        this.loop = loop;
        this.exitFrame = exitFrame;
        this.handoffFrame = handoffFrame;
    }

    /// <summary>对应的逻辑动画键。</summary>
    public AnimationKey Key => key;

    /// <summary>Clip 在 60Hz 下覆盖的总逻辑帧数。</summary>
    public int DurationFrames => durationFrames;

    /// <summary>是否按 DurationFrames 循环采样。</summary>
    public bool Loop => loop;

    /// <summary>状态允许结束的相位帧；循环片通常等于 DurationFrames。</summary>
    public int ExitFrame => exitFrame;

    /// <summary>Start→Gait 或 Pivot→InputAuth 的交接帧。</summary>
    public int HandoffFrame => handoffFrame;

    /// <summary>所有帧必须落在明确范围内，禁止以默认值猜测缺失资产。</summary>
    public bool IsValid =>
        durationFrames > 0
        && exitFrame > 0
        && exitFrame <= durationFrames
        && handoffFrame >= 0
        && handoffFrame <= exitFrame;

    /// <summary>把相位帧折算为确定性采样帧；循环键按周期取模。</summary>
    public int ResolveSampleFrame(int phaseFrame)
    {
        if (!IsValid)
            throw new InvalidOperationException($"Locomotion timing「{key}」无效。");

        int frame = Math.Max(0, phaseFrame);
        if (loop)
            return frame % durationFrames;
        return Math.Min(frame, durationFrames - 1);
    }
}
