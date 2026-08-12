using System;
using UnityEngine;

/// <summary>
/// Locomotion 选片真源（L-DIR1/2）：gait×cardinal → AnimationKey；
/// Clip 仍挂 CharacterAnimationProfile；回退链只在本表。
/// </summary>
[Serializable]
public sealed class LocomotionAnimSet
{
    [Header("Walk Loop")]
    [SerializeField] AnimationKey walkLoopForward = AnimationKey.Walk;
    [SerializeField] AnimationKey walkLoopBack = AnimationKey.Walk;
    [SerializeField] AnimationKey walkLoopLeft = AnimationKey.WalkLeft;
    [SerializeField] AnimationKey walkLoopRight = AnimationKey.WalkRight;

    [Header("Walk Start")]
    [SerializeField] AnimationKey walkStartForward = AnimationKey.WalkStart;
    [SerializeField] AnimationKey walkStartBack = AnimationKey.WalkStart;
    [SerializeField] AnimationKey walkStartLeft = AnimationKey.WalkStartLeft;
    [SerializeField] AnimationKey walkStartRight = AnimationKey.WalkStartRight;

    [Header("Run / Sprint Loop")]
    [SerializeField] AnimationKey runLoopForward = AnimationKey.Run;
    [SerializeField] AnimationKey sprintLoopForward = AnimationKey.Sprint;

    [Header("Run Start")]
    [Tooltip("跑档起步；缺片回退 Walk Start 链。")]
    [SerializeField] AnimationKey runStartForward = AnimationKey.Start;

    [Header("Shared")]
    [SerializeField] AnimationKey idle = AnimationKey.Idle;
    [SerializeField] AnimationKey pivotTurn = AnimationKey.PivotTurn;
    [SerializeField] AnimationKey stopL = AnimationKey.StopL;
    [SerializeField] AnimationKey stopR = AnimationKey.StopR;
    [SerializeField] AnimationKey startEnd = AnimationKey.StartEnd;

    /// <summary>默认表：槽位指向既有 AnimationKey，现有 AnimationProfile 可零改资产工作。</summary>
    public static LocomotionAnimSet CreateDefault() => new LocomotionAnimSet();

    public AnimationKey Idle => idle;
    public AnimationKey PivotTurn => pivotTurn;
    public AnimationKey StopL => stopL;
    public AnimationKey StopR => stopR;
    public AnimationKey StartEnd => startEnd;

    /// <summary>是否存在任一可用起步槽 Clip。</summary>
    public bool HasAnyStartClip(ILocomotionAnimClipQuery clips)
    {
        if (clips == null)
            return false;
        return clips.HasClip(runStartForward)
            || clips.HasClip(walkStartForward)
            || clips.HasClip(walkStartBack)
            || clips.HasClip(walkStartLeft)
            || clips.HasClip(walkStartRight)
            || clips.HasClip(AnimationKey.Start)
            || clips.HasClip(AnimationKey.WalkStart)
            || clips.HasClip(AnimationKey.WalkStartLeft)
            || clips.HasClip(AnimationKey.WalkStartRight);
    }

    /// <summary>解析起步槽；回退链固定在 AnimSet（非 State）。</summary>
    public AnimationKey ResolveStart(
        LocomotionGait gait,
        MoveCardinal cardinal,
        ILocomotionAnimClipQuery clips)
    {
        if (clips == null)
            return AnimationKey.Start;

        if (gait == LocomotionGait.Walk)
            return ResolveWalkStart(cardinal, clips);

        // Run/Sprint：优先跑起步 Fwd，缺则走起步链
        if (clips.HasClip(runStartForward))
            return runStartForward;
        if (clips.HasClip(AnimationKey.Start))
            return AnimationKey.Start;
        return ResolveWalkStart(cardinal, clips);
    }

    /// <summary>解析循环槽；缺片回退 Fwd→Walk/Run；Sprint 缺片回退 Run。</summary>
    public AnimationKey ResolveLoop(
        LocomotionGait gait,
        MoveCardinal cardinal,
        ILocomotionAnimClipQuery clips)
    {
        if (clips == null)
            return AnimationKey.Walk;

        switch (gait)
        {
            case LocomotionGait.Sprint:
                return ResolveWithFallback(sprintLoopForward, AnimationKey.Run, clips);
            case LocomotionGait.Run:
                return ResolveWithFallback(runLoopForward, AnimationKey.Run, clips);
            default:
                return ResolveWalkLoop(cardinal, clips);
        }
    }

    AnimationKey ResolveWalkStart(MoveCardinal cardinal, ILocomotionAnimClipQuery clips)
    {
        AnimationKey preferred = cardinal switch
        {
            MoveCardinal.Left => walkStartLeft,
            MoveCardinal.Right => walkStartRight,
            MoveCardinal.Back => walkStartBack,
            _ => walkStartForward,
        };

        if (clips.HasClip(preferred))
            return preferred;
        if (preferred != walkStartForward && clips.HasClip(walkStartForward))
            return walkStartForward;
        if (clips.HasClip(AnimationKey.WalkStart))
            return AnimationKey.WalkStart;
        if (clips.HasClip(AnimationKey.Start))
            return AnimationKey.Start;
        return preferred;
    }

    AnimationKey ResolveWalkLoop(MoveCardinal cardinal, ILocomotionAnimClipQuery clips)
    {
        AnimationKey preferred = cardinal switch
        {
            MoveCardinal.Left => walkLoopLeft,
            MoveCardinal.Right => walkLoopRight,
            MoveCardinal.Back => walkLoopBack,
            _ => walkLoopForward,
        };

        if (clips.HasClip(preferred))
            return preferred;

        if (preferred != walkLoopForward && clips.HasClip(walkLoopForward))
            return walkLoopForward;

        if (clips.HasClip(AnimationKey.Walk))
            return AnimationKey.Walk;

        return preferred;
    }

    static AnimationKey ResolveWithFallback(
        AnimationKey preferred,
        AnimationKey fallback,
        ILocomotionAnimClipQuery clips)
    {
        if (clips.HasClip(preferred))
            return preferred;
        if (clips.HasClip(fallback))
            return fallback;
        return preferred;
    }
}
