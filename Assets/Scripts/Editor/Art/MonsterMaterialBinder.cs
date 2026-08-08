#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 创建 Monster 外部材质、写入 Remap，并触发 Reimport。
/// 实际赋材质由 <see cref="MonsterMaterialPostprocessor"/> 在导入时按 Renderer 名完成
/// （该 FBX 会落到 Default-Material，仅靠 Remap 无效）。
/// 菜单：ACTGame/Art/Bind Monster Materials
/// </summary>
public static class MonsterMaterialBinder
{
    const string ModelFbxPath = "Assets/Art/Characters/Monster/Monster.fbx";
    const string TexDir = "Assets/Art/Characters/Monster/Tex";
    const string MaterialsDir = "Assets/Art/Characters/Monster/Tex/Materials";

    const string BodySlot = "MAT_Monster_Goblin";
    const string WeaponSlot = "MAT_Metro_Goblin_Weapon";

    const string BodyAlbedo = "Monster_Goblin_D.png";
    const string BodyNormal = "Monster_Goblin_N.png";

    const string WeaponAlbedo = "Monster_Metro_Goblin_Weapon_D.png";
    const string WeaponNormal = "Monster_Metro_Goblin_Weapon_N.png";

    /// <summary>创建材质、配置 importer、Reimport，并校验场景可用的 Renderer 材质。</summary>
    [MenuItem("ACTGame/Art/Bind Monster Materials")]
    public static void Bind()
    {
        if (!File.Exists(ModelFbxPath))
        {
            EditorUtility.DisplayDialog("Monster Materials", $"找不到模型：\n{ModelFbxPath}", "OK");
            return;
        }

        if (!AssetDatabase.IsValidFolder(TexDir))
        {
            EditorUtility.DisplayDialog("Monster Materials", $"找不到贴图目录：\n{TexDir}", "OK");
            return;
        }

        EnsureMaterialsFolder();

        EnsureNormalMapImport(TexPath(BodyNormal));
        EnsureNormalMapImport(TexPath(WeaponNormal));

        Material body = EnsureStandardMaterial(BodySlot, BodyAlbedo, BodyNormal);
        Material weapon = EnsureStandardMaterial(WeaponSlot, WeaponAlbedo, WeaponNormal);
        if (body == null || weapon == null)
        {
            EditorUtility.DisplayDialog(
                "Monster Materials",
                "材质创建失败：请确认 Tex 下 D/N 贴图已导入。",
                "OK");
            return;
        }

        var importer = AssetImporter.GetAtPath(ModelFbxPath) as ModelImporter;
        if (importer == null)
        {
            EditorUtility.DisplayDialog("Monster Materials", $"无法获取 ModelImporter：\n{ModelFbxPath}", "OK");
            return;
        }

        // ImportStandard：才能触发 OnAssignMaterialModel；该 FBX 在 MaterialDescription 下会落 Default-Material
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.materialSearch = ModelImporterMaterialSearch.Local;

        ClearMaterialRemaps(importer);
        // Remap 兜底；主路径是 MonsterMaterialPostprocessor.OnAssignMaterialModel
        Remap(importer, BodySlot, body);
        Remap(importer, WeaponSlot, weapon);
        Remap(importer, "Monster_Goblin", body);
        Remap(importer, "Metro_Goblin_Weapon", weapon);
        Remap(importer, "Monster_Goblin_D", body);
        Remap(importer, "Monster_Metro_Goblin_Weapon_D", weapon);

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        string validation = ValidateImportedRenderers(body, weapon);
        Debug.Log(
            $"[MonsterMaterialBinder] 完成。body={AssetDatabase.GetAssetPath(body)}；" +
            $"weapon={AssetDatabase.GetAssetPath(weapon)}\n{validation}");

        bool ok = validation.StartsWith("校验：OK");
        EditorUtility.DisplayDialog(
            "Monster Materials",
            (ok ? "绑定成功。\n\n" : "仍异常，请把下列校验贴给开发。\n\n") + validation +
            "\n\n请拖拽 Monster.fbx 到场景确认。",
            "OK");
    }

    /// <summary>清除旧 Material Remap。</summary>
    static void ClearMaterialRemaps(ModelImporter importer)
    {
        var existing = importer.GetExternalObjectMap();
        if (existing == null || existing.Count == 0)
            return;

        var keys = new List<AssetImporter.SourceAssetIdentifier>();
        foreach (var kv in existing)
        {
            if (kv.Key.type == typeof(Material))
                keys.Add(kv.Key);
        }

        for (int i = 0; i < keys.Count; i++)
            importer.RemoveRemap(keys[i]);
    }

    /// <summary>写入 FBX 槽位 → 外部材质 Remap。</summary>
    static void Remap(ModelImporter importer, string slotName, Material material)
    {
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slotName), material);
    }

    /// <summary>确保 Materials 目录存在。</summary>
    static void EnsureMaterialsFolder()
    {
        if (AssetDatabase.IsValidFolder(MaterialsDir))
            return;

        if (!AssetDatabase.IsValidFolder(TexDir))
            return;

        AssetDatabase.CreateFolder(TexDir, "Materials");
    }

    /// <summary>创建或更新 Built-in Standard 材质。</summary>
    static Material EnsureStandardMaterial(string materialName, string albedoFile, string normalFile)
    {
        string matPath = $"{MaterialsDir}/{materialName}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        Shader shader = ResolveBuiltinStandardShader();
        if (shader == null)
        {
            Debug.LogError("[MonsterMaterialBinder] 找不到 Built-in Standard / Legacy Diffuse。");
            return null;
        }

        if (mat == null)
        {
            mat = new Material(shader) { name = materialName };
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else
        {
            mat.shader = shader;
        }

        Texture2D albedo = LoadTexture(albedoFile);
        Texture2D normal = LoadTexture(normalFile);
        if (albedo == null)
        {
            Debug.LogError($"[MonsterMaterialBinder] 缺少 Albedo：{TexPath(albedoFile)}");
            return null;
        }

        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0f);
        if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", 0.25f);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.25f);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", Color.white);

        if (mat.HasProperty("_MainTex"))
            mat.SetTexture("_MainTex", albedo);
        if (mat.HasProperty("_BaseMap"))
            mat.SetTexture("_BaseMap", albedo);

        if (normal != null && mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
            if (mat.HasProperty("_BumpScale"))
                mat.SetFloat("_BumpScale", 1f);
        }

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    /// <summary>解析真正的 Built-in Standard，避开 FX 包同名 Shader。</summary>
    static Shader ResolveBuiltinStandardShader()
    {
        Shader[] all = Resources.FindObjectsOfTypeAll<Shader>();
        for (int i = 0; i < all.Length; i++)
        {
            Shader shader = all[i];
            if (shader == null || shader.name != "Standard")
                continue;

            string path = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(path) || path.StartsWith("Resources/unity_builtin_extra"))
                return shader;
            if (!path.StartsWith("Assets/"))
                return shader;
        }

        Shader diffuse = Shader.Find("Legacy Shaders/Diffuse");
        if (diffuse != null)
            return diffuse;

        return Shader.Find("Standard");
    }

    /// <summary>通过主 Prefab 根节点校验子 Renderer 材质（比 LoadAllAssets 子资源更可靠）。</summary>
    static string ValidateImportedRenderers(Material body, Material weapon)
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFbxPath);
        if (root == null)
            return "校验：FAIL — 无法加载 Monster.fbx GameObject。";

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return "校验：FAIL — 未找到 Renderer。";

        var sb = new StringBuilder();
        int okCount = 0;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            Material[] mats = renderer.sharedMaterials;
            sb.AppendLine($"{renderer.GetType().Name} '{renderer.name}':");

            bool rendererOk = mats != null && mats.Length > 0;
            if (!rendererOk)
            {
                sb.AppendLine("  (无材质)");
                continue;
            }

            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null)
                {
                    sb.AppendLine($"  [{m}] null");
                    rendererOk = false;
                    continue;
                }

                Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : mat.mainTexture;
                bool isExpected = mat == body || mat == weapon ||
                                  mat.name == BodySlot || mat.name == WeaponSlot;
                bool hasTex = mainTex != null;
                if (!isExpected || !hasTex)
                    rendererOk = false;

                string mainTexName = hasTex ? mainTex.name : "null";
                string shaderName = mat.shader != null ? mat.shader.name : "null";
                sb.AppendLine($"  [{m}] mat={mat.name} shader={shaderName} mainTex={mainTexName} ok={isExpected && hasTex}");
            }

            if (rendererOk)
                okCount++;
        }

        if (okCount == renderers.Length)
            return $"校验：OK — Renderer {okCount}/{renderers.Length}\n{sb}";

        return $"校验：FAIL — Renderer {okCount}/{renderers.Length}\n{sb}";
    }

    /// <summary>将贴图导入类型设为 NormalMap。</summary>
    static void EnsureNormalMapImport(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        if (importer.textureType == TextureImporterType.NormalMap)
            return;

        importer.textureType = TextureImporterType.NormalMap;
        importer.sRGBTexture = false;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        Debug.Log($"[MonsterMaterialBinder] 已将法线贴图标记为 NormalMap：{assetPath}");
    }

    static string TexPath(string fileName) => $"{TexDir}/{fileName}";

    static Texture2D LoadTexture(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath(fileName));
    }
}
#endif
