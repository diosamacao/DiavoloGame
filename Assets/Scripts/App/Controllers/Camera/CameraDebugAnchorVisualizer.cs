using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Play 模式实心球锚点：挂到 MainCamera 可见，不依赖 Scene Gizmos 开关。
/// 仅 Editor / Development Build 创建；由 CameraManager 驱动位置。
/// </summary>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
[DefaultExecutionOrder(1100)]
public sealed class CameraDebugAnchorVisualizer : AppControllerBase
{
    const string RootName = "CameraDebugAnchors";
    const float DefaultRadius = 0.07f;

    static readonly Color ColorSim = new(0.25f, 1f, 0.35f, 1f);
    static readonly Color ColorPresentation = Color.cyan;
    static readonly Color ColorVisual = new(1f, 0.35f, 0.9f, 1f);
    static readonly Color ColorCameraRoot = Color.yellow;
    static readonly Color ColorFollow = new(1f, 0.55f, 0.1f, 1f);
    static readonly Color ColorOrbit = new(1f, 0.4f, 0.75f, 1f);
    static readonly Color ColorPitch = new(0.55f, 0.75f, 1f, 1f);
    static readonly Color ColorMainCam = Color.white;
    static readonly Color ColorVcam = new(0.8f, 0.8f, 0.8f, 1f);

    readonly Dictionary<string, Marker> _markers = new(16);
    Transform _root;
    float _radius = DefaultRadius;
    bool _showLabels = true;
    CameraManager _camera;
    GUIStyle _labelStyle;

    struct Marker
    {
        public Transform Transform;
        public MeshRenderer Renderer;
        public Color Color;
    }

    /// <summary>绑定相机并确保调试根节点存在。</summary>
    public void Bind(CameraManager camera)
    {
        _camera = camera;
        EnsureRoot();
    }

    /// <summary>球半径（米）；由 CameraManager 同步。</summary>
    public void SetRadius(float radiusMeters) =>
        _radius = Mathf.Clamp(radiusMeters, 0.02f, 0.5f);

    /// <summary>是否在 Game 视图叠锚点名。</summary>
    public void SetShowLabels(bool show) => _showLabels = show;

    /// <summary>按当前相机状态刷新全部实心球位置与显隐。</summary>
    public void Sync()
    {
        if (_camera == null || !_camera.DrawCameraDebugGizmos)
        {
            SetRootActive(false);
            return;
        }

        EnsureRoot();
        SetRootActive(true);

        PlayerController player = _camera.GetComponent<PlayerController>();
        if (player == null)
            player = FindObjectOfType<PlayerController>();

        Transform presentation = _camera.PresentationFollowTarget;
        Transform visual = presentation != null
            ? presentation.Find("CharacterVisualMotionRoot")
            : null;
        Transform sim = player != null ? player.transform : null;
        Transform cameraRoot = _camera.CameraRootTransform;
        Transform orbit = _camera.OrbitPivotTransform;
        Transform pitch = _camera.PitchPivotTransform;
        Camera mainCam = Camera.main;
        var vcam = _camera.VirtualCamera;

        Place("SimRoot", sim != null ? sim.position : null, ColorSim);
        Place("PresentationRoot", presentation != null ? presentation.position : null, ColorPresentation);
        Place("VisualMotionRoot", visual != null ? visual.position : null, ColorVisual);
        Place("CameraRoot", cameraRoot != null ? cameraRoot.position : null, ColorCameraRoot);
        Place("FollowAnchor", _camera.FollowAnchorPosition, ColorFollow);
        Place("OrbitPivot", orbit != null ? orbit.position : null, ColorOrbit);
        Place("PitchPivot", pitch != null ? pitch.position : null, ColorPitch);
        Place("MainCamera", mainCam != null ? mainCam.transform.position : null, ColorMainCam);
        Place("VCam", vcam != null ? vcam.transform.position : null, ColorVcam);

        // 连接线：始终画在 Game/Scene（需 Game 视图开启 Gizmos 才见 Debug.DrawLine）
        DrawLink(sim, presentation);
        DrawLink(presentation, cameraRoot);
        if (cameraRoot != null)
            Debug.DrawLine(cameraRoot.position, _camera.FollowAnchorPosition, ColorFollow);
        if (orbit != null)
            Debug.DrawLine(_camera.FollowAnchorPosition, orbit.position, ColorOrbit);
        if (orbit != null && mainCam != null)
            Debug.DrawLine(orbit.position, mainCam.transform.position, new Color(1f, 1f, 1f, 0.5f));
    }

    void LateUpdate()
    {
        // CameraManager.LateUpdate 之后再刷，保证 FollowAnchor 已是本帧终值
        Sync();
    }

    void OnGUI()
    {
        if (!_showLabels || _camera == null || !_camera.DrawCameraDebugGizmos || _root == null || !_root.gameObject.activeSelf)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        EnsureLabelStyle();
        foreach (KeyValuePair<string, Marker> pair in _markers)
        {
            Marker marker = pair.Value;
            if (marker.Transform == null || !marker.Transform.gameObject.activeSelf)
                continue;

            Vector3 world = marker.Transform.position;
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0.05f)
                continue;

            // GUI 原点在左上，Screen 在左下
            var rect = new Rect(screen.x + 8f, Screen.height - screen.y - 10f, 160f, 18f);
            GUI.Label(rect, pair.Key, _labelStyle);
        }
    }

    void OnDestroy()
    {
        if (_root != null)
            Destroy(_root.gameObject);
        _markers.Clear();
    }

    void EnsureRoot()
    {
        if (_root != null)
            return;

        var go = new GameObject(RootName);
        // 挂在场景根，避免随 CameraManager 旋转；位置每帧写入世界坐标
        go.transform.SetParent(null, false);
        _root = go.transform;
    }

    void SetRootActive(bool active)
    {
        if (_root != null && _root.gameObject.activeSelf != active)
            _root.gameObject.SetActive(active);
    }

    void Place(string id, Vector3? position, Color color)
    {
        if (!position.HasValue)
        {
            if (_markers.TryGetValue(id, out Marker existing) && existing.Transform != null)
                existing.Transform.gameObject.SetActive(false);
            return;
        }

        Marker marker = GetOrCreate(id, color);
        marker.Transform.gameObject.SetActive(true);
        marker.Transform.position = position.Value;
        float diameter = _radius * 2f;
        marker.Transform.localScale = new Vector3(diameter, diameter, diameter);
        if (marker.Renderer != null && marker.Renderer.sharedMaterial != null)
            marker.Renderer.sharedMaterial.color = color;
    }

    Marker GetOrCreate(string id, Color color)
    {
        if (_markers.TryGetValue(id, out Marker marker) && marker.Transform != null)
            return marker;

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = id;
        sphere.transform.SetParent(_root, false);

        // 调试球不参与物理
        Collider col = sphere.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        var renderer = sphere.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = CreateSolidMaterial(color);

        marker = new Marker
        {
            Transform = sphere.transform,
            Renderer = renderer,
            Color = color,
        };
        _markers[id] = marker;
        return marker;
    }

    static Material CreateSolidMaterial(Color color)
    {
        // Built-in Unlit；URP 工程若失败再回退 Sprites/Default
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Standard");

        var mat = new Material(shader)
        {
            color = color,
            hideFlags = HideFlags.HideAndDontSave,
        };
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        return mat;
    }

    static void DrawLink(Transform a, Transform b)
    {
        if (a == null || b == null)
            return;
        Debug.DrawLine(a.position, b.position, new Color(1f, 1f, 1f, 0.35f));
    }

    void EnsureLabelStyle()
    {
        if (_labelStyle != null)
            return;

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };
    }
}
#else
/// <summary>非开发构建不创建运行时锚点球。</summary>
public sealed class CameraDebugAnchorVisualizer : MonoBehaviour
{
}
#endif
