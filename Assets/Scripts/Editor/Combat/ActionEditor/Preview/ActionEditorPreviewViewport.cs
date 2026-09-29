using System;
using UnityEditor;
using UnityEngine;

/// <summary>内嵌隔离视口，独占临时模型和渲染资源；动画采样仍由主窗口会话负责。</summary>
public sealed class ActionEditorPreviewViewport : IDisposable
{
    PreviewRenderUtility render;
    GameObject instance;
    GameObject source;
    Vector3 target = Vector3.up;
    float yaw = 155, pitch = 10, distance = 4;
    bool grid = true;
    Mesh gridMesh;
    Material gridMaterial;
    int cameraControl;

    /// <summary>只实例化 Prefab，绝不将源资产或普通场景对象移入 PreviewScene。</summary>
    public Transform Bind(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab != null && !EditorUtility.IsPersistent(prefab)) throw new ArgumentException("隔离预览只接受 Project 中的模型资产。");
        if (source == prefab && instance != null)
        {
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance.transform;
        }
        Dispose();
        if (prefab == null) return null;
        render = new PreviewRenderUtility();
        render.cameraFieldOfView = 30;
        instance = UnityEngine.Object.Instantiate(prefab);
        instance.hideFlags = HideFlags.HideAndDontSave;
        instance.transform.SetPositionAndRotation(position, rotation);
        render.AddSingleGO(instance);
        source = prefab;
        Focus();
        return instance.transform;
    }

    /// <summary>释放所有隔离资源；调用者应先结束绑定该模型的采样会话。</summary>
    public void Dispose()
    {
        render?.Cleanup(); render = null;
        if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
        if (gridMesh != null) UnityEngine.Object.DestroyImmediate(gridMesh);
        if (gridMaterial != null) UnityEngine.Object.DestroyImmediate(gridMaterial);
        instance = null; source = null; gridMesh = null; gridMaterial = null;
    }

    /// <summary>渲染当前姿态及三维覆盖层（回调内不可绘制 GUI）；场景模式指向 Scene。</summary>
    public void Draw(Rect rect, bool sceneMode, Transform sceneTarget, Action<Camera> overlay)
    {
        if (rect.width < 1 || rect.height < 1) return;
        if (sceneMode)
        {
            GUILayout.BeginArea(rect);
            GUILayout.FlexibleSpace();
            EditorGUILayout.HelpBox(sceneTarget != null
                ? "场景预览 · " + sceneTarget.name + "\n动画与命中框显示在 Scene 视图中。"
                : "场景预览\n将 Hierarchy 中的模型拖入顶部目标框。", MessageType.Info);
            if (sceneTarget != null && GUILayout.Button("定位 Scene 中的模型"))
            {
                Selection.activeTransform = sceneTarget;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
            return;
        }
        if (render == null || instance == null)
        {
            GUI.Label(rect, "隔离预览：请在顶部选择模型 Prefab。", EditorStyles.centeredGreyMiniLabel);
            return;
        }
        Rect controls = new(rect.x + 8, rect.y + 6, rect.width - 16, 22);
        grid = GUI.Toggle(new Rect(controls.x, controls.y, 55, 22), grid, "网格", EditorStyles.miniButton);
        if (GUI.Button(new Rect(controls.x + 60, controls.y, 65, 22), "复位 / F", EditorStyles.miniButton)) Focus();
        Rect view = new(rect.x, rect.y + 32, rect.width, Mathf.Max(1, rect.height - 52));
        HandleCamera(view);
        GUI.Label(new Rect(rect.x + 8, rect.yMax - 19, rect.width - 16, 19), "右键旋转 · 中键平移 · 滚轮缩放 · F 聚焦", EditorStyles.centeredGreyMiniLabel);
        if (Event.current.type != EventType.Repaint) return;
        Camera previousCamera = Camera.current;
        var previousZTest = Handles.zTest;
        render.BeginPreview(view, GUIStyle.none);
        Texture texture;
        try
        {
            var camera = render.camera;
            camera.backgroundColor = new Color(.105f, .12f, .15f);
            camera.clearFlags = CameraClearFlags.Color;
            camera.transform.position = target + Quaternion.Euler(pitch, yaw, 0) * Vector3.back * distance;
            camera.transform.LookAt(target);
            camera.nearClipPlane = .01f; camera.farClipPlane = 500;
            ConfigureLighting(camera.transform.rotation);
            if (grid) DrawGrid();
            // 保持模型与覆盖层使用同一投影，窄视口也不能只改变模型的 FOV。
            render.Render(updatefov: false);
            if (overlay != null)
            {
                // DrawingScope 是 struct；无参构造不会保存状态，Dispose 会把全局矩阵清零。
                using (new Handles.DrawingScope(Color.white, Matrix4x4.identity))
                {
                    RenderTexture.active = camera.targetTexture;
                    Handles.SetCamera(camera);
                    Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                    overlay(camera);
                }
            }
        }
        finally
        {
            Handles.zTest = previousZTest;
            // 先恢复相机，再由 EndPreview 恢复 GUI 的 RT、viewport 和 GL 矩阵。
            try
            {
                if (previousCamera != null) Handles.SetCamera(previousCamera);
                Camera.SetupCurrent(previousCamera);
            }
            finally { texture = render.EndPreview(); }
        }
        GUI.DrawTexture(view, texture, ScaleMode.StretchToFill, false);
    }

    // 主光与柔和补光跟随观察方向；环境光避免模型背光面全黑。
    void ConfigureLighting(Quaternion cameraRotation)
    {
        render.ambientColor = new Color(.35f, .37f, .4f);
        var lights = render.lights;
        lights[0].color = new Color(1f, .96f, .9f);
        lights[0].intensity = 1.2f;
        lights[0].transform.rotation = cameraRotation * Quaternion.Euler(25, -30, 0);
        lights[1].color = new Color(.8f, .88f, 1f);
        lights[1].intensity = .8f;
        lights[1].transform.rotation = cameraRotation * Quaternion.Euler(-10, 40, 0);
    }

    void Focus()
    {
        var bounds = new Bounds(instance != null ? instance.transform.position + Vector3.up : Vector3.up, Vector3.one);
        if (instance != null)
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
        target = bounds.center;
        distance = Mathf.Max(2, bounds.extents.magnitude * 3);
        yaw = 155; pitch = 10;
    }

    void HandleCamera(Rect rect)
    {
        Event e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition) && (e.button == 1 || e.button == 2))
        { cameraControl = id; GUIUtility.hotControl = id; e.Use(); }
        if (e.type == EventType.MouseUp && cameraControl != 0)
        { if (GUIUtility.hotControl == cameraControl) GUIUtility.hotControl = 0; cameraControl = 0; e.Use(); }
        if (!rect.Contains(e.mousePosition) && cameraControl == 0) return;
        if (e.type == EventType.MouseDrag && cameraControl != 0 && e.button == 1)
        { yaw += e.delta.x * .5f; pitch = Mathf.Clamp(pitch + e.delta.y * .5f, -85, 85); e.Use(); }
        else if (e.type == EventType.MouseDrag && cameraControl != 0 && e.button == 2)
        { target += Quaternion.Euler(pitch, yaw, 0) * new Vector3(-e.delta.x, e.delta.y, 0) * distance * .002f; e.Use(); }
        else if (e.type == EventType.ScrollWheel)
        { distance = Mathf.Clamp(distance * Mathf.Exp(e.delta.y * .08f), .2f, 100); e.Use(); }
        else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F && !EditorGUIUtility.editingTextField)
        { Focus(); e.Use(); }
    }

    void DrawGrid()
    {
        if (gridMesh == null)
        {
            var vertices = new Vector3[84];
            var indices = new int[84];
            for (int i = 0; i <= 20; i++)
            {
                int n = i * 4; float p = i - 10;
                vertices[n] = new Vector3(p, 0, -10); vertices[n + 1] = new Vector3(p, 0, 10);
                vertices[n + 2] = new Vector3(-10, 0, p); vertices[n + 3] = new Vector3(10, 0, p);
            }
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            gridMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, vertices = vertices };
            gridMesh.SetIndices(indices, MeshTopology.Lines, 0);
            gridMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
            gridMaterial.SetColor("_Color", new Color(.25f, .3f, .35f));
            gridMaterial.SetInt("_ZWrite", 0);
        }
        render.DrawMesh(gridMesh, Matrix4x4.identity, gridMaterial, 0);
    }
}

