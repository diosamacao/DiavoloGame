using UnityEditor;
using UnityEngine;

/// <summary>Scene 视图 Handles 绘制 Hitbox / Hurtbox 线框。</summary>
public static class HitboxSceneDrawing
{
    /// <summary>用 Handles 绘制定向 Box 线框。</summary>
    public static void DrawWireOrientedBox(HitboxOrientedBox box, Color color)
    {
        Color previous = Handles.color;
        Matrix4x4 previousMatrix = Handles.matrix;

        Handles.color = color;
        Handles.matrix = Matrix4x4.TRS(box.Center, box.Rotation, box.HalfExtents * 2f);
        Handles.DrawWireCube(Vector3.zero, Vector3.one);

        Handles.matrix = previousMatrix;
        Handles.color = previous;
    }
}
