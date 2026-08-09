using UnityEditor;
using UnityEngine;

/// <summary>校验 EnemyDefinition 挂树与 BehaviorTree 资产结构（不再提供默认种树菜单）。</summary>
public static class EnemyBehaviorTreeSetupMenu
{
    /// <summary>扫描 EnemyDefinition 挂载 + 全部 BehaviorTreeAsset 结构校验。</summary>
    [MenuItem("ACT/Enemy/Validate Enemy Behavior Trees")]
    public static void ValidateEnemyBehaviorTrees()
    {
        int errors = 0;
        int ok = 0;

        string[] defGuids = AssetDatabase.FindAssets("t:EnemyDefinition");
        for (int i = 0; i < defGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(defGuids[i]);
            var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (def == null)
                continue;

            if (!def.Validate(def))
            {
                errors++;
                continue;
            }

            if (def.BrainProfile != null
                && def.BrainProfile.EnableCombatActions
                && def.BehaviorTree == null)
            {
                Debug.LogError($"[{path}] Combat Actions 开启但 BehaviorTree 为空。", def);
                errors++;
                continue;
            }

            if (def.BrainProfile != null
                && !def.BrainProfile.EnableCombatActions
                && def.BehaviorTree != null)
            {
                Debug.LogWarning(
                    $"[{path}] 木桩（Combat Actions 关）仍挂了 BehaviorTree，可忽略但不会创建 Runner。",
                    def);
            }

            ok++;
        }

        string[] treeGuids = AssetDatabase.FindAssets("t:EnemyBehaviorTreeAsset");
        int treeOk = 0;
        int treeFail = 0;
        for (int i = 0; i < treeGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(treeGuids[i]);
            var asset = AssetDatabase.LoadAssetAtPath<EnemyBehaviorTreeAsset>(path);
            if (asset == null)
                continue;

            EnemyBehaviorTreeValidationResult result = asset.ValidateAsset();
            if (!result.IsValid)
            {
                treeFail++;
                errors++;
                for (int e = 0; e < result.Errors.Count; e++)
                    Debug.LogError($"[{path}] {result.Errors[e]}", asset);
            }
            else
            {
                treeOk++;
                for (int w = 0; w < result.Warnings.Count; w++)
                    Debug.LogWarning($"[{path}] {result.Warnings[w]}", asset);
            }
        }

        Debug.Log(
            $"Validate Enemy Behavior Trees：Definition 通过 {ok}；" +
            $"TreeAsset 通过 {treeOk} / 失败 {treeFail}；总问题 {errors}。");
    }
}
