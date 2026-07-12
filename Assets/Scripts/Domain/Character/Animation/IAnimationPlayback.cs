using System;
using UnityEngine;

/// <summary>动画播放后端契约；Playable / Animancer 等实现此接口，门面不感知具体 Graph。</summary>
public interface IAnimationPlayback : IDisposable
{
    /// <summary>当前是否有有效输出目标。</summary>
    bool IsValid { get; }

    /// <summary>播放倍率；0 = 冻结（卡肉），1 = 正常。</summary>
    float Speed { get; set; }

    /// <summary>当前主 Clip（淡入目标）；无则 null。</summary>
    AnimationClip CurrentClip { get; }

    /// <summary>主 Clip 归一化时间；循环 Clip 可大于 1。</summary>
    float NormalizedTime { get; }

    /// <summary>主 Clip 是否已播完至少一遍（淡入中或循环 Clip 视为未结束）。</summary>
    bool HasFinished { get; }

    /// <summary>以固定秒数淡入播放 Clip；同引用也会强制从头重播。</summary>
    void Play(AnimationClip clip, float fadeDuration);

    /// <summary>推进淡入混合等需手动更新的逻辑；Graph 自动推进时仍用于权重插值。</summary>
    void Tick(float deltaTime);
}
