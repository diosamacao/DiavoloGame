using UnityEngine;

/// <summary>
/// 默认选片：Run/Sprint 走原键；Walk 下主导横向且已绑 Clip 时用 WalkLeft/WalkRight。
/// </summary>
public sealed class DefaultLocomotionAnimResolver : ILocomotionAnimResolver
{
    /// <summary>横向主导死区：|x| 须大于此且不小于 |y|。</summary>
    public const float LateralDominanceEpsilon = 0.2f;

    readonly float _lateralEpsilon;

    /// <summary>创建默认 Resolver。</summary>
    public DefaultLocomotionAnimResolver(float lateralEpsilon = LateralDominanceEpsilon)
    {
        _lateralEpsilon = Mathf.Max(0.01f, lateralEpsilon);
    }

    /// <inheritdoc />
    public AnimationKey Resolve(
        LocomotionGait gait,
        Vector2 localMoveIntent,
        ILocomotionAnimClipQuery clips)
    {
        if (clips == null)
            return AnimationKey.Walk;

        switch (gait)
        {
            case LocomotionGait.Sprint:
                if (clips.HasClip(AnimationKey.Sprint))
                    return AnimationKey.Sprint;
                return AnimationKey.Run;
            case LocomotionGait.Run:
                return AnimationKey.Run;
            default:
                return ResolveWalk(localMoveIntent, clips);
        }
    }

    AnimationKey ResolveWalk(Vector2 localMove, ILocomotionAnimClipQuery clips)
    {
        float ax = Mathf.Abs(localMove.x);
        float ay = Mathf.Abs(localMove.y);
        if (ax >= _lateralEpsilon && ax >= ay)
        {
            if (localMove.x < 0f && clips.HasClip(AnimationKey.WalkLeft))
                return AnimationKey.WalkLeft;
            if (localMove.x > 0f && clips.HasClip(AnimationKey.WalkRight))
                return AnimationKey.WalkRight;
        }

        return AnimationKey.Walk;
    }
}
