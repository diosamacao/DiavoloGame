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

/// <summary>查询某 AnimationKey 是否已绑定 Clip（避免 Resolver 依赖具体 Profile 类型）。</summary>
public interface ILocomotionAnimClipQuery
{
    /// <summary>是否已配置非空 Clip。</summary>
    bool HasClip(AnimationKey key);
}
