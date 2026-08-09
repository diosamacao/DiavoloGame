using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>创建默认行为树资产并校验真敌 Definition 是否已挂树。</summary>
public static class EnemyBehaviorTreeSetupMenu
{
    const string MeleePath = "Assets/Data/Enemy/BehaviorTrees/BT_MeleeChaseAttack.asset";
    const string ChaseOnlyPath = "Assets/Data/Enemy/BehaviorTrees/BT_ChaseOnly.asset";
    const string KitePath = "Assets/Data/Enemy/BehaviorTrees/BT_Kite.asset";

    /// <summary>在标准目录创建预设树（已存在则跳过）。</summary>
    [MenuItem("ACT/Enemy/Create Default Behavior Tree Assets")]
    public static void CreateDefaultBehaviorTreeAssets()
    {
        EnsureFolder("Assets/Data/Enemy/BehaviorTrees");
        CreateIfMissing(MeleePath, EnemyBehaviorTreeKind.MeleeChaseAttack);
        CreateIfMissing(ChaseOnlyPath, EnemyBehaviorTreeKind.ChaseOnly);
        CreateIfMissing(KitePath, EnemyBehaviorTreeKind.Kite);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"已确保默认行为树：\n- {MeleePath}\n- {ChaseOnlyPath}\n- {KitePath}\n" +
            "请把对应树挂到真敌 Definition；木桩请关闭 BrainProfile.enableCombatActions。");
    }

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

    static void CreateIfMissing(string assetPath, EnemyBehaviorTreeKind kind)
    {
        var existing = AssetDatabase.LoadAssetAtPath<EnemyBehaviorTreeAsset>(assetPath);
        if (existing != null)
        {
            Debug.Log($"已存在，跳过：{assetPath}");
            return;
        }

        var asset = ScriptableObject.CreateInstance<EnemyBehaviorTreeAsset>();
        var so = new SerializedObject(asset);
        so.FindProperty("kind").enumValueIndex = (int)kind;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(asset, assetPath);
        Debug.Log($"已创建：{assetPath} ({kind})");
    }

    static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
