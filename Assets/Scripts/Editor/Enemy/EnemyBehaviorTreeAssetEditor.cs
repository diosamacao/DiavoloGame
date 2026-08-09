using UnityEditor;
using UnityEngine;

/// <summary>行为树资产 Inspector：根节点、校验、打开 Graph 编辑器（无 Kind/预设）。</summary>
[CustomEditor(typeof(EnemyBehaviorTreeAsset))]
public sealed class EnemyBehaviorTreeAssetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("customRoot"), true);
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Ensure Ids + Sync Layout"))
            PrepareGraph();
        if (GUILayout.Button("Validate"))
            ValidateSelected();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("graphLayout"), true);

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("Open Behavior Tree Editor", GUILayout.Height(28)))
            EnemyBehaviorTreeEditorWindow.Open((EnemyBehaviorTreeAsset)target);

        EditorGUILayout.HelpBox(
            "运行真源 = customRoot（须手动配置）。graphLayout 仅坐标。\n" +
            "菜单：ACT/Enemy/Behavior Tree Editor（空格创建节点）。",
            MessageType.None);
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
