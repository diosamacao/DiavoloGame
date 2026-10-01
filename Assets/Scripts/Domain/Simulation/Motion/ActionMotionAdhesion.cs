using System;

/// <summary>吸附窗口位移重映射：捕获时固定偏移轴，按基础位移节奏逼近目标并在末帧收敛。</summary>
public static class ActionMotionAdhesion
{
    /// <summary>一个动作实例、窗口和目标的捕获状态；切换任一项或回退帧时必须重新捕获。</summary>
    public struct State
    {
        /// <summary>是否已通过距离、角度和非重合检查。</summary>
        public bool Acquired;
        /// <summary>捕获时的世界偏移；跨过敌人后保持方向。</summary>
        public int OffsetXMm, OffsetZMm;
        /// <summary>最后一次计算的目标落点，供预览标记使用。</summary>
        public int DesiredXMm, DesiredZMm;
        /// <summary>上次计算帧；回退时重新捕获，卡肉不调用计算。</summary>
        public int LastFrame;
    }

    /// <summary>
    /// 返回本帧完整世界位移，替代基础位移而非在已移动角色上追加。
    /// progress 是当前基础路程占窗口剩余路程的比例；零路程按剩余帧均摊。
    /// 修正上限仅限制中间帧，末帧以精确落点为准；调用者仍通过碰撞电机移动。
    /// </summary>
    public static bool TryComputeDisplacementMm(ref State state,
        int actorXMm, int actorZMm, float actorYawDegrees, int targetXMm, int targetZMm,
        in ActionMotionAdhesionParams window, int currentFrame,
        int baseXMm, int baseZMm, double progress, out int moveXMm, out int moveZMm)
    {
        moveXMm = baseXMm; moveZMm = baseZMm;
        if (!window.IsActiveAtFrame(currentFrame)) return false;
        if (state.Acquired && currentFrame < state.LastFrame) state = default;
        if (!state.Acquired)
        {
            double dx = (double)targetXMm - actorXMm, dz = (double)targetZMm - actorZMm;
            double distanceSquared = dx * dx + dz * dz;
            if (window.MaxAcquireDistanceMm > 0
                && distanceSquared > (double)window.MaxAcquireDistanceMm * window.MaxAcquireDistanceMm) return false;
            if (window.MaxAngleMilliDeg > 0 && distanceSquared > 0)
            {
                float axisYaw = (float)(Math.Atan2(dx, dz) * (180.0 / Math.PI));
                if (Math.Abs(DeltaAngleDegrees(actorYawDegrees, axisYaw))
                    > MotionQuantization.MilliDegToDegrees(window.MaxAngleMilliDeg)) return false;
            }
            if (!TryBuildDesiredMm(actorXMm, actorZMm, targetXMm, targetZMm,
                    window.HorizontalOffsetMm, window.LateralOffsetMm, out int x, out int z)) return false;
            state.Acquired = true;
            state.OffsetXMm = x - targetXMm; state.OffsetZMm = z - targetZMm;
        }
        state.LastFrame = currentFrame;
        state.DesiredXMm = targetXMm + state.OffsetXMm;
        state.DesiredZMm = targetZMm + state.OffsetZMm;
        double gapX = (double)state.DesiredXMm - actorXMm, gapZ = (double)state.DesiredZMm - actorZMm;
        bool finalFrame = currentFrame == window.EndFrame;
        double weight = finalFrame ? 1 : Math.Max(0, Math.Min(1, progress));
        double correctionX = gapX * weight - baseXMm, correctionZ = gapZ * weight - baseZMm;
        double magnitude = Math.Sqrt(correctionX * correctionX + correctionZ * correctionZ);
        if (!finalFrame && magnitude > window.MaxCorrectionMmPerFrame)
        {
            double scale = window.MaxCorrectionMmPerFrame / magnitude;
            correctionX *= scale; correctionZ *= scale;
        }
        moveXMm = (int)Math.Round(baseXMm + correctionX, MidpointRounding.AwayFromZero);
        moveZMm = (int)Math.Round(baseZMm + correctionZ, MidpointRounding.AwayFromZero);
        return true;
    }

    /// <summary>按平面过滤后的烘焙路程保持快慢节奏；剩余无位移时改用剩余帧比例。</summary>
    public static double BakedProgress(ActionBakedMotion baked, int frame, int endFrame)
    {
        double current = 0, remaining = 0;
        if (baked != null && baked.IsReady)
            for (int i = Math.Max(0, frame); i <= endFrame && i < baked.frameCount; i++)
            {
                baked.TryGetDelta(i, out SimVec2 delta, out _);
                int x = delta.X, z = delta.Z;
                double length = Math.Sqrt((double)x * x + (double)z * z);
                if (i == frame) current = length;
                remaining += length;
            }
        return remaining > 0 ? current / remaining : 1d / Math.Max(1, endFrame - frame + 1);
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
