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

    /// <summary>Additive 层当前权重；无叠加时为 0。</summary>
    float AdditiveWeight { get; }

    /// <summary>以固定秒数淡入播放 Clip；同引用也会强制从头重播。</summary>
    void Play(AnimationClip clip, float fadeDuration);

    /// <summary>
    /// 在主 Clip 上叠加一段 Additive Clip；同层后写覆盖并从头 Seek。
    /// mask 为空则全骨骼；不改主槽 CrossFade。
    /// </summary>
    void PlayAdditive(AnimationClip clip, AvatarMask mask, float fadeDuration);

    /// <summary>立刻将 Additive 层权重置 0 并断开 Clip。</summary>
    void StopAdditive();

    /// <summary>将当前主 Clip 跳到指定时间（秒）；用于段 startFrame 裁切起点，不推进时间。</summary>
    void Seek(float timeSeconds);

    /// <summary>按固定步长推进 Graph 时间与 CrossFade 权重；Simulation 每逻辑步必须调用。</summary>
    void Tick(float deltaTime);
}
