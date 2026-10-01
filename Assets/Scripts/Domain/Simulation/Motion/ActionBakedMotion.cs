using System;

/// <summary>保存招式原始水平差分，并统一派生逻辑轨迹与模型残差（毫米）；朝向不由此表驱动。</summary>
[Serializable]
public sealed class ActionBakedMotion
{
    public int logicHz = ActionSim.LogicHz;
    public int frameCount;
    public ActionMotionPlanarMode planarMode = ActionMotionPlanarMode.FullPlanar;
    public int[] positionDeltaMmX = Array.Empty<int>();
    public int[] positionDeltaMmZ = Array.Empty<int>();
    public int[] yawDeltaMilliDeg = Array.Empty<int>();
    public string inplaceContentHash = string.Empty;
    public string rootMotionContentHash = string.Empty;
    public string matchedRootMotionName = string.Empty;
    public ActionBakedMotionStatus bakeStatus = ActionBakedMotionStatus.None;

    /// <summary>空表（未烘焙）。</summary>
    public static ActionBakedMotion CreateEmpty() => new();

    /// <summary>是否可安全查表。</summary>
    public bool IsReady =>
        bakeStatus == ActionBakedMotionStatus.Ok
        && frameCount > 0
        && positionDeltaMmX != null
        && positionDeltaMmZ != null
        && yawDeltaMilliDeg != null
        && positionDeltaMmX.Length == frameCount
        && positionDeltaMmZ.Length == frameCount
        && yawDeltaMilliDeg.Length == frameCount;

    /// <summary>按逻辑帧取本地水平 Δ；越界钳到最后一帧。yawMilliDeg 恒为 0（朝向不由运动表驱动）。</summary>
    public bool TryGetDelta(int frame, out SimVec2 deltaMm, out int yawMilliDeg)
    {
        deltaMm = SimVec2.Zero;
        yawMilliDeg = 0;
        if (!IsReady)
            return false;

        int index = frame < 0 ? 0 : frame;
        if (index >= frameCount)
            index = frameCount - 1;

        if (planarMode == ActionMotionPlanarMode.EndpointSigned)
        {
            GetCumulativeAndEndpoint(index, out long x, out long z, out long endX, out long endZ);
            ProjectCumulative(x, z, endX, endZ, out long currentX, out long currentZ);
            ProjectCumulative(x - positionDeltaMmX[index], z - positionDeltaMmZ[index],
                endX, endZ, out long previousX, out long previousZ);
            // 对累计位置量化后再差分，避免逐帧取整误差累积成非零末帧残差。
            deltaMm = new SimVec2((int)(currentX - previousX), (int)(currentZ - previousZ));
        }
        else
        {
            deltaMm = new SimVec2(positionDeltaMmX[index], positionDeltaMmZ[index]);
        }
        // 即使旧资产里残留非零 yaw 数组，查表也不向外提供偏航
        yawMilliDeg = 0;
        return true;
    }

    /// <summary>用烘焙结果覆盖本实例字段（供 Editor 写回）。</summary>
    public void CopyFrom(ActionBakedMotion source)
    {
        if (source == null)
        {
            Clear();
            return;
        }

        logicHz = source.logicHz;
        frameCount = source.frameCount;
        planarMode = source.planarMode;
        positionDeltaMmX = CloneArray(source.positionDeltaMmX);
        positionDeltaMmZ = CloneArray(source.positionDeltaMmZ);
        yawDeltaMilliDeg = CloneArray(source.yawDeltaMilliDeg);
        inplaceContentHash = source.inplaceContentHash ?? string.Empty;
        rootMotionContentHash = source.rootMotionContentHash ?? string.Empty;
        matchedRootMotionName = source.matchedRootMotionName ?? string.Empty;
        bakeStatus = source.bakeStatus;
    }

    /// <summary>重置为未烘焙。</summary>
    public void Clear()
    {
        logicHz = ActionSim.LogicHz;
        frameCount = 0;
        planarMode = ActionMotionPlanarMode.FullPlanar;
        positionDeltaMmX = Array.Empty<int>();
        positionDeltaMmZ = Array.Empty<int>();
        yawDeltaMilliDeg = Array.Empty<int>();
        inplaceContentHash = string.Empty;
        rootMotionContentHash = string.Empty;
        matchedRootMotionName = string.Empty;
        bakeStatus = ActionBakedMotionStatus.None;
    }

    /// <summary>
    /// 相对逻辑轨迹的累计本地残差：Full - Gameplay，与 TryGetDelta 使用相同的累计位置量化。
    /// EndpointSigned 保留偏离起终点连线的摆动，完整动作末帧残差严格为零。
    /// </summary>
    public bool TryGetVisualResidualMm(int frame, out int residualMmX, out int residualMmZ)
    {
        residualMmX = 0;
        residualMmZ = 0;
        if (!IsReady)
            return false;

        if (planarMode != ActionMotionPlanarMode.EndpointSigned)
            return true;

        int index = frame < 0 ? 0 : frame;
        if (index >= frameCount)
            index = frameCount - 1;

        GetCumulativeAndEndpoint(index, out long fullX, out long fullZ, out long endX, out long endZ);
        ProjectCumulative(fullX, fullZ, endX, endZ, out long gameX, out long gameZ);
        residualMmX = (int)(fullX - gameX);
        residualMmZ = (int)(fullZ - gameZ);
        return true;
    }

    // 端点必须来自裁剪、拼接完成的整张表，不能使用单个来源 Clip 的末帧。
    void GetCumulativeAndEndpoint(int index, out long x, out long z, out long endX, out long endZ)
    {
        x = z = endX = endZ = 0;
        for (int i = 0; i < frameCount; i++)
        {
            endX += positionDeltaMmX[i];
            endZ += positionDeltaMmZ[i];
            if (i == index)
            {
                x = endX;
                z = endZ;
            }
        }
    }

    // 不钳制投影比例，保留超过终点后的回撤及起手后退；零净位移没有可投影的方向。
    static void ProjectCumulative(long x, long z, long endX, long endZ, out long gameX, out long gameZ)
    {
        if (x == endX && z == endZ)
        {
            gameX = endX;
            gameZ = endZ;
            return;
        }

        double lengthSquared = (double)endX * endX + (double)endZ * endZ;
        double progress = lengthSquared == 0 ? 0 : ((double)x * endX + (double)z * endZ) / lengthSquared;
        gameX = (long)Math.Round(endX * progress, MidpointRounding.AwayFromZero);
        gameZ = (long)Math.Round(endZ * progress, MidpointRounding.AwayFromZero);
    }

    static int[] CloneArray(int[] source)
    {
        if (source == null || source.Length == 0)
            return Array.Empty<int>();
        var copy = new int[source.Length];
        Array.Copy(source, copy, source.Length);
        return copy;
    }
}
