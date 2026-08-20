using UnityEngine;

/// <summary>预览用运行时材质。优先自定义 Shader，找不到则回退 Built-in。</summary>
public sealed class MalevolentShrineMaterials
{
    public Material wood;
    public Material bone;
    public Material horn;
    public Material flesh;
    public Material fleshLit;
    public Material ground;
    public Material building;
    public Material slice;
    public Material ring;
    public Material slashKai;
    public Material slashHachi;
    public Material dust;
    public Material smoke;
    public Material ash;
    public Material energy;
    public Material caster;
    public Material groundGlow;
    public Texture2D particleTex;

    public static MalevolentShrineMaterials Create()
    {
        MalevolentShrineMaterials kit = new MalevolentShrineMaterials();
        kit.wood = Solid(new Color(0.13f, 0.07f, 0.045f), 0.08f, 0.35f);
        kit.bone = Solid(new Color(0.62f, 0.54f, 0.41f), 0.04f, 0.2f);
        kit.horn = Solid(new Color(0.2f, 0.13f, 0.08f), 0.02f, 0.15f);
        kit.flesh = Solid(new Color(0.22f, 0.04f, 0.04f), 0.35f, 0.55f);
        kit.fleshLit = Unlit(new Color(0.45f, 0.03f, 0.02f, 1f), zWrite: true);
        kit.ground = Solid(new Color(0.07f, 0.06f, 0.055f), 0.02f, 0.55f);
        kit.building = Solid(new Color(0.23f, 0.22f, 0.2f), 0.06f, 0.4f);
        kit.slice = Solid(new Color(0.3f, 0.26f, 0.22f), 0.08f, 0.35f);
        kit.caster = Solid(new Color(0.16f, 0.14f, 0.13f), 0.2f, 0.45f);
        kit.particleTex = SoftBlob(64);
        kit.ring = FindOrUnlit("ACT/Preview/MalevolentShrine/Ring", new Color(0.42f, 0.06f, 0.04f, 0.62f));
        kit.slashKai = CreateSlash(new Color(0.02f, 0.02f, 0.025f, 1f), new Color(0.93f, 0.93f, 0.91f, 1f), 0.09f);
        kit.slashHachi = CreateSlash(new Color(0.02f, 0.02f, 0.025f, 1f), new Color(0.94f, 0.93f, 0.9f, 1f), 0.1f);
        kit.dust = Particle(new Color(0.28f, 0.16f, 0.12f, 0.5f), additive: false, kit.particleTex);
        kit.smoke = Particle(new Color(0.2f, 0.07f, 0.05f, 0.38f), additive: false, kit.particleTex);
        kit.ash = Particle(new Color(0.16f, 0.1f, 0.08f, 0.42f), additive: false, kit.particleTex);
        kit.energy = Particle(new Color(0.55f, 0.08f, 0.04f, 0.8f), additive: true, kit.particleTex);
        kit.groundGlow = Unlit(new Color(0.55f, 0.07f, 0.04f, 0.32f), zWrite: false);
        if (kit.groundGlow != null && kit.particleTex != null && kit.groundGlow.HasProperty("_MainTex"))
            kit.groundGlow.SetTexture("_MainTex", kit.particleTex);
        return kit;
    }

    public void DestroyCreated()
    {
        Destroy(wood);
        Destroy(bone);
        Destroy(horn);
        Destroy(flesh);
        Destroy(fleshLit);
        Destroy(ground);
        Destroy(building);
        Destroy(slice);
        Destroy(ring);
        Destroy(slashKai);
        Destroy(slashHachi);
        Destroy(dust);
        Destroy(smoke);
        Destroy(ash);
        Destroy(energy);
        Destroy(caster);
        Destroy(groundGlow);
        Destroy(particleTex);
    }

    static Material Solid(Color color, float metallic, float smoothness)
    {
        Shader shader = Shader.Find("Standard");
        Material mat = shader != null ? new Material(shader) : Unlit(color, true);
        if (shader != null)
        {
            mat.color = color;
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Glossiness", smoothness);
        }

        return mat;
    }

    static Material Unlit(Color color, bool zWrite)
    {
        Shader shader = Shader.Find("ACT/Preview/MalevolentShrine/UnlitAlpha");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        Material mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_ZWrite"))
            mat.SetFloat("_ZWrite", zWrite ? 1f : 0f);
        return mat;
    }

    static Material FindOrUnlit(string shaderName, Color color)
    {
        Shader shader = Shader.Find(shaderName);
        Material mat = shader != null ? new Material(shader) : Unlit(color, false);
        mat.color = color;
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        return mat;
    }

    static Material CreateSlash(Color fill, Color outline, float outlineWidth)
    {
        Shader shader = Shader.Find("ACT/Preview/MalevolentShrine/Slash");
        if (shader == null)
            return Unlit(fill, false);
        Material mat = new Material(shader);
        mat.SetColor("_FillColor", fill);
        mat.SetColor("_OutlineColor", outline);
        mat.SetFloat("_Outline", outlineWidth);
        mat.SetFloat("_TipPower", 0.72f);
        return mat;
    }

    static Material Particle(Color color, bool additive, Texture2D texture)
    {
        Shader shader = Shader.Find(additive ? "Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            return Unlit(color, false);
        Material mat = new Material(shader);
        mat.SetColor("_TintColor", color);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        if (texture != null && mat.HasProperty("_MainTex"))
            mat.SetTexture("_MainTex", texture);
        return mat;
    }

    static Texture2D SoftBlob(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "MalevolentShrine.SoftBlob",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        float mid = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x - mid) / mid;
                float v = (y - mid) / mid;
                float r = Mathf.Sqrt(u * u + v * v);
                float lump = 0.12f * Mathf.Sin(u * 5.5f) * Mathf.Sin(v * 4.2f);
                float alpha = Mathf.Pow(Mathf.Clamp01(1.05f - r + lump), 1.85f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply(false, false);
        return tex;
    }

    static void Destroy(Object asset)
    {
        if (asset == null)
            return;
        if (Application.isPlaying)
            Object.Destroy(asset);
        else
            Object.DestroyImmediate(asset);
    }
}
