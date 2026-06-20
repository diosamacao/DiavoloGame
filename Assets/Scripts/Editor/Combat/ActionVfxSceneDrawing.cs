using UnityEditor;
using UnityEngine;

/// <summary>Scene 视图绘制 VFX 帧事件预览（轴向标记 + 可选 Prefab 实例）。</summary>
public static class ActionVfxSceneDrawing
{
    /// <summary>在挂点处绘制 VFX 位置/朝向预览标记。</summary>
    public static void DrawVfxMarker(Transform anchor, ActionVfxKeyframe vfx, Color color)
    {
        if (anchor == null || vfx == null)
            return;

        Vector3 worldCenter;
        Quaternion worldRotation;
        ResolveWorldPose(anchor, vfx, out worldCenter, out worldRotation);

        Color previous = Handles.color;
        Handles.color = color;
        Handles.DrawWireDisc(worldCenter, worldRotation * Vector3.up, HandleUtility.GetHandleSize(worldCenter) * 0.12f);
        Handles.DrawLine(worldCenter, worldCenter + worldRotation * Vector3.forward * HandleUtility.GetHandleSize(worldCenter) * 0.35f);
        Handles.color = previous;
    }

    /// <summary>由挂点与 VFX 关键帧计算世界空间位置与旋转。</summary>
    public static void ResolveWorldPose(
        Transform anchor,
        ActionVfxKeyframe vfx,
        out Vector3 worldCenter,
        out Quaternion worldRotation)
    {
        Quaternion localRotation = Quaternion.Euler(vfx.LocalEulerAngles);
        worldCenter = anchor.TransformPoint(vfx.LocalOffset);
        worldRotation = anchor.rotation * localRotation;
    }
}
