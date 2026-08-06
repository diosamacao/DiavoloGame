using Cinemachine;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene 附加可视化：箭头、滤左右残差、图例。
/// Play 时实心球由 <see cref="CameraDebugAnchorVisualizer"/> 画进 Game 视图；Edit 模式在此补实心球。
/// </summary>
public static class CameraDebugGizmoDrawer
{
    const float AxisLength = 0.55f;

    /// <summary>挂在 CameraManager 上绘制完整相机锚点链（选中与否都显示）。</summary>
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Active)]
    public static void DrawCameraRig(CameraManager camera, GizmoType gizmoType)
    {
        if (camera == null || !camera.DrawCameraDebugGizmos)
            return;

        Transform presentation = camera.PresentationFollowTarget;
        Transform cameraRoot = camera.CameraRootTransform;
        Transform orbit = camera.OrbitPivotTransform;
        Transform pitch = camera.PitchPivotTransform;
        Vector3 followAnchor = camera.FollowAnchorPosition;
        float radius = camera.DebugAnchorRadius;

        PlayerController player = Object.FindObjectOfType<PlayerController>();
        Transform simulationRoot = player != null ? player.transform : null;
        Transform visualRoot = presentation != null
            ? presentation.Find("CharacterVisualMotionRoot")
            : null;

        Camera brainCamera = Camera.main;

        // Play 时实心球已由运行时 Mesh 绘制；Edit 模式在此画实心球
        if (!Application.isPlaying)
        {
            if (simulationRoot != null)
                DrawSolidSphere(simulationRoot.position, new Color(0.25f, 1f, 0.35f), "SimRoot", radius);
            if (presentation != null)
                DrawSolidSphere(presentation.position, Color.cyan, "PresentationRoot", radius);
            if (visualRoot != null)
                DrawSolidSphere(visualRoot.position, new Color(1f, 0.35f, 0.9f), "VisualMotionRoot", radius);
            if (cameraRoot != null)
                DrawSolidSphere(cameraRoot.position, Color.yellow, "CameraRoot", radius);
            DrawSolidSphere(followAnchor, new Color(1f, 0.55f, 0.1f), "FollowAnchor", radius);
            if (orbit != null)
                DrawSolidSphere(orbit.position, new Color(1f, 0.4f, 0.75f), "OrbitPivot", radius);
            if (pitch != null)
                DrawSolidSphere(pitch.position, new Color(0.6f, 0.75f, 1f), "PitchPivot", radius);
            if (brainCamera != null)
                DrawSolidSphere(brainCamera.transform.position, Color.white, "MainCamera", radius);
            CinemachineVirtualCamera vcam = camera.VirtualCamera;
            if (vcam != null)
                DrawSolidSphere(vcam.transform.position, new Color(0.85f, 0.85f, 0.85f), "VCam", radius);
        }

        // —— 连接链 ——
        Handles.color = new Color(1f, 1f, 1f, 0.35f);
        if (presentation != null && cameraRoot != null)
            Handles.DrawDottedLine(presentation.position, cameraRoot.position, 2f);
        if (cameraRoot != null)
            Handles.DrawLine(cameraRoot.position, followAnchor);
        if (orbit != null)
            Handles.DrawDottedLine(followAnchor, orbit.position, 2f);
        if (orbit != null && brainCamera != null)
            Handles.DrawDottedLine(orbit.position, brainCamera.transform.position, 3f);

        // —— 朝向：角色滤左右前向 vs 镜头 PlanarForward ——
        if (cameraRoot != null)
        {
            Vector3 followFwd = camera.GetFollowForwardAxis();
            Vector3 camFwd = camera.PlanarForward;
            Vector3 camRight = camera.PlanarRight;
            Vector3 origin = cameraRoot.position;

            Handles.color = new Color(0.2f, 0.9f, 1f, 0.95f);
            Handles.ArrowHandleCap(
                0,
                origin,
                Quaternion.LookRotation(followFwd),
                AxisLength,
                EventType.Repaint);
            Handles.Label(origin + followFwd * (AxisLength + 0.05f), "FollowFwd (滤左右轴)");

            Handles.color = new Color(0.3f, 0.55f, 1f, 0.95f);
            Handles.ArrowHandleCap(
                0,
                origin + Vector3.up * 0.05f,
                Quaternion.LookRotation(camFwd),
                AxisLength,
                EventType.Repaint);
            Handles.Label(origin + camFwd * (AxisLength + 0.05f) + Vector3.up * 0.05f, "Cam PlanarFwd");

            Handles.color = new Color(1f, 0.35f, 0.35f, 0.8f);
            Handles.ArrowHandleCap(
                0,
                origin + Vector3.up * 0.1f,
                Quaternion.LookRotation(camRight),
                AxisLength * 0.7f,
                EventType.Repaint);
        }

        // —— 滤左右残差：CameraRoot 相对 FollowAnchor 的侧向分量 ——
        if (cameraRoot != null)
            DrawLateralResidual(cameraRoot.position, followAnchor, camera.GetFollowForwardAxis());

        DrawLegend(camera, cameraRoot, followAnchor, orbit);
    }

    /// <summary>画出被滤掉的左右位移（红虚线），便于调 lateralFollowFactor。</summary>
    static void DrawLateralResidual(Vector3 cameraRootPos, Vector3 followAnchor, Vector3 followForward)
    {
        Vector3 delta = cameraRootPos - followAnchor;
        Vector3 forwardPart = Vector3.Dot(delta, followForward) * followForward;
        Vector3 verticalPart = new Vector3(0f, delta.y, 0f);
        Vector3 lateralPart = delta - forwardPart - verticalPart;
        if (lateralPart.sqrMagnitude < 0.0001f)
            return;

        Handles.color = new Color(1f, 0.15f, 0.15f, 0.9f);
        Handles.DrawDottedLine(followAnchor, followAnchor + lateralPart, 4f);
        Handles.Label(
            followAnchor + lateralPart * 0.5f + Vector3.up * 0.12f,
            $"Lateral residual {lateralPart.magnitude:0.00}m");
    }

    /// <summary>Edit 模式实心球 + 标签（Handles.SphereHandleCap）。</summary>
    static void DrawSolidSphere(Vector3 position, Color color, string label, float radius)
    {
        Handles.color = color;
        Handles.SphereHandleCap(0, position, Quaternion.identity, radius * 2f, EventType.Repaint);
        Handles.Label(position + Vector3.up * (radius + 0.12f), label);
    }

    /// <summary>屏幕角图例 + 数值摘要，避免只看色点猜含义。</summary>
    static void DrawLegend(
        CameraManager camera,
        Transform cameraRoot,
        Vector3 followAnchor,
        Transform orbit)
    {
        Handles.BeginGUI();
        const float width = 320f;
        const float height = 168f;
        var rect = new Rect(12f, 12f, width, height);
        GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

        var style = new GUIStyle(EditorStyles.miniLabel)
        {
            richText = true,
            wordWrap = false,
        };

        float x = rect.x + 8f;
        float y = rect.y + 6f;
        GUI.Label(new Rect(x, y, width - 16f, 18f), "<b>Camera Debug</b>", style);
        y += 18f;
        GUI.Label(
            new Rect(x, y, width - 16f, 16f),
            $"Yaw {camera.YawDegrees:0.0}°  Pitch {camera.PitchDegrees:0.0}°  Lateral {camera.LateralFollowFactor:0.00}",
            style);
        y += 16f;
        GUI.Label(new Rect(x, y, width - 16f, 16f), "绿 Sim · 青 Presentation · 粉 Visual", style);
        y += 14f;
        GUI.Label(new Rect(x, y, width - 16f, 16f), "黄 CameraRoot · 橙 FollowAnchor · 品红 Orbit", style);
        y += 14f;
        GUI.Label(new Rect(x, y, width - 16f, 16f), "蓝 Pitch · 白 MainCamera · 红=滤掉的左右", style);
        y += 16f;

        if (cameraRoot != null)
        {
            float rootToFollow = Vector3.Distance(cameraRoot.position, followAnchor);
            GUI.Label(
                new Rect(x, y, width - 16f, 16f),
                $"CameraRoot→FollowAnchor {rootToFollow:0.000}m",
                style);
            y += 14f;
        }

        if (orbit != null && Camera.main != null)
        {
            float orbitToCam = Vector3.Distance(orbit.position, Camera.main.transform.position);
            GUI.Label(
                new Rect(x, y, width - 16f, 16f),
                $"Orbit→MainCamera {orbitToCam:0.00}m",
                style);
        }

        Handles.EndGUI();
    }
}
