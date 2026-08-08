using System;

/// <summary>招式权威运动表：逐逻辑帧本地水平 Δ（毫米）与偏航 Δ（毫度）。</summary>
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

        int dx = positionDeltaMmX[index];
        int dz = positionDeltaMmZ[index];
        ApplyPlanarMode(planarMode, ref dx, ref dz);
        deltaMm = new SimVec2(dx, dz);
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
    /// Wave 2：相对 Gameplay 路径的视觉残差（累计绝对本地毫米）。
    /// Full = 原始累计；Gameplay = 对每帧 Δ 做 planarMode 后再累计；Residual = Full - Gameplay。
    /// 运行时派生，无需另存数组；ForwardSigned 时残差主要为横向。
    /// </summary>
    public bool TryGetVisualResidualMm(int frame, out int residualMmX, out int residualMmZ)
    {
        residualMmX = 0;
        residualMmZ = 0;
        if (!IsReady)
            return false;

        int index = frame < 0 ? 0 : frame;
        if (index >= frameCount)
            index = frameCount - 1;

        long fullX = 0;
        long fullZ = 0;
        long gameX = 0;
        long gameZ = 0;
        for (int i = 0; i <= index; i++)
        {
            int dx = positionDeltaMmX[i];
            int dz = positionDeltaMmZ[i];
            fullX += dx;
            fullZ += dz;

            int gdx = dx;
            int gdz = dz;
            ApplyPlanarMode(planarMode, ref gdx, ref gdz);
            gameX += gdx;
            gameZ += gdz;
        }

        residualMmX = (int)(fullX - gameX);
        residualMmZ = (int)(fullZ - gameZ);
        return true;
    }

    /// <summary>
    /// 按 planarMode 投影单帧原始 Δ。
    /// ForwardSigned：丢弃 dx、保留 dz；FullPlanar / 未知值保持原 Δ。
    /// </summary>
    public static void ApplyPlanarMode(ActionMotionPlanarMode mode, ref int dxMm, ref int dzMm)
    {
        if (mode == ActionMotionPlanarMode.ForwardSigned)
            dxMm = 0;
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
