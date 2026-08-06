using UnityEditor;
using UnityEngine;

/// <summary>
/// Wave 0：Play/Edit Mode Scene Gizmo — Motor 碰撞圆、SimulationRoot、PresentationRoot、CameraRoot/Orbit。
/// </summary>
public static class CharacterAnchorGizmoDrawer
{
    /// <summary>绘制玩家锚点与 Motor 圆。</summary>
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Active)]
    public static void DrawPlayerAnchors(PlayerController player, GizmoType gizmoType)
    {
        if (player == null || player.Actor == null)
            return;

        CharacterActor actor = player.Actor;
        CharacterMotorSim motor = actor.MotorSim;
        Transform simRoot = player.transform;
        Transform presentation = actor.PresentationRoot;

        DrawMotorDisk(simRoot, motor, new Color(0.2f, 1f, 0.35f, 0.85f));
        DrawAxisMarker(simRoot.position, Color.green, "SimulationRoot/Motor");

        if (presentation != null)
        {
            DrawAxisMarker(presentation.position, Color.cyan, "PresentationRoot");
            Transform visual = presentation.Find("CharacterVisualMotionRoot");
            if (visual != null)
                DrawAxisMarker(visual.position, new Color(1f, 0.3f, 0.85f), "VisualMotionRoot");
        }

        CameraManager camera = Object.FindObjectOfType<CameraManager>();
        if (camera == null)
            return;

        if (camera.CameraRootTransform != null)
            DrawAxisMarker(camera.CameraRootTransform.position, Color.yellow, "CameraRoot");
        if (camera.OrbitPivotTransform != null)
            DrawAxisMarker(camera.OrbitPivotTransform.position, new Color(1f, 0.4f, 0.9f), "OrbitPivot");
    }

    static void DrawMotorDisk(Transform root, CharacterMotorSim motor, Color color)
    {
        if (root == null || motor == null)
            return;

        Vector3 center = new(
            MotionQuantization.MmToMeters(motor.PositionMm.X),
            MotionQuantization.MmToMeters(motor.YMm),
            MotionQuantization.MmToMeters(motor.PositionMm.Z));
        float radius = MotionQuantization.MmToMeters(motor.RadiusMm);
        Handles.color = color;
        Handles.DrawWireDisc(center, Vector3.up, radius);
        Handles.DrawLine(center, center + root.forward * radius);
    }

    static void DrawAxisMarker(Vector3 position, Color color, string label)
    {
        Handles.color = color;
        Handles.DrawWireCube(position, Vector3.one * 0.08f);
        Handles.DrawLine(position, position + Vector3.up * 0.25f);
        Handles.Label(position + Vector3.up * 0.28f, label);
    }
}
