/// <summary>重定位落点解析（静物碰撞）；IgnoreCharacters 与 FindNearestValid 首版等同直接尝试 desired。</summary>
public static class ActionMotionRelocation
{
    /// <summary>尝试解析到 desired；失败返回 false。</summary>
    public static bool TryResolve(
        CharacterMotorSim motor,
        ISimCollisionWorld collision,
        int desiredXMm,
        int desiredZMm,
        MotionCollisionPolicy policy,
        out SimVec2 resolved)
    {
        resolved = default;
        if (motor == null || collision == null)
            return false;

        SimVec2 from = motor.PositionMm;
        var desired = new SimVec2(desiredXMm, desiredZMm);

        if (policy == MotionCollisionPolicy.IgnoreAll)
        {
            resolved = desired;
            return true;
        }

        // RequireFreeSpace / FindNearestValid / IgnoreCharacters：均走静物 ResolveMove
        // （角色软体由 SoftBodySuppress 另控，不在此过滤）
        SimVec2 moved = collision.ResolveMove(from, desired, motor.RadiusMm);
        if (policy == MotionCollisionPolicy.RequireFreeSpace
            && (moved.X != desiredXMm || moved.Z != desiredZMm))
        {
            return false;
        }

        resolved = moved;
        return true;
    }
}
