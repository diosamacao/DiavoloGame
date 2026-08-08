using UnityEditor;
using UnityEngine;

/// <summary>
/// 在 Scene 中绘制烘焙运动表累计轨迹（原始 FullPlanar 与当前 planarMode 生效后），
/// 并可选标出当前预览帧落点。
/// </summary>
public static class ActionMotionTrajectorySceneDrawing
{
    /// <summary>相对 root 当前位姿绘制累计轨迹（root 未被动画预览挪走时使用）。</summary>
    public static void DrawBakedTrajectories(
        ActionDefinition action,
        Transform root,
        int previewFrame = -1)
    {
        if (root == null)
            return;

        DrawBakedTrajectories(action, root.position, root.rotation, previewFrame);
    }

    /// <summary>
    /// 相对指定世界原点绘制累计轨迹。
    /// Action Editor 预览会挪动角色根时，必须传入预览原点而非当前 Transform。
    /// </summary>
    /// <param name="previewFrame">≥0 时在 Full/Gameplay 路径上标出该帧累计落点。</param>
    public static void DrawBakedTrajectories(
        ActionDefinition action,
        Vector3 originPosition,
        Quaternion originRotation,
        int previewFrame = -1)
    {
        if (action == null)
            return;

        ActionBakedMotion baked = action.BakedMotion;
        if (baked == null || !baked.IsReady || baked.frameCount <= 0)
            return;

        DrawPolyline(originPosition, originRotation, baked, applyPlanarMode: false, new Color(1f, 0.55f, 0.15f, 0.95f), 3f);
        DrawPolyline(originPosition, originRotation, baked, applyPlanarMode: true, new Color(0.2f, 0.85f, 1f, 0.95f), 2f);
        DrawResidualPolyline(originPosition, originRotation, baked, new Color(1f, 0.3f, 0.85f, 0.9f), 2f);

        if (previewFrame >= 0)
            DrawPreviewFrameMarkers(originPosition, originRotation, baked, previewFrame);

        Handles.color = Color.white;
        Handles.Label(
            originPosition + Vector3.up * 0.05f,
            $"{action.name}\nOrange=Full  Cyan=Gameplay({baked.planarMode})  Magenta=Residual");
    }

    /// <summary>
    /// 累计到含 previewFrame 在内的本地水平位移（米）。
    /// applyPlanarMode=true 为 Gameplay 路径，否则为原始 Full。
    /// </summary>
    public static bool TryGetCumulativeLocalMeters(
        ActionBakedMotion baked,
        int previewFrame,
        bool applyPlanarMode,
        out Vector3 localMeters)
    {
        localMeters = Vector3.zero;
        if (baked == null || !baked.IsReady || baked.frameCount <= 0)
            return false;

        int index = previewFrame < 0 ? 0 : previewFrame;
        if (index >= baked.frameCount)
            index = baked.frameCount - 1;

        long xMm = 0;
        long zMm = 0;
        for (int i = 0; i <= index; i++)
        {
            int dx = baked.positionDeltaMmX[i];
            int dz = baked.positionDeltaMmZ[i];
            if (applyPlanarMode)
                ActionBakedMotion.ApplyPlanarMode(baked.planarMode, ref dx, ref dz);

            xMm += dx;
            zMm += dz;
        }

        localMeters = new Vector3(
            MotionQuantization.MmToMeters((int)xMm),
            0f,
            MotionQuantization.MmToMeters((int)zMm));
        return true;
    }

    /// <summary>在 Full / Gameplay 累计点上画预览帧标记。</summary>
    static void DrawPreviewFrameMarkers(
        Vector3 originPosition,
        Quaternion originRotation,
        ActionBakedMotion baked,
        int previewFrame)
    {
        if (!TryGetCumulativeLocalMeters(baked, previewFrame, applyPlanarMode: false, out Vector3 fullLocal))
            return;

        TryGetCumulativeLocalMeters(baked, previewFrame, applyPlanarMode: true, out Vector3 gameLocal);

        Vector3 fullWorld = LocalToWorld(originPosition, originRotation, fullLocal);
        Vector3 gameWorld = LocalToWorld(originPosition, originRotation, gameLocal);

        Handles.color = new Color(1f, 0.55f, 0.15f, 1f);
        Handles.SphereHandleCap(0, fullWorld, Quaternion.identity, 0.08f, EventType.Repaint);
        Handles.color = new Color(0.2f, 0.85f, 1f, 1f);
        Handles.SphereHandleCap(0, gameWorld, Quaternion.identity, 0.07f, EventType.Repaint);

        // 从原点连到当前 Gameplay 落点，强调 scrub 位移
        Handles.color = new Color(0.2f, 0.85f, 1f, 0.55f);
        Handles.DrawLine(originPosition, gameWorld);
    }

    /// <summary>绘制相对 Gameplay 的残差累计路径（从原点出发的偏移折线）。</summary>
    static void DrawResidualPolyline(
        Vector3 originPosition,
        Quaternion originRotation,
        ActionBakedMotion baked,
        Color color,
        float thickness)
    {
        var points = new Vector3[baked.frameCount];
        for (int i = 0; i < baked.frameCount; i++)
        {
            baked.TryGetVisualResidualMm(i, out int rx, out int rz);
            Vector3 local = new(
                MotionQuantization.MmToMeters(rx),
                0.02f,
                MotionQuantization.MmToMeters(rz));
            points[i] = LocalToWorld(originPosition, originRotation, local);
        }

        Handles.color = color;
#if UNITY_2020_2_OR_NEWER
        Handles.DrawAAPolyLine(thickness, points);
#else
        Handles.DrawPolyLine(points);
#endif
    }

    static void DrawPolyline(
        Vector3 originPosition,
        Quaternion originRotation,
        ActionBakedMotion baked,
        bool applyPlanarMode,
        Color color,
        float thickness)
    {
        var points = new Vector3[baked.frameCount + 1];
        long xMm = 0;
        long zMm = 0;
        points[0] = originPosition;

        for (int i = 0; i < baked.frameCount; i++)
        {
            int dx = baked.positionDeltaMmX[i];
            int dz = baked.positionDeltaMmZ[i];
            if (applyPlanarMode)
                ActionBakedMotion.ApplyPlanarMode(baked.planarMode, ref dx, ref dz);

            xMm += dx;
            zMm += dz;
            Vector3 local = new(
                MotionQuantization.MmToMeters((int)xMm),
                0f,
                MotionQuantization.MmToMeters((int)zMm));
            points[i + 1] = LocalToWorld(originPosition, originRotation, local);
        }

        Handles.color = color;
#if UNITY_2020_2_OR_NEWER
        Handles.DrawAAPolyLine(thickness, points);
#else
        Handles.DrawPolyLine(points);
#endif
    }

    static Vector3 LocalToWorld(Vector3 originPosition, Quaternion originRotation, Vector3 local) =>
        originPosition + originRotation * local;
}
