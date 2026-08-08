#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Monster.fbx 导入时强制赋材质。
/// 该 FBX 在 Unity 中会落到 Default-Material（Remap 槽位不生效），故按 Renderer 名绑定。
/// </summary>
public sealed class MonsterMaterialPostprocessor : AssetPostprocessor
{
    const string ModelFbxPath = "Assets/Art/Characters/Monster/Monster.fbx";
    const string BodyMatPath = "Assets/Art/Characters/Monster/Tex/Materials/MAT_Monster_Goblin.mat";
    const string WeaponMatPath = "Assets/Art/Characters/Monster/Tex/Materials/MAT_Metro_Goblin_Weapon.mat";

    /// <summary>仅处理 Monster 主模型，并固定导入材质策略。</summary>
    void OnPreprocessModel()
    {
        if (!IsMonsterModel(assetPath))
            return;

        var importer = (ModelImporter)assetImporter;
        // OnAssignMaterialModel 仅在 ImportStandard(Legacy) 下回调；MaterialDescription 模式不会进这里
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.materialSearch = ModelImporterMaterialSearch.Local;
    }

    /// <summary>
    /// Legacy 导入赋材质回调：按 Renderer/槽位名替换为外部材质（避免 Default-Material 白模）。
    /// </summary>
    Material OnAssignMaterialModel(Material material, Renderer renderer)
    {
        if (!IsMonsterModel(assetPath))
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

        // 兜底：未知槽位也给身体材质，避免再落回 Default-Material 白模
        Debug.LogWarning(
            $"[MonsterMaterialPostprocessor] 未识别槽位，回退身体材质：mat={matName}, renderer={rendererName}");
        return body;
    }

    static bool IsMonsterModel(string path)
    {
        return string.Equals(
            path.Replace('\\', '/'),
            ModelFbxPath,
            StringComparison.OrdinalIgnoreCase);
    }

    static bool ContainsIgnoreCase(string value, string token)
    {
        return !string.IsNullOrEmpty(value) &&
               value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
#endif
