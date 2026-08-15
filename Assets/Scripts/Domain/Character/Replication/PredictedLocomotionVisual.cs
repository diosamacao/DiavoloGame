/// <summary>
/// 客机本机 Locomotion 选片：权威起步/急停/折返优先；松手不得先切 Idle；
/// 冲刺用 Sprint 键而不是把 Run 加速。
/// </summary>
public static class PredictedLocomotionVisual
{
    /// <summary>起步、急停、折返等一次性相位，位姿应对齐权威，切键硬切。</summary>
    public static bool IsTransitionPhase(AnimationKey key)
    {
        switch (key)
        {
            case AnimationKey.Start:
            case AnimationKey.PivotTurn:
            case AnimationKey.StopL:
            case AnimationKey.StopR:
            case AnimationKey.StartEnd:
            case AnimationKey.WalkStart:
            case AnimationKey.WalkStartLeft:
            case AnimationKey.WalkStartRight:
                return true;
            default:
                return false;
        }
    }

    /// <summary>稳态循环步态。</summary>
    public static bool IsGaitLoop(AnimationKey key)
    {
        switch (key)
        {
            case AnimationKey.Walk:
            case AnimationKey.Run:
            case AnimationKey.Sprint:
            case AnimationKey.WalkLeft:
            case AnimationKey.WalkRight:
                return true;
            default:
                return false;
        }
    }

    /// <summary>进出一次性相位硬切；Idle↔走跑冲刺走默认 CrossFade。</summary>
    public static bool ShouldHardCut(AnimationKey? previous, AnimationKey next)
    {
        if (IsTransitionPhase(next))
            return true;
        return previous.HasValue && IsTransitionPhase(previous.Value);
    }

    /// <summary>本地升档结果对应的循环片。</summary>
    public static AnimationKey LoopKeyFromGait(LocomotionGait gait)
    {
        switch (gait)
        {
            case LocomotionGait.Sprint:
                return AnimationKey.Sprint;
            case LocomotionGait.Run:
                return AnimationKey.Run;
            default:
                return AnimationKey.Walk;
        }
    }

    /// <summary>按步态取预测水平速度；无移动时由调用方传 0。</summary>
    public static int SpeedMmForGait(LocomotionGait gait, in PredictedLocomotionConfig config)
    {
        switch (gait)
        {
            case LocomotionGait.Sprint:
                return config.SprintSpeedMm;
            case LocomotionGait.Run:
                return config.RunSpeedMm;
            case LocomotionGait.Walk:
                return config.WalkSpeedMm;
            default:
                return 0;
        }
    }

    /// <summary>
    /// 解析本机应播的 Locomotion 键。
    /// 权威过渡相位始终赢；松手时若权威仍是走跑则继续播，等 Stop/Idle。
    /// </summary>
    public static AnimationKey ResolveSelfKey(
        in ActorReplicationSnapshot authority,
        bool hasMoveIntent,
        LocomotionGait predictedGait)
    {
        AnimationKey predictedLoop = LoopKeyFromGait(predictedGait);
        if (!TryReadPhase(in authority, out AnimationKey auth))
            return hasMoveIntent ? predictedLoop : AnimationKey.Idle;

        if (IsTransitionPhase(auth))
            return auth;

        if (!hasMoveIntent)
            return auth;

        if (auth == AnimationKey.Idle)
        {
            return predictedGait == LocomotionGait.Walk
                ? AnimationKey.WalkStart
                : AnimationKey.Start;
        }

        if (auth == AnimationKey.Sprint || predictedGait == LocomotionGait.Sprint)
            return AnimationKey.Sprint;
        if (predictedGait == LocomotionGait.Run)
            return AnimationKey.Run;
        if (IsGaitLoop(auth))
            return auth;
        return predictedLoop;
    }

    /// <summary>快照相位能映射到 AnimationKey 时为 true。</summary>
    public static bool TryReadPhase(in ActorReplicationSnapshot snapshot, out AnimationKey key)
    {
        int raw = snapshot.LocomotionPhase;
        if (!System.Enum.IsDefined(typeof(AnimationKey), raw))
        {
            key = AnimationKey.Idle;
            return false;
        }

        key = (AnimationKey)raw;
        return true;
    }
}
