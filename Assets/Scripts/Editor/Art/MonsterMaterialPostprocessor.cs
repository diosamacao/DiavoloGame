#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Monster 目录下全部 FBX（主模型 / Root / Inplace）导入时强制赋材质。
/// 这些 FBX 在 MaterialDescription 模式下会落到 Default-Material，故用 Legacy + Renderer 名绑定。
/// </summary>
public sealed class MonsterMaterialPostprocessor : AssetPostprocessor
{
    public const string MonsterRootFolder = "Assets/Art/Characters/Monster";
    public const string BodyMatPath = "Assets/Art/Characters/Monster/Tex/Materials/MAT_Monster_Goblin.mat";
    public const string WeaponMatPath = "Assets/Art/Characters/Monster/Tex/Materials/MAT_Metro_Goblin_Weapon.mat";

    /// <summary>Monster 目录下任意 FBX：固定 Legacy 材质导入策略。</summary>
    void OnPreprocessModel()
    {
        if (!IsMonsterFbx(assetPath))
            return;

        var importer = (ModelImporter)assetImporter;
        // OnAssignMaterialModel 仅在 ImportStandard(Legacy) 下回调
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.materialSearch = ModelImporterMaterialSearch.Local;
    }

    /// <summary>
    /// Legacy 赋材质回调：按 Renderer/槽位名替换为外部材质（避免 Default-Material 白模）。
    /// </summary>
    Material OnAssignMaterialModel(Material material, Renderer renderer)
    {
        if (!IsMonsterFbx(assetPath))
            return null;

        Material body = AssetDatabase.LoadAssetAtPath<Material>(BodyMatPath);
        Material weapon = AssetDatabase.LoadAssetAtPath<Material>(WeaponMatPath);
        if (body == null || weapon == null)
        {
            Debug.LogWarning(
                "[MonsterMaterialPostprocessor] 外部材质尚未创建。请先执行菜单 ACTGame/Art/Bind Monster Materials。");
            return null;
        }

        string matName = material != null ? material.name : string.Empty;
        string rendererName = renderer != null ? renderer.name : string.Empty;

        if (ContainsIgnoreCase(matName, "Weapon") || ContainsIgnoreCase(rendererName, "Weapon"))
            return weapon;

        if (ContainsIgnoreCase(matName, "Goblin") || ContainsIgnoreCase(rendererName, "Goblin") ||
            ContainsIgnoreCase(matName, "Monster"))
            return body;

        Debug.LogWarning(
            $"[MonsterMaterialPostprocessor] 未识别槽位，回退身体材质：path={assetPath}, mat={matName}, renderer={rendererName}");
        return body;
    }

    /// <summary>是否为 Monster 目录下的 FBX（含 Root / Inplace / 主模型）。</summary>
    public static bool IsMonsterFbx(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        string normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith(MonsterRootFolder + "/", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalized, MonsterRootFolder, StringComparison.OrdinalIgnoreCase))
            return false;

        return normalized.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
    }

    static bool ContainsIgnoreCase(string value, string token)
    {
        return !string.IsNullOrEmpty(value) &&
               value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
#endif
