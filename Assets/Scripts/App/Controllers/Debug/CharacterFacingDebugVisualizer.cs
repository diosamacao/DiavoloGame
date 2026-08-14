using UnityEngine;

/// <summary>
/// Play 模式脚底实心箭头：输入 wish（黄）与模型朝向（品红）。
/// 圆柱+圆锥组合；不依赖 Scene Gizmos 开关；仅 Editor / Development Build。
/// </summary>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
[DefaultExecutionOrder(1200)]
public sealed class CharacterFacingDebugVisualizer : AppControllerBase
{
    const string RootName = "CharacterFacingDebugArrows";
    const float FeetLiftMeters = 0.05f;

    static readonly Color ColorWish = new(1f, 0.92f, 0.05f, 1f);
    static readonly Color ColorModel = new(1f, 0.15f, 0.95f, 1f);

    [Header("Display")]
    [SerializeField] bool drawArrows = true;
    [SerializeField, Min(0.2f)] float arrowLength = 1.1f;
    [SerializeField, Min(0.01f)] float shaftRadius = 0.045f;
    [SerializeField, Min(0.02f)] float headRadius = 0.11f;
    [SerializeField, Range(0.15f, 0.45f)] float headLengthRatio = 0.28f;
    [SerializeField] bool showLabels = true;

    PlayerController _player;
    Transform _root;
    SolidArrow _wishArrow;
    SolidArrow _modelArrow;
    GUIStyle _labelStyle;

    /// <summary>绑定玩家；由 PlayerController 在装配后调用。</summary>
    public void Bind(PlayerController player)
    {
        _player = player;
        EnsureRoot();
    }

    /// <summary>是否绘制箭头。</summary>
    public void SetDrawEnabled(bool enabled) => drawArrows = enabled;

    void LateUpdate()
    {
        if (!drawArrows)
        {
            SetRootActive(false);
            return;
        }

        if (_player == null)
            _player = GetComponent<PlayerController>();
        if (_player == null || _player.Actor == null)
        {
            SetRootActive(false);
            return;
        }

        EnsureRoot();
        SetRootActive(true);

        CharacterActor actor = _player.Actor;
        Vector3 feet = ResolveFeetWorld(actor);

        Vector3 wish = actor.DebugMoveWishWorldDirection;
        wish.y = 0f;
        if (wish.sqrMagnitude > 0.0001f)
        {
            _wishArrow.SetVisible(true);
            _wishArrow.Place(feet, wish.normalized, arrowLength, shaftRadius, headRadius, headLengthRatio);
        }
        else
        {
            _wishArrow.SetVisible(false);
        }

        Vector3 modelFwd = ResolveModelForward(actor);
        modelFwd.y = 0f;
        if (modelFwd.sqrMagnitude > 0.0001f)
        {
            // 略抬高品红箭头，避免与黄箭完全共线时 z-fight
            Vector3 modelFeet = feet + Vector3.up * 0.02f;
            _modelArrow.SetVisible(true);
            _modelArrow.Place(modelFeet, modelFwd.normalized, arrowLength, shaftRadius, headRadius, headLengthRatio);
        }
        else
        {
            _modelArrow.SetVisible(false);
        }
    }

    void OnGUI()
    {
        if (!showLabels || !drawArrows || _root == null || !_root.gameObject.activeSelf)
            return;

        Camera cam = Camera.main;
        if (cam == null || _player?.Actor == null)
            return;

        EnsureLabelStyle();
        Vector3 feet = ResolveFeetWorld(_player.Actor);
        DrawLabel(cam, feet + Vector3.up * 0.35f, "Wish (黄)", ColorWish);
        DrawLabel(cam, feet + Vector3.up * 0.55f, "Model (品红)", ColorModel);
    }

    void OnDestroy()
    {
        if (_root != null)
            Destroy(_root.gameObject);
        _root = null;
        _wishArrow = default;
        _modelArrow = default;
    }

    void EnsureRoot()
    {
        if (_root != null)
        {
            if (_wishArrow.IsValid && _modelArrow.IsValid)
                return;
        }
        else
        {
            var go = new GameObject(RootName);
            go.transform.SetParent(null, false);
            _root = go.transform;
        }

        _wishArrow = SolidArrow.Create(_root, "WishArrow", ColorWish);
        _modelArrow = SolidArrow.Create(_root, "ModelFacingArrow", ColorModel);
    }

    void SetRootActive(bool active)
    {
        if (_root != null && _root.gameObject.activeSelf != active)
            _root.gameObject.SetActive(active);
    }

    /// <summary>脚底用表现插值位，避免逻辑帧阶梯造成箭头整体抖。</summary>
    Vector3 ResolveFeetWorld(CharacterActor actor)
    {
        Vector3 pos = actor.RenderedPosition;
        if (pos.sqrMagnitude < 0.0001f && _player != null)
            pos = _player.transform.position;
        return pos + Vector3.up * FeetLiftMeters;
    }

    /// <summary>模型朝向：优先 VisualMotionRoot 水平前向，否则 Presentation / Sim。</summary>
    static Vector3 ResolveModelForward(CharacterActor actor)
    {
        Transform visual = actor.VisualMotionRoot;
        if (visual != null)
            return visual.forward;

        Transform presentation = actor.PresentationRoot;
        if (presentation != null)
            return presentation.forward;

        return Vector3.forward;
    }

    void DrawLabel(Camera cam, Vector3 world, string text, Color color)
    {
        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z <= 0.05f)
            return;

        _labelStyle.normal.textColor = color;
        var rect = new Rect(screen.x + 6f, Screen.height - screen.y - 8f, 140f, 18f);
        GUI.Label(rect, text, _labelStyle);
    }

    void EnsureLabelStyle()
    {
        if (_labelStyle != null)
            return;

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };
    }

    /// <summary>圆柱杆 + 圆锥头的实心箭头。</summary>
    struct SolidArrow
    {
        public Transform Root;
        public Transform Shaft;
        public Transform Head;
        public MeshRenderer ShaftRenderer;
        public MeshRenderer HeadRenderer;

        public bool IsValid => Root != null;

        public static SolidArrow Create(Transform parent, string name, Color color)
        {
            var rootGo = new GameObject(name);
            rootGo.transform.SetParent(parent, false);

            GameObject shaftGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaftGo.name = "Shaft";
            shaftGo.transform.SetParent(rootGo.transform, false);
            DestroyCollider(shaftGo);

            // CreatePrimitive 无 Cone：自建单位圆锥（高 1、底半径 0.5，轴 +Y，尖在 +Y）
            GameObject headGo = new GameObject("Head");
            headGo.transform.SetParent(rootGo.transform, false);
            var headFilter = headGo.AddComponent<MeshFilter>();
            headFilter.sharedMesh = BuildUnitConeMesh(24);
            var headRenderer = headGo.AddComponent<MeshRenderer>();

            Material mat = CreateSolidMaterial(color);
            var shaftRenderer = shaftGo.GetComponent<MeshRenderer>();
            shaftRenderer.sharedMaterial = mat;
            headRenderer.sharedMaterial = mat;

            return new SolidArrow
            {
                Root = rootGo.transform,
                Shaft = shaftGo.transform,
                Head = headGo.transform,
                ShaftRenderer = shaftRenderer,
                HeadRenderer = headRenderer,
            };
        }

        /// <summary>单位圆锥：尖 (0,0.5,0)、底 y=-0.5 半径 0.5；缩放后高=scale.y、半径=scale.x/2。</summary>
        static Mesh BuildUnitConeMesh(int segments)
        {
            segments = Mathf.Max(8, segments);
            var mesh = new Mesh { name = "DebugSolidCone" };

            // tip + 底圆周 + 底心
            var verts = new Vector3[segments + 2];
            verts[0] = new Vector3(0f, 0.5f, 0f);
            verts[1] = new Vector3(0f, -0.5f, 0f);
            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                verts[i + 2] = new Vector3(Mathf.Cos(a) * 0.5f, -0.5f, Mathf.Sin(a) * 0.5f);
            }

            // 侧面三角形 + 底面扇形
            var tris = new int[segments * 6];
            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                int cur = i + 2;
                int next = (i + 1) % segments + 2;
                tris[t++] = 0;
                tris[t++] = next;
                tris[t++] = cur;
                tris[t++] = 1;
                tris[t++] = cur;
                tris[t++] = next;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.HideAndDontSave;
            return mesh;
        }

        public void SetVisible(bool visible)
        {
            if (Root != null)
                Root.gameObject.SetActive(visible);
        }

        /// <summary>箭头沿 worldForward，总长 length，脚点在 origin。</summary>
        public void Place(
            Vector3 origin,
            Vector3 worldForward,
            float length,
            float shaftRadiusMeters,
            float headRadiusMeters,
            float headLengthRatio)
        {
            if (Root == null || worldForward.sqrMagnitude < 0.0001f)
                return;

            float headLen = Mathf.Clamp(length * headLengthRatio, 0.08f, length * 0.5f);
            float shaftLen = Mathf.Max(0.05f, length - headLen);

            Root.position = origin;
            Root.rotation = Quaternion.LookRotation(worldForward.normalized, Vector3.up);

            // Unity 圆柱/圆锥默认轴为 +Y；转到父级 +Z（forward）
            Quaternion alignYToZ = Quaternion.Euler(90f, 0f, 0f);

            // Cylinder 默认高 2 → scale.y = shaftLen/2
            Shaft.localRotation = alignYToZ;
            Shaft.localScale = new Vector3(shaftRadiusMeters * 2f, shaftLen * 0.5f, shaftRadiusMeters * 2f);
            Shaft.localPosition = new Vector3(0f, 0f, shaftLen * 0.5f);

            // Cone 默认高 1、底在 -Y → 转到 +Z 后尖朝 +Z
            Head.localRotation = alignYToZ;
            Head.localScale = new Vector3(headRadiusMeters * 2f, headLen, headRadiusMeters * 2f);
            Head.localPosition = new Vector3(0f, 0f, shaftLen + headLen * 0.5f);
        }

        static void DestroyCollider(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null)
                Object.Destroy(col);
        }

        static Material CreateSolidMaterial(Color color)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var mat = new Material(shader)
            {
                color = color,
                hideFlags = HideFlags.HideAndDontSave,
            };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            return mat;
        }
    }
}
#else
/// <summary>非开发构建不创建朝向调试箭头。</summary>
public sealed class CharacterFacingDebugVisualizer : MonoBehaviour
{
    public void Bind(PlayerController player) { }

    public void SetDrawEnabled(bool enabled) { }
}
#endif
