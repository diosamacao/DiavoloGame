using UnityEngine;

/// <summary>近景假建筑：先整块，被「解」命中后切成预切片并延迟崩塌。</summary>
public sealed class MalevolentShrineDestructibleBuilding : MonoBehaviour
{
    public Transform solid;
    public Transform sliceRoot;
    public bool sliced;
    public float sliceTime = -1f;
    public bool collapsed;
    public bool faded;

    public void Prepare(Transform solidBody, Transform slices)
    {
        solid = solidBody;
        sliceRoot = slices;
        if (sliceRoot != null)
            sliceRoot.gameObject.SetActive(false);
        ResetState();
    }

    public void ResetState()
    {
        sliced = false;
        sliceTime = -1f;
        collapsed = false;
        faded = false;
        if (solid != null)
            solid.gameObject.SetActive(true);
        if (sliceRoot == null)
            return;

        sliceRoot.gameObject.SetActive(false);
        for (int i = 0; i < sliceRoot.childCount; i++)
        {
            Transform piece = sliceRoot.GetChild(i);
            Rigidbody body = piece.GetComponent<Rigidbody>();
            if (body != null)
            {
                if (Application.isPlaying)
                    Destroy(body);
                else
                    DestroyImmediate(body);
            }

            MalevolentShrineSlicePiece slice = piece.GetComponent<MalevolentShrineSlicePiece>();
            if (slice != null)
            {
                piece.localPosition = slice.restLocalPosition;
                piece.localRotation = slice.restLocalRotation;
            }

            Renderer renderer = piece.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color color = renderer.sharedMaterial != null ? renderer.sharedMaterial.color : Color.white;
                color.a = 1f;
                SetRendererColor(renderer, color);
                if (Application.isPlaying && renderer.sharedMaterial != null)
                    renderer.material.color = color;
            }
        }
    }

    public void Slice(Vector3 planePoint, Vector3 planeNormal, float time)
    {
        if (sliced || sliceRoot == null || solid == null)
            return;

        sliced = true;
        sliceTime = time;
        solid.gameObject.SetActive(false);
        sliceRoot.gameObject.SetActive(true);
        planeNormal.Normalize();

        for (int i = 0; i < sliceRoot.childCount; i++)
        {
            Transform piece = sliceRoot.GetChild(i);
            float side = Mathf.Sign(Vector3.Dot(piece.position - planePoint, planeNormal));
            if (Mathf.Approximately(side, 0f))
                side = 1f;
            piece.position += planeNormal * side * 0.045f;
        }
    }

    public void Collapse()
    {
        if (!sliced || collapsed || sliceRoot == null)
            return;

        collapsed = true;
        for (int i = 0; i < sliceRoot.childCount; i++)
        {
            Transform piece = sliceRoot.GetChild(i);
            Rigidbody body = piece.gameObject.GetComponent<Rigidbody>();
            if (body == null)
                body = piece.gameObject.AddComponent<Rigidbody>();
            body.mass = 18f;
            body.drag = 0.35f;
            body.angularDrag = 0.2f;
            Vector3 outward = (piece.position - transform.position);
            outward.y = 0f;
            body.AddForce(outward.normalized * 1.6f + Vector3.up * 1.1f, ForceMode.Impulse);
            body.AddTorque(Random.insideUnitSphere * 2.4f, ForceMode.Impulse);
        }
    }

    public void Fade(float alpha)
    {
        if (sliceRoot == null)
            return;

        faded = alpha <= 0.02f;
        for (int i = 0; i < sliceRoot.childCount; i++)
        {
            Renderer renderer = sliceRoot.GetChild(i).GetComponent<Renderer>();
            if (renderer == null)
                continue;
            Color color = renderer.material.color;
            color.a = alpha;
            SetRendererColor(renderer, color);
        }
    }

    static void SetRendererColor(Renderer renderer, Color color)
    {
        Material mat = Application.isPlaying ? renderer.material : renderer.sharedMaterial;
        if (mat == null)
            return;
        mat.color = color;
        if (color.a < 0.99f)
        {
            mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
    }
}

/// <summary>预切片块的初始姿态，供重播时复位。</summary>
public sealed class MalevolentShrineSlicePiece : MonoBehaviour
{
    public Vector3 restLocalPosition;
    public Quaternion restLocalRotation;
}
