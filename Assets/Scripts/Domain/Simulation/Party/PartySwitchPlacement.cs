
/// <summary>计算普通换人的确定性逻辑落点，不依赖相机或 Unity Transform。</summary>
public static class PartySwitchPlacement
{
    /// <summary>普通换人时新角色位于旧角色局部右侧 0.6 米。</summary>
    public const int NormalSwitchRightOffsetMm = 1000;

    /// <summary>按退场角色的逻辑朝向计算新角色右侧落点。</summary>
    public static SimVec2 ResolveNormalSwitchPosition(
        SimVec2 outgoingPosition,
        int outgoingFacingMilliDeg)
    {
        CharacterMotorSim.RotateLocalToWorld(
            outgoingFacingMilliDeg,
            NormalSwitchRightOffsetMm,
            0,
            out int offsetXMm,
            out int offsetZMm);
        return new SimVec2(
            outgoingPosition.X + offsetXMm,
            outgoingPosition.Z + offsetZMm);
    }
}
