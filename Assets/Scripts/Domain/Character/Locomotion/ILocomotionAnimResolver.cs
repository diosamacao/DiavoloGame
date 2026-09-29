using UnityEngine;

/// <summary>由步态 + 局部移动意图解析 AnimationKey（经 AnimSet / DirectionModel）。</summary>
public interface ILocomotionAnimResolver
{
    /// <summary>解析本帧应播放的 Locomotion 循环动画键。</summary>
    AnimationKey Resolve(
        LocomotionGait gait,
        Vector2 localMoveIntent,
        ILocomotionAnimClipQuery clips);
}
