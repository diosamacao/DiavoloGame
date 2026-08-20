/// <summary>走跑预测状态：仅毫米位姿。ActionId / Hit 不进此结构。</summary>
public readonly struct LocomotionPredictState
{
    /// <summary>创建位姿快照。</summary>
    public LocomotionPredictState(int posXMm, int posZMm, int posYMm, int facingMilliDeg)
    {
        PosXMm = posXMm;
        PosZMm = posZMm;
        PosYMm = posYMm;
        FacingMilliDeg = facingMilliDeg;
    }

    /// <summary>水平 X 毫米。</summary>
    public int PosXMm { get; }

    /// <summary>水平 Z 毫米。</summary>
    public int PosZMm { get; }

    /// <summary>竖直 Y 毫米。</summary>
    public int PosYMm { get; }

    /// <summary>朝向毫度。</summary>
    public int FacingMilliDeg { get; }

    /// <summary>从权威角色快照抽取位姿；忽略动作与生命边沿。</summary>
    public static LocomotionPredictState FromSnapshot(in ActorReplicationSnapshot snapshot) =>
        new LocomotionPredictState(
            snapshot.PosXMm,
            snapshot.PosZMm,
            snapshot.PosYMm,
            snapshot.FacingMilliDeg);
}
