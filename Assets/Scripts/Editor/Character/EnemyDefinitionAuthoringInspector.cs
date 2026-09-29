using UnityEditor;
using UnityEngine;

/// <summary>敌人配置复用角色工作台并保留敌人专属参数编辑。</summary>
[CustomEditor(typeof(EnemyDefinition))]
public sealed class EnemyDefinitionAuthoringInspector : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("打开角色工作台")) CharacterAuthoringWindow.Open(target);
        DrawDefaultInspector();
    }
}
