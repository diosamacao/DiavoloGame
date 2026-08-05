using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene 视图相机扶正：清除侧滚（Roll），保持当前朝向的俯仰/偏航。
/// </summary>
public static class LevelSceneViewCameraMenu
{
    const string MenuPath = "ACTGame/Scene View/Level Camera (Clear Roll)";

    /// <summary>
    /// 将当前 Scene 视图相机扶正为无侧滚姿态。
    /// </summary>
    [MenuItem(MenuPath)]
    public static void LevelActiveSceneViewCamera()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            Debug.LogWarning("[LevelSceneView] 没有可用的 Scene 视图。");
            return;
        }

        Quaternion leveled = BuildLevelRotation(sceneView.rotation);
        // LookAtDirect 会写入 pivot/size/rotation，避免只改 rotation 时状态不同步
        sceneView.LookAtDirect(sceneView.pivot, leveled, sceneView.size);
        sceneView.Repaint();
        Debug.Log("[LevelSceneView] Scene 相机已扶正（Roll = 0）。");
    }

    /// <summary>
    /// 用世界 Up 重建朝向，去掉绕视线的侧滚。
    /// </summary>
    static Quaternion BuildLevelRotation(Quaternion current)
    {
        Vector3 forward = current * Vector3.forward;
        // 俯仰接近 ±90° 时 LookRotation(up) 不稳定，改用偏航锁定的欧拉重建
        if (Mathf.Abs(Vector3.Dot(forward.normalized, Vector3.up)) > 0.99f)
        {
            Vector3 euler = current.eulerAngles;
            return Quaternion.Euler(euler.x, euler.y, 0f);
        }

        return Quaternion.LookRotation(forward, Vector3.up);
    }
}
