using System;
using UnityEditor;
using UnityEngine;

/// <summary>从 RootMotion AnimationClip 采样水平位移并量化为 ActionBakedMotion（不含偏航）。</summary>
public static class RootMotionBakeUtility
{
    /// <summary>按 logicHz 从 RM Clip 烘焙整段水平位移表；yaw 恒为 0（朝向由运行时索敌/输入控制）。</summary>
    public static ActionBakedMotion BakeClip(
        AnimationClip rootMotionClip,
        int logicHz,
        ActionMotionPlanarMode planarMode,
        string matchedRootMotionName,
        string inplaceContentHash,
        string rootMotionContentHash)
    {
        var result = ActionBakedMotion.CreateEmpty();
        result.logicHz = Mathf.Max(1, logicHz);
        result.planarMode = planarMode;
        result.matchedRootMotionName = matchedRootMotionName ?? string.Empty;
        result.inplaceContentHash = inplaceContentHash ?? string.Empty;
        result.rootMotionContentHash = rootMotionContentHash ?? string.Empty;

        if (rootMotionClip == null || rootMotionClip.length <= 0f)
        {
            result.bakeStatus = ActionBakedMotionStatus.Failed;
            return result;
        }

        // 复用 Locomotion 曲线解析；位置相对 Clip 起点，已在起点朝向空间
        LocomotionRootMotionTrack track = LocomotionRootMotionBaker.Bake(
            rootMotionClip,
            result.logicHz);
        if (!track.IsValid)
        {
            result.bakeStatus = ActionBakedMotionStatus.Failed;
            return result;
        }

        int frameCount = Mathf.Max(1, Mathf.CeilToInt(rootMotionClip.length * result.logicHz));
        var dx = new int[frameCount];
        var dz = new int[frameCount];
        // 朝向不进表：避免 Humanoid RootQ→euler.y 假偏航污染运行时
        var yaw = new int[frameCount];

        for (int frame = 0; frame < frameCount; frame++)
        {
            float t0 = frame / (float)result.logicHz;
            float t1 = (frame + 1) / (float)result.logicHz;
            if (!track.TryGetDelta(t0, t1, out Vector3 localDelta, out _))
            {
                dx[frame] = 0;
                dz[frame] = 0;
                yaw[frame] = 0;
                continue;
            }

            // 直接使用 Clip 起点局部水平差分，不再用 RootQ yaw 做 Inverse 投影
            dx[frame] = MotionQuantization.MetersToMm(localDelta.x);
            dz[frame] = MotionQuantization.MetersToMm(localDelta.z);
            yaw[frame] = 0;
        }

        result.frameCount = frameCount;
        result.positionDeltaMmX = dx;
        result.positionDeltaMmZ = dz;
        result.yawDeltaMilliDeg = yaw;
        result.bakeStatus = ActionBakedMotionStatus.Ok;
        return result;
    }

    /// <summary>用 Clip 路径+时长+帧率生成稳定指纹（脏检测用）。</summary>
    public static string ComputeClipContentHash(AnimationClip clip)
    {
        if (clip == null)
            return string.Empty;

        string path = AssetDatabase.GetAssetPath(clip);
        return $"{path}|{clip.name}|{clip.length:F5}|{clip.frameRate:F3}";
    }

    /// <summary>
    /// 与 <see cref="BakeClip"/> 相同的帧数估算（Ceil(length*Hz)），不采样曲线。
    /// 供 Dirty 指纹热路径使用，避免 Inspector 重绘时重复烘焙。
    /// </summary>
    public static int EstimateFrameCount(AnimationClip rootMotionClip, int logicHz)
    {
        if (rootMotionClip == null || rootMotionClip.length <= 0f)
            return 0;

        return Mathf.Max(1, Mathf.CeilToInt(rootMotionClip.length * Mathf.Max(1, logicHz)));
    }

    /// <summary>校验累计水平位移误差（米）；超阈值返回 false。</summary>
    public static bool ValidateAgainstTrack(
        AnimationClip rootMotionClip,
        ActionBakedMotion table,
        float maxAccumulatedErrorMeters,
        float maxSingleFrameErrorMeters,
        out string report)
    {
        report = string.Empty;
        if (rootMotionClip == null || table == null || !table.IsReady)
        {
            report = "表无效或 Clip 为空";
            return false;
        }

        LocomotionRootMotionTrack track = LocomotionRootMotionBaker.Bake(
            rootMotionClip,
            table.logicHz);
        if (!track.IsValid)
        {
            report = "无法从 RM Clip 采样校验轨";
            return false;
        }

        Vector3 accumSource = Vector3.zero;
        Vector3 accumTable = Vector3.zero;
        float maxFrameErr = 0f;

        for (int frame = 0; frame < table.frameCount; frame++)
        {
            float t0 = frame / (float)table.logicHz;
            float t1 = (frame + 1) / (float)table.logicHz;
            track.TryGetDelta(t0, t1, out Vector3 srcDelta, out _);
            Vector3 srcLocal = new(srcDelta.x, 0f, srcDelta.z);
            // 与 TryGetDelta 对齐：按 planarMode 在本地 mm 上投影后再比
            int dxMm = MotionQuantization.MetersToMm(srcLocal.x);
            int dzMm = MotionQuantization.MetersToMm(srcLocal.z);
            ActionBakedMotion.ApplyPlanarMode(table.planarMode, ref dxMm, ref dzMm);
            srcLocal = new Vector3(
                MotionQuantization.MmToMeters(dxMm),
                0f,
                MotionQuantization.MmToMeters(dzMm));
            accumSource += srcLocal;

            table.TryGetDelta(frame, out SimVec2 mm, out _);
            Vector3 tableDelta = new(
                MotionQuantization.MmToMeters(mm.X),
                0f,
                MotionQuantization.MmToMeters(mm.Z));
            accumTable += tableDelta;

            float frameErr = Vector3.Distance(srcLocal, tableDelta);
            if (frameErr > maxFrameErr)
                maxFrameErr = frameErr;
        }

        float accumErr = Vector3.Distance(accumSource, accumTable);
        report =
            $"accumErr={accumErr * 100f:F2}cm, maxFrameErr={maxFrameErr * 1000f:F2}mm, "
            + $"frames={table.frameCount}, rm={table.matchedRootMotionName}";

        return accumErr <= maxAccumulatedErrorMeters
            && maxFrameErr <= maxSingleFrameErrorMeters;
    }
}
