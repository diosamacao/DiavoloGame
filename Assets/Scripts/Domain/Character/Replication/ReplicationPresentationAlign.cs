/// <summary>
/// 客机本机表现何时跟权威快照相位：出招、受击、起步/折返/急停有烘焙位移，不能再用 Walk/Run 预测盖掉。
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
        return PredictedLocomotionVisual.TryReadPhase(in snapshot, out AnimationKey key)
            && PredictedLocomotionVisual.IsTransitionPhase(key);
    }
}
