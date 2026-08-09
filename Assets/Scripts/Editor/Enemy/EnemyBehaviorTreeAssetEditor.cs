using UnityEditor;
using UnityEngine;

/// <summary>行为树资产 Inspector：Fill 预设、校验、Graph 布局只读预览。</summary>
[CustomEditor(typeof(EnemyBehaviorTreeAsset))]
public sealed class EnemyBehaviorTreeAssetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("kind"));
        var kind = (EnemyBehaviorTreeKind)serializedObject.FindProperty("kind").enumValueIndex;

        if (kind == EnemyBehaviorTreeKind.Custom)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("customRoot"), true);
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Fill ← Melee"))
                FillCustom(EnemyBehaviorTreeDefFactory.CreateMeleeChaseAttack());
            if (GUILayout.Button("Fill ← ChaseOnly"))
                FillCustom(EnemyBehaviorTreeDefFactory.CreateChaseOnly());
            if (GUILayout.Button("Fill ← Kite"))
                FillCustom(EnemyBehaviorTreeDefFactory.CreateKite());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Ensure Ids + Sync Layout"))
                PrepareGraph();
            if (GUILayout.Button("Validate"))
                ValidateSelected();
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox(
                "当前使用代码预设种树（Melee / ChaseOnly / Kite）。切换 Kind=Custom 后可编辑节点。",
                MessageType.Info);
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("graphLayout"), true);

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("Open Behavior Tree Editor", GUILayout.Height(28)))
            EnemyBehaviorTreeEditorWindow.Open((EnemyBehaviorTreeAsset)target);

        EditorGUILayout.HelpBox(
            "运行真源 = customRoot。graphLayout 仅坐标。\n" +
            "菜单：ACT/Enemy/Behavior Tree Editor（空格创建节点）。",
            MessageType.None);
    }

    void FillCustom(EnemyBehaviorNodeDef root)
    {
        var asset = (EnemyBehaviorTreeAsset)target;
        Undo.RecordObject(asset, "Fill Behavior Tree Custom Root");
        asset.SetKindForEditor(EnemyBehaviorTreeKind.Custom);
        asset.SetCustomRootForEditor(root);
        EditorUtility.SetDirty(asset);
        serializedObject.Update();
    }

    void PrepareGraph()
    {
        var asset = (EnemyBehaviorTreeAsset)target;
        Undo.RecordObject(asset, "Prepare Behavior Tree Graph");
        asset.PrepareForGraphEditor();
        EditorUtility.SetDirty(asset);
        serializedObject.Update();
        Debug.Log($"[{asset.name}] Ensure Ids + Sync Layout 完成。", asset);
    }

    void ValidateSelected()
    {
        var asset = (EnemyBehaviorTreeAsset)target;
        EnemyBehaviorTreeValidationResult result = asset.ValidateAsset();
        if (result.IsValid)
            Debug.Log(
                $"[{asset.name}] Validate 通过（警告 {result.Warnings.Count}）。",
                asset);
        else
            Debug.LogError(
                $"[{asset.name}] Validate 失败：{result.Errors.Count} error / {result.Warnings.Count} warning。",
                asset);

        for (int i = 0; i < result.Errors.Count; i++)
            Debug.LogError(result.Errors[i], asset);
        for (int i = 0; i < result.Warnings.Count; i++)
            Debug.LogWarning(result.Warnings[i], asset);
    }
}
