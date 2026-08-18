/// <summary>Match 分配的权威出生位姿；毫米与毫度，不依赖 Host Root。</summary>
public readonly struct MatchSpawnPose
{
    /// <summary>按槽位创建出生位姿。</summary>
    public MatchSpawnPose(int xMm, int yMm, int zMm, int facingMilliDeg)
    {
        XMm = xMm;
        YMm = yMm;
        ZMm = zMm;
        FacingMilliDeg = facingMilliDeg;
    }

    /// <summary>世界 X，毫米。</summary>
    public int XMm { get; }

    /// <summary>世界 Y，毫米。</summary>
    public int YMm { get; }

    /// <summary>世界 Z，毫米。</summary>
    public int ZMm { get; }

    /// <summary>朝向，毫度。</summary>
    public int FacingMilliDeg { get; }
}
