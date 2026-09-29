using UnityEditor;
using UnityEngine;

/// <summary>身体配置按任务分组，反应和模式不再混在基础数值中。</summary>
[CustomEditor(typeof(CharacterConfig))]
public sealed class CharacterConfigAuthoringInspector : Editor
{
    bool motor, combat, modes, resources;
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("打开角色工作台")) CharacterAuthoringWindow.Open(target);
        serializedObject.Update();
        foreach (string field in new[] { "modelPrefab", "modelLocalPosition", "modelLocalEulerAngles" })
            EditorGUILayout.PropertyField(serializedObject.FindProperty(field));
        DrawGroup(ref motor, "移动速度与碰撞体", "motor");
        DrawGroup(ref combat, "战斗身体与反应", "combat");
        DrawGroup(ref modes, "模式与内容绑定", "combatModes");
        DrawGroup(ref resources, "资源上限与回复", "resources");
        serializedObject.ApplyModifiedProperties();
    }
    void DrawGroup(ref bool expanded, string label, string path)
    {
        expanded = EditorGUILayout.Foldout(expanded, label, true);
        if (expanded) EditorGUILayout.PropertyField(serializedObject.FindProperty(path), true);
    }
}
