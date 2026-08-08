using UnityEngine;

/// <summary>按 FacingPolicy 解析重定位后朝向（度）。</summary>
public static class ActionMotionFacing
{
    /// <summary>解析朝向；FaceDestination 在零位移时回退 PreserveCurrent。</summary>
    public static float ResolveDegrees(
        MotionFacingPolicy policy,
        float actorYawDegrees,
        in SimCombatPose actorPose,
        in SimCombatPose targetPose,
        SimVec2 fromMm,
        SimVec2 resolvedMm)
    {
        switch (policy)
        {
            case MotionFacingPolicy.FaceTarget:
            {
                float dx = targetPose.Position.x - actorPose.Position.x;
                float dz = targetPose.Position.z - actorPose.Position.z;
                if (dx * dx + dz * dz < 0.0001f)
                    return actorYawDegrees;
                return Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            }
            case MotionFacingPolicy.MatchTarget:
                return targetPose.YawDegrees;
            case MotionFacingPolicy.FaceDestination:
            {
                int dx = resolvedMm.X - fromMm.X;
                int dz = resolvedMm.Z - fromMm.Z;
                if (dx == 0 && dz == 0)
                    return actorYawDegrees;
                return Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            }
            default:
                return actorYawDegrees;
        }
    }
}
