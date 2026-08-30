using UnityEditor;
using UnityEngine;

/// <summary>
/// Wave 0：角色逻辑/表现锚点 Gizmo（Motor 圆、Sim/Presentation/Visual），不绘制相机定位。
/// </summary>
public static class CharacterAnchorGizmoDrawer
{
    /// <summary>绘制玩家 Motor 与表现层级锚点（不含相机 Rig）。</summary>
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

        if (presentation == null)
            return;

        DrawAxisMarker(presentation.position, Color.cyan, "PresentationRoot");
        Transform visual = presentation.Find("CharacterVisualMotionRoot");
        if (visual != null)
            DrawAxisMarker(visual.position, new Color(1f, 0.3f, 0.85f), "VisualMotionRoot");
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
        // Play 时只保留文字，避免运行状态下实体球遮挡角色观察。
        if (!Application.isPlaying)
        {
            Handles.color = color;
            Handles.SphereHandleCap(0, position, Quaternion.identity, 0.14f, EventType.Repaint);
        }

        Handles.color = color;
        Handles.Label(position + Vector3.up * 0.16f, label);
    }
}
