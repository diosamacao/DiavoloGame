using UnityEngine;

/// <summary>Hitbox / Hurtbox 线框绘制工具，Gizmo 与 Scene 视图共用同一套 OBB 数据。</summary>
public static class HitboxGizmoDrawing
{
    /// <summary>用 Gizmos 绘制定向 Box 线框。</summary>
    public static void DrawWireOrientedBox(HitboxOrientedBox box, Color color)
    {
        Matrix4x4 previous = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = Matrix4x4.TRS(box.Center, box.Rotation, box.HalfExtents * 2f);
        Gizmos.color = color;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

        Gizmos.matrix = previous;
        Gizmos.color = previousColor;
    }
}
