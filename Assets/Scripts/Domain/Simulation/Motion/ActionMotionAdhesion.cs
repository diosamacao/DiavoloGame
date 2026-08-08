using System;

/// <summary>
/// TargetAdhesion 纯计算：连线动态 desired + 剩余帧均摊「朝向前方未到达缺口」。
/// 方案 A：desired 已不在角色朝向前方（过冲/身后）时不倒拖。
/// </summary>
public static class ActionMotionAdhesion
{
    /// <summary>
    /// 计算吸附世界毫米修正（X/Z）。
    /// 仅当 desired 仍在朝向前方时，将误差均摊到剩余帧；否则 correction=0。
    /// </summary>
    public static bool TryComputeCorrectionMm(
        int actorXMm,
        int actorZMm,
        float actorYawDegrees,
        int targetXMm,
        int targetZMm,
        in ActionMotionAdhesionParams window,
        int currentFrame,
        out int correctionXMm,
        out int correctionZMm)
    {
        correctionXMm = 0;
        correctionZMm = 0;
        if (!window.IsActiveAtFrame(currentFrame))
            return false;

        long dx = (long)targetXMm - actorXMm;
        long dz = (long)targetZMm - actorZMm;
        long distSq = dx * dx + dz * dz;
        int maxAcquire = window.MaxAcquireDistanceMm;
        if (maxAcquire > 0 && distSq > (long)maxAcquire * maxAcquire)
            return false;

        if (window.MaxAngleMilliDeg > 0 && distSq > 0)
        {
            // 连线相对角色朝向的平面夹角
            float axisYaw = (float)(Math.Atan2(dx, dz) * (180.0 / Math.PI));
            float delta = DeltaAngleDegrees(actorYawDegrees, axisYaw);
            float maxDeg = MotionQuantization.MilliDegToDegrees(window.MaxAngleMilliDeg);
            if (Math.Abs(delta) > maxDeg)
                return false;
        }

        if (!TryBuildDesiredMm(
                actorXMm,
                actorZMm,
                targetXMm,
                targetZMm,
                window.HorizontalOffsetMm,
                window.LateralOffsetMm,
                out int desiredX,
                out int desiredZ))
        {
            return false;
        }

        long errX = (long)desiredX - actorXMm;
        long errZ = (long)desiredZ - actorZMm;

        // Unity yaw：forward = (sin, 0, cos)
        double yawRad = actorYawDegrees * (Math.PI / 180.0);
        float forwardX = (float)Math.Sin(yawRad);
        float forwardZ = (float)Math.Cos(yawRad);
        // 方案 A：只补朝向前方的缺口；过冲后 desired 落到身后则不拉回
        float forwardGap = errX * forwardX + errZ * forwardZ;
        if (forwardGap <= 0f)
            return false;

        float rightX = forwardZ;
        float rightZ = -forwardX;
        float lateralGap = errX * rightX + errZ * rightZ;

        int remainingFrames = window.EndFrame - currentFrame + 1;
        if (remainingFrames < 1)
            remainingFrames = 1;

        float plannedForward = forwardGap / remainingFrames;
        float plannedLateral = lateralGap / remainingFrames;
        float plannedX = forwardX * plannedForward + rightX * plannedLateral;
        float plannedZ = forwardZ * plannedForward + rightZ * plannedLateral;

        float mag = (float)Math.Sqrt(plannedX * plannedX + plannedZ * plannedZ);
        int maxCorr = window.MaxCorrectionMmPerFrame;
        if (mag > maxCorr && mag > 0.0001f)
        {
            float scale = maxCorr / mag;
            plannedX *= scale;
            plannedZ *= scale;
        }

        correctionXMm = (int)Math.Round(plannedX, MidpointRounding.AwayFromZero);
        correctionZMm = (int)Math.Round(plannedZ, MidpointRounding.AwayFromZero);
        return correctionXMm != 0 || correctionZMm != 0;
    }

    /// <summary>
    /// desired = enemy + axis * horizontalOffset + perp * lateralOffset。
    /// axis = normalize(enemy − player)；重合时失败。
    /// </summary>
    public static bool TryBuildDesiredMm(
        int actorXMm,
        int actorZMm,
        int targetXMm,
        int targetZMm,
        int horizontalOffsetMm,
        int lateralOffsetMm,
        out int desiredXMm,
        out int desiredZMm)
    {
        desiredXMm = targetXMm;
        desiredZMm = targetZMm;

        float axisX = targetXMm - actorXMm;
        float axisZ = targetZMm - actorZMm;
        float len = (float)Math.Sqrt(axisX * axisX + axisZ * axisZ);
        if (len < 0.001f)
            return false;

        axisX /= len;
        axisZ /= len;
        // 水平法线（左向）
        float perpX = -axisZ;
        float perpZ = axisX;

        desiredXMm = targetXMm
            + (int)Math.Round(axisX * horizontalOffsetMm, MidpointRounding.AwayFromZero)
            + (int)Math.Round(perpX * lateralOffsetMm, MidpointRounding.AwayFromZero);
        desiredZMm = targetZMm
            + (int)Math.Round(axisZ * horizontalOffsetMm, MidpointRounding.AwayFromZero)
            + (int)Math.Round(perpZ * lateralOffsetMm, MidpointRounding.AwayFromZero);
        return true;
    }

    /// <summary>有符号最小角差，范围 (-180, 180]。</summary>
    static float DeltaAngleDegrees(float current, float target)
    {
        float delta = target - current;
        while (delta > 180f)
            delta -= 360f;
        while (delta <= -180f)
            delta += 360f;
        return delta;
    }
}
