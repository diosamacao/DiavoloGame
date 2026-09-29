using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>角色编辑器共用的条件字段与挂点选择，保持既有名字寻址语义。</summary>
public static class CharacterAuthoringFields
{
    /// <summary>仅启用切模式行为时显示其参数，隐藏时保留原值。</summary>
    public static void DrawModeSwitch(SerializedProperty node)
    {
        var behaviors = node.FindPropertyRelative("startBehaviors");
        for (int i = 0; i < behaviors.arraySize; i++)
            if (behaviors.GetArrayElementAtIndex(i).intValue == (int)ActionGraphStartBehaviorType.SwitchCombatMode)
            {
                EditorGUILayout.PropertyField(node.FindPropertyRelative("switchCombatModeTarget"));
                EditorGUILayout.PropertyField(node.FindPropertyRelative("switchCombatModePolicy"));
                return;
            }
    }

    /// <summary>显示模型名字候选；缺失或重名保持文本可编辑并提示，不生成路径身份。</summary>
    public static string DrawAnchorPicker(Transform model, string current)
    {
        if (model == null) return current;
        var names = model.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
        string[] choices = new[] { "使用默认挂点（空）" }.Concat(names.Distinct().OrderBy(n => n)).ToArray();
        int selected = string.IsNullOrEmpty(current) ? 0 : System.Array.IndexOf(choices, current);
        EditorGUI.BeginChangeCheck();
        int next = EditorGUILayout.Popup("从模型选择挂点", selected, choices);
        if (EditorGUI.EndChangeCheck() && next >= 0) return next == 0 ? "" : choices[next];
        if (!string.IsNullOrEmpty(current))
        {
            int count = names.Count(n => n == current.Trim());
            if (count != 1) EditorGUILayout.HelpBox(count == 0 ? "模型中缺少此挂点；运行时将回退默认挂点。" : "模型有重名挂点；运行时使用首个同名节点，请确认模型命名。", MessageType.Warning);
        }
        return current;
    }
}
