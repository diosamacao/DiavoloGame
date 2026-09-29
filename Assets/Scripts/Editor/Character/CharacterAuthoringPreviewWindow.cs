using UnityEditor;
using UnityEngine;

/// <summary>角色上下文的隔离模型预览；时间轴采样此临时实例，不改写场景对象。</summary>
public sealed class CharacterAuthoringPreviewWindow : EditorWindow
{
    PreviewRenderUtility preview;
    GameObject model;
    CharacterConfig config;
    float yaw = 155f;
    float distance = 4f;

    /// <summary>创建或复用当前角色的临时模型，供时间轴采样与挂点选择。</summary>
    public static Transform Bind(CharacterConfig value)
    {
        if (value == null || value.ModelPrefab == null) return null;
        var window = GetWindow<CharacterAuthoringPreviewWindow>("Character Preview");
        if (window.config != value || window.model == null)
        {
            window.Cleanup();
            window.config = value;
            window.preview = new PreviewRenderUtility();
            window.preview.cameraFieldOfView = 30f;
            window.model = Instantiate(value.ModelPrefab);
            window.model.hideFlags = HideFlags.HideAndDontSave;
            window.model.transform.localPosition = value.ModelLocalPosition;
            window.model.transform.localRotation = value.ModelLocalRotation;
            window.preview.AddSingleGO(window.model);
        }
        window.Show();
        return window.model.transform;
    }

    void OnEnable() => EditorApplication.update += Repaint;
    void OnDisable() { EditorApplication.update -= Repaint; Cleanup(); }
    void Cleanup()
    {
        preview?.Cleanup(); preview = null;
        if (model != null) DestroyImmediate(model);
        model = null; config = null;
    }
    void OnGUI()
    {
        if (preview == null || model == null) { EditorGUILayout.HelpBox("从角色工作台打开动作以创建预览。", MessageType.Info); return; }
        yaw = EditorGUILayout.Slider("视角", yaw, -180, 180);
        distance = EditorGUILayout.Slider("距离", distance, 0.5f, 12);
        Rect rect = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        if (Event.current.type != EventType.Repaint) return;
        preview.BeginPreview(rect, GUIStyle.none);
        Vector3 target = model.transform.position + Vector3.up * 0.9f;
        preview.camera.transform.position = target + Quaternion.Euler(8, yaw, 0) * Vector3.back * distance;
        preview.camera.transform.LookAt(target);
        preview.camera.nearClipPlane = 0.01f; preview.camera.farClipPlane = 100f;
        preview.lights[0].intensity = 1.2f;
        preview.lights[0].transform.rotation = Quaternion.Euler(40, 30, 0);
        preview.lights[1].intensity = 0.8f;
        preview.Render();
        GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
    }
}
