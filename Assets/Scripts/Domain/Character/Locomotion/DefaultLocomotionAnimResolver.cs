using UnityEngine;

/// <summary>
/// 默认选片：Run/Sprint 走原键；Walk 下主导横向且已绑 Clip 时用 WalkLeft/WalkRight；
/// 起步按步态 + 本地输入选 WalkStartLeft/Right / WalkStart / Start。
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
        if (TryResolveLateralKey(
                localMove,
                clips,
                AnimationKey.WalkLeft,
                AnimationKey.WalkRight,
                out AnimationKey lateral))
        {
            return lateral;
        }

        return AnimationKey.Walk;
    }

    /// <summary>
    /// 起步 Clip：Walk 按横向选 WalkStartLeft/Right，否则 WalkStart；缺片逐级回退到 Start。
    /// Run/Sprint → Start（缺则 Walk 起步链）。
    /// </summary>
    public static AnimationKey ResolveStartKey(
        LocomotionGait initialGait,
        Vector2 localMoveIntent,
        ILocomotionAnimClipQuery clips,
        float lateralEpsilon = LateralDominanceEpsilon)
    {
        if (clips == null)
            return AnimationKey.Start;

        if (initialGait == LocomotionGait.Walk)
            return ResolveWalkStartKey(localMoveIntent, clips, lateralEpsilon);

        if (clips.HasClip(AnimationKey.Start))
            return AnimationKey.Start;

        return ResolveWalkStartKey(localMoveIntent, clips, lateralEpsilon);
    }

    /// <summary>走档起步：横移优先左右 Start，再正向 WalkStart，最后 Start。</summary>
    public static AnimationKey ResolveWalkStartKey(
        Vector2 localMoveIntent,
        ILocomotionAnimClipQuery clips,
        float lateralEpsilon = LateralDominanceEpsilon)
    {
        if (clips == null)
            return AnimationKey.WalkStart;

        float eps = Mathf.Max(0.01f, lateralEpsilon);
        if (TryResolveLateralKey(
                localMoveIntent,
                clips,
                AnimationKey.WalkStartLeft,
                AnimationKey.WalkStartRight,
                out AnimationKey lateral,
                eps))
        {
            return lateral;
        }

        if (clips.HasClip(AnimationKey.WalkStart))
            return AnimationKey.WalkStart;
        if (clips.HasClip(AnimationKey.Start))
            return AnimationKey.Start;
        return AnimationKey.WalkStart;
    }

    /// <summary>是否存在任一可用起步 Clip（含左右 WalkStart）。</summary>
    public static bool HasAnyStartClip(ILocomotionAnimClipQuery clips) =>
        clips != null
        && (clips.HasClip(AnimationKey.Start)
            || clips.HasClip(AnimationKey.WalkStart)
            || clips.HasClip(AnimationKey.WalkStartLeft)
            || clips.HasClip(AnimationKey.WalkStartRight));

    /// <summary>主导横向时尝试左右 Key；无对应 Clip 则 false。</summary>
    static bool TryResolveLateralKey(
        Vector2 localMove,
        ILocomotionAnimClipQuery clips,
        AnimationKey leftKey,
        AnimationKey rightKey,
        out AnimationKey key,
        float lateralEpsilon = LateralDominanceEpsilon)
    {
        key = leftKey;
        float ax = Mathf.Abs(localMove.x);
        float ay = Mathf.Abs(localMove.y);
        if (ax < lateralEpsilon || ax < ay)
            return false;

        if (localMove.x < 0f && clips.HasClip(leftKey))
        {
            key = leftKey;
            return true;
        }

        if (localMove.x > 0f && clips.HasClip(rightKey))
        {
            key = rightKey;
            return true;
        }

        return false;
    }
}
