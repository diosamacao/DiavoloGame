#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 创建 Monster 外部材质，并对主模型 + Blender_FbxFormat_Root + Blender_FbxFormat_Inplace
/// 全部 FBX 执行 Reimport（实际赋材质见 <see cref="MonsterMaterialPostprocessor"/>）。
/// 菜单：ACTGame/Art/Bind Monster Materials
/// </summary>
public static class MonsterMaterialBinder
{
    const string ModelFbxPath = "Assets/Art/Characters/Monster/Monster.fbx";
    const string RootAnimFolder = "Assets/Art/Characters/Monster/Blender_FbxFormat_Root";
    const string InplaceAnimFolder = "Assets/Art/Characters/Monster/Blender_FbxFormat_Inplace";
    const string TexDir = "Assets/Art/Characters/Monster/Tex";
    const string MaterialsDir = "Assets/Art/Characters/Monster/Tex/Materials";

    const string BodySlot = "MAT_Monster_Goblin";
    const string WeaponSlot = "MAT_Metro_Goblin_Weapon";

    const string BodyAlbedo = "Monster_Goblin_D.png";
    const string BodyNormal = "Monster_Goblin_N.png";

    const string WeaponAlbedo = "Monster_Metro_Goblin_Weapon_D.png";
    const string WeaponNormal = "Monster_Metro_Goblin_Weapon_N.png";

    /// <summary>创建材质并对 Monster 目录相关 FBX 全部 Reimport + 校验。</summary>
    [MenuItem("ACTGame/Art/Bind Monster Materials")]
    public static void Bind()
    {
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

        List<string> fbxPaths = CollectMonsterFbxPaths();
        if (fbxPaths.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Monster Materials",
                "未找到任何 Monster FBX（主模型 / Root / Inplace）。",
                "OK");
            return;
        }

        int reimported = 0;
        try
        {
            for (int i = 0; i < fbxPaths.Count; i++)
            {
                string path = fbxPaths[i];
                EditorUtility.DisplayProgressBar(
                    "Bind Monster Materials",
                    $"Reimport ({i + 1}/{fbxPaths.Count})\n{path}",
                    (float)(i + 1) / fbxPaths.Count);

                if (ConfigureAndReimport(path, body, weapon))
                    reimported++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        string validation = ValidateAllFbx(fbxPaths, body, weapon);
        Debug.Log(
            $"[MonsterMaterialBinder] Reimport {reimported}/{fbxPaths.Count}\n{validation}");

        bool ok = validation.StartsWith("校验：OK");
        EditorUtility.DisplayDialog(
            "Monster Materials",
            (ok ? "绑定成功。\n\n" : "部分异常，请查看详情。\n\n") +
            $"已处理 FBX：{reimported}/{fbxPaths.Count}\n\n{validation}",
            "OK");
    }

    /// <summary>收集主模型 + Root + Inplace 下全部 FBX 路径。</summary>
    static List<string> CollectMonsterFbxPaths()
    {
        var paths = new List<string>();
        var seen = new HashSet<string>();

        void AddPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            string n = path.Replace('\\', '/');
            if (!n.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                return;
            if (seen.Add(n))
                paths.Add(n);
        }

        if (File.Exists(ModelFbxPath) || AssetDatabase.LoadMainAssetAtPath(ModelFbxPath) != null)
            AddPath(ModelFbxPath);

        AddFolderFbx(RootAnimFolder, AddPath);
        AddFolderFbx(InplaceAnimFolder, AddPath);

        paths.Sort(System.StringComparer.OrdinalIgnoreCase);
        return paths;
    }

    /// <summary>把文件夹内 FBX 加入列表。</summary>
    static void AddFolderFbx(string folder, System.Action<string> addPath)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            return;

        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            addPath(path);
        }
    }

    /// <summary>配置 importer Remap 并强制 Reimport；依赖 Postprocessor 真正赋材质。</summary>
    static bool ConfigureAndReimport(string fbxPath, Material body, Material weapon)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[MonsterMaterialBinder] 非 ModelImporter：{fbxPath}");
            return false;
        }

        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.materialSearch = ModelImporterMaterialSearch.Local;

        ClearMaterialRemaps(importer);
        Remap(importer, BodySlot, body);
        Remap(importer, WeaponSlot, weapon);
        Remap(importer, "Monster_Goblin", body);
        Remap(importer, "Metro_Goblin_Weapon", weapon);
        Remap(importer, "Monster_Goblin_D", body);
        Remap(importer, "Monster_Metro_Goblin_Weapon_D", weapon);

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        return true;
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

    /// <summary>校验所有含网格的 FBX；无 Renderer 的纯动画文件记为 skip。</summary>
    static string ValidateAllFbx(List<string> fbxPaths, Material body, Material weapon)
    {
        int withMeshOk = 0;
        int withMeshFail = 0;
        int skippedNoMesh = 0;
        var failDetails = new StringBuilder();

        for (int i = 0; i < fbxPaths.Count; i++)
        {
            string path = fbxPaths[i];
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null)
            {
                withMeshFail++;
                failDetails.AppendLine($"FAIL {path} — 无法加载 GameObject");
                continue;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                skippedNoMesh++;
                continue;
            }

            bool fileOk = true;
            for (int r = 0; r < renderers.Length; r++)
            {
                Material[] mats = renderers[r].sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    fileOk = false;
                    break;
                }

                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null)
                    {
                        fileOk = false;
                        break;
                    }

                    Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : mat.mainTexture;
                    bool isExpected = mat == body || mat == weapon ||
                                      mat.name == BodySlot || mat.name == WeaponSlot;
                    if (!isExpected || mainTex == null)
                    {
                        fileOk = false;
                        failDetails.AppendLine(
                            $"FAIL {Path.GetFileName(path)} / {renderers[r].name}: mat={mat.name} tex={(mainTex != null ? mainTex.name : "null")}");
                        break;
                    }
                }

                if (!fileOk)
                    break;
            }

            if (fileOk)
                withMeshOk++;
            else
                withMeshFail++;
        }

        int meshTotal = withMeshOk + withMeshFail;
        var sb = new StringBuilder();
        if (withMeshFail == 0 && meshTotal > 0)
            sb.AppendLine($"校验：OK — 含网格 FBX {withMeshOk}/{meshTotal}，无网格跳过 {skippedNoMesh}");
        else if (meshTotal == 0)
            sb.AppendLine($"校验：FAIL — 所有 FBX 均无 Renderer（跳过 {skippedNoMesh}）");
        else
            sb.AppendLine($"校验：FAIL — 含网格 OK {withMeshOk} / FAIL {withMeshFail}，无网格跳过 {skippedNoMesh}");

        if (failDetails.Length > 0)
        {
            sb.AppendLine("失败明细（最多 20 条）：");
            string[] lines = failDetails.ToString().Split('\n');
            int limit = Mathf.Min(20, lines.Length);
            for (int i = 0; i < limit; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    sb.AppendLine(lines[i].TrimEnd('\r'));
            }
        }

        return sb.ToString().TrimEnd();
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
