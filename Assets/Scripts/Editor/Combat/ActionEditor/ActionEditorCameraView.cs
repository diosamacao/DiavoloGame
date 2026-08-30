using UnityEditor;
using UnityEngine;

/// <summary>独立渲染当前 Action Camera Pose 的可停靠视图，不接管 SceneView 导航。</summary>
public sealed class ActionEditorCameraView : EditorWindow
{
    const int MinTextureSize = 64;
    const int MaxTextureSize = 2048;

    static ActionEditorCameraView _instance;

    CameraShotPose _pose;
    string _shotId;
    int _previewFrame;
    bool _hasPose;
    Camera _previewCamera;
    RenderTexture _renderTexture;

    /// <summary>打开唯一 Camera View；Action Editor 下一次 Scene 刷新会推送当前帧。</summary>
    [MenuItem("ACT/Action Camera View")]
    public static void Open()
    {
        ActionEditorCameraView window = GetWindow<ActionEditorCameraView>();
        window.titleContent = new GUIContent("Camera View");
        window.minSize = new Vector2(320f, 220f);
        window.Show();
        SceneView.RepaintAll();
    }

    /// <summary>若专用窗口已打开，则推送当前 Camera Window 的求值结果。</summary>
    public static void Publish(string shotId, int previewFrame, in CameraShotPose pose)
    {
        if (_instance == null)
            return;

        _instance.SetPose(shotId, previewFrame, pose);
    }

    /// <summary>当前帧不在有效 Camera Window 时清空旧画面。</summary>
    public static void ClearPose()
    {
        if (_instance == null || !_instance._hasPose)
            return;

        _instance._hasPose = false;
        _instance.Repaint();
    }

    void OnEnable()
    {
        _instance = this;
        titleContent = new GUIContent("Camera View");
    }

    void OnDisable()
    {
        if (_instance == this)
            _instance = null;
        ReleaseRenderResources();
    }

    void OnGUI()
    {
        DrawToolbar();
        Rect contentRect = GUILayoutUtility.GetRect(
            MinTextureSize,
            MaxTextureSize,
            MinTextureSize,
            MaxTextureSize,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true));

        if (!_hasPose)
        {
            EditorGUI.HelpBox(
                contentRect,
                "在 Action Editor 中选择 Camera Window，并把播放头移动到窗口范围内。",
                MessageType.Info);
            return;
        }

        float aspect = ResolveOutputAspect();
        Rect previewRect = FitAspect(contentRect, aspect);
        if (Event.current.type == EventType.Repaint)
        {
            EnsureRenderTexture(previewRect);
            RenderCurrentPose();
        }

        EditorGUI.DrawRect(contentRect, Color.black);
        if (_renderTexture != null)
            EditorGUI.DrawPreviewTexture(previewRect, _renderTexture, null, ScaleMode.StretchToFill);
    }

    /// <summary>缓存最新 Pose；只有值变化时请求窗口重绘。</summary>
    void SetPose(string shotId, int previewFrame, in CameraShotPose pose)
    {
        bool changed = !_hasPose
            || _previewFrame != previewFrame
            || _shotId != shotId
            || Vector3.SqrMagnitude(_pose.WorldPosition - pose.WorldPosition) > 0.00000001f
            || Vector3.SqrMagnitude(_pose.WorldLookAt - pose.WorldLookAt) > 0.00000001f
            || !Mathf.Approximately(_pose.FieldOfView, pose.FieldOfView);

        _pose = pose;
        _shotId = shotId;
        _previewFrame = previewFrame;
        _hasPose = true;
        if (changed)
            Repaint();
    }

    /// <summary>显示当前 Shot、逻辑帧和 FOV，保持预览窗口只读。</summary>
    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label(
            _hasPose
                ? $"{_shotId}  |  Frame {_previewFrame}  |  FOV {_pose.FieldOfView:0.#}°"
                : "等待 Camera Window",
            EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("刷新", EditorStyles.toolbarButton))
            Repaint();
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>复用 Main Camera 的渲染设置，仅覆盖 Shot 的位置、朝向、FOV 与目标纹理。</summary>
    void RenderCurrentPose()
    {
        EnsurePreviewCamera();
        if (_previewCamera == null || _renderTexture == null)
            return;

        Camera source = Camera.main;
        if (source != null && source != _previewCamera)
            _previewCamera.CopyFrom(source);
        else
        {
            _previewCamera.clearFlags = CameraClearFlags.Skybox;
            _previewCamera.cullingMask = ~0;
            _previewCamera.nearClipPlane = 0.03f;
            _previewCamera.farClipPlane = 1000f;
        }

        Vector3 direction = _pose.WorldLookAt - _pose.WorldPosition;
        Quaternion rotation = direction.sqrMagnitude > 0.000001f
            ? Quaternion.LookRotation(direction, Vector3.up)
            : Quaternion.identity;
        _previewCamera.transform.SetPositionAndRotation(_pose.WorldPosition, rotation);
        _previewCamera.fieldOfView = Mathf.Clamp(_pose.FieldOfView, 1f, 179f);
        _previewCamera.enabled = false;
        _previewCamera.targetTexture = _renderTexture;
        _previewCamera.Render();
        _previewCamera.targetTexture = null;
    }

    /// <summary>按当前输出相机画幅显示；没有 Main Camera 时使用 16:9。</summary>
    static float ResolveOutputAspect()
    {
        Camera source = Camera.main;
        return source != null && source.aspect > 0.01f ? source.aspect : 16f / 9f;
    }

    /// <summary>在可用区域内生成保持画幅的最大矩形。</summary>
    static Rect FitAspect(Rect area, float aspect)
    {
        float width = area.width;
        float height = width / aspect;
        if (height > area.height)
        {
            height = area.height;
            width = height * aspect;
        }

        return new Rect(
            area.x + (area.width - width) * 0.5f,
            area.y + (area.height - height) * 0.5f,
            width,
            height);
    }

    /// <summary>窗口尺寸变化时重建受上限约束的 RenderTexture，避免每帧分配。</summary>
    void EnsureRenderTexture(Rect previewRect)
    {
        float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
        int width = Mathf.Clamp(
            Mathf.RoundToInt(previewRect.width * pixelsPerPoint),
            MinTextureSize,
            MaxTextureSize);
        int height = Mathf.Clamp(
            Mathf.RoundToInt(previewRect.height * pixelsPerPoint),
            MinTextureSize,
            MaxTextureSize);
        if (_renderTexture != null
            && _renderTexture.width == width
            && _renderTexture.height == height)
        {
            return;
        }

        ReleaseRenderTexture();
        _renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "ActionEditorCameraView",
            hideFlags = HideFlags.HideAndDontSave,
            antiAliasing = 1,
        };
        _renderTexture.Create();
    }

    /// <summary>按需创建不参与正常 Camera 更新的隐藏预览相机。</summary>
    void EnsurePreviewCamera()
    {
        if (_previewCamera != null)
            return;

        var cameraObject = new GameObject("ActionEditorCameraView")
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        _previewCamera = cameraObject.AddComponent<Camera>();
        _previewCamera.enabled = false;
    }

    /// <summary>释放隐藏 Camera 与 RenderTexture，避免关闭窗口后残留 Editor 对象。</summary>
    void ReleaseRenderResources()
    {
        ReleaseRenderTexture();
        if (_previewCamera != null)
            DestroyImmediate(_previewCamera.gameObject);
        _previewCamera = null;
    }

    /// <summary>释放仅由本窗口持有的渲染纹理。</summary>
    void ReleaseRenderTexture()
    {
        if (_renderTexture == null)
            return;
        _renderTexture.Release();
        DestroyImmediate(_renderTexture);
        _renderTexture = null;
    }
}
