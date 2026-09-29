using UnityEditor;
using UnityEngine;

/// <summary>角色身份 Inspector 提供统一工作台入口。</summary>
[CustomEditor(typeof(CharacterDefinition))]
public sealed class CharacterDefinitionAuthoringInspector : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("打开角色工作台")) CharacterAuthoringWindow.Open(target);
        DrawDefaultInspector();
    }
}
