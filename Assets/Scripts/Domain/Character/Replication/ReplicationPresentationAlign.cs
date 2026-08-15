/// <summary>
/// 他人 Proxy / 快照对齐用的 Locomotion 相位判断。
/// 本机走跑由内层机选片，禁止再猜 Idle/Walk/Run。
/// </summary>
public static class ReplicationPresentationAlign
{
    /// <summary>快照处于出招、受击或烘焙 Locomotion 相位时，位姿与 Clip 应对齐权威。</summary>
    public static bool ShouldAlignFromSnapshot(in ActorReplicationSnapshot snapshot)
    {
        if (snapshot.ActionId != 0)
            return true;
        if (snapshot.VitalityEdge == VitalityReplicationEdge.Hit
            || snapshot.VitalityEdge == VitalityReplicationEdge.Death)
            return true;
        return TryReadPhase(in snapshot, out AnimationKey key)
            && IsTransitionPhase(key);
    }

    /// <summary>起步、急停、折返等一次性相位；切键硬切并可 Seek。</summary>
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

    /// <summary>进出一次性相位硬切；Idle↔走跑冲刺走默认 CrossFade。</summary>
    public static bool ShouldHardCut(AnimationKey? previous, AnimationKey next)
    {
        if (IsTransitionPhase(next))
            return true;
        return previous.HasValue && IsTransitionPhase(previous.Value);
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
