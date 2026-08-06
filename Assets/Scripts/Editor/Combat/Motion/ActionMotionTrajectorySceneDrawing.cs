using UnityEditor;
using UnityEngine;

/// <summary>
/// Wave 0：在 Scene 中绘制烘焙运动表累计轨迹（原始 FullPlanar 与当前 planarMode 生效后），用于对照横摆来源。
/// </summary>
public static class ActionMotionTrajectorySceneDrawing
{
    /// <summary>相对 root 本地 XY 平面绘制累计轨迹；Y 贴 root 高度。</summary>
    public static void DrawBakedTrajectories(ActionDefinition action, Transform root)
    {
        if (action == null || root == null)
            return;

        ActionBakedMotion baked = action.BakedMotion;
        if (baked == null || !baked.IsReady || baked.frameCount <= 0)
            return;

        DrawPolyline(root, baked, applyPlanarMode: false, new Color(1f, 0.55f, 0.15f, 0.95f), 3f);
        DrawPolyline(root, baked, applyPlanarMode: true, new Color(0.2f, 0.85f, 1f, 0.95f), 2f);
        DrawResidualPolyline(root, baked, new Color(1f, 0.3f, 0.85f, 0.9f), 2f);

        Handles.color = Color.white;
        Handles.Label(
            root.position + Vector3.up * 0.05f,
            $"{action.name}\nOrange=Full  Cyan=Gameplay({baked.planarMode})  Magenta=Residual");
    }

    /// <summary>绘制相对 Gameplay 的残差累计路径（从 root 出发的偏移折线）。</summary>
    static void DrawResidualPolyline(Transform root, ActionBakedMotion baked, Color color, float thickness)
    {
        var points = new Vector3[baked.frameCount];
        for (int i = 0; i < baked.frameCount; i++)
        {
            baked.TryGetVisualResidualMm(i, out int rx, out int rz);
            Vector3 local = new(
                MotionQuantization.MmToMeters(rx),
                0.02f,
                MotionQuantization.MmToMeters(rz));
            points[i] = root.TransformPoint(local);
        }

        Handles.color = color;
#if UNITY_2020_2_OR_NEWER
        Handles.DrawAAPolyLine(thickness, points);
#else
        Handles.DrawPolyLine(points);
#endif
    }

    static void DrawPolyline(
        Transform root,
        ActionBakedMotion baked,
        bool applyPlanarMode,
        Color color,
        float thickness)
    {
        var points = new Vector3[baked.frameCount + 1];
        long xMm = 0;
        long zMm = 0;
        points[0] = root.TransformPoint(Vector3.zero);

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
            points[i + 1] = root.TransformPoint(local);
        }

        Handles.color = color;
#if UNITY_2020_2_OR_NEWER
        Handles.DrawAAPolyLine(thickness, points);
#else
        Handles.DrawPolyLine(points);
#endif
    }
}
