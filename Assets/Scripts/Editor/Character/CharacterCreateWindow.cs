using System;
using UnityEditor;
using UnityEngine;

/// <summary>预览并创建独立的空角色草稿及用途目录。</summary>
public sealed class CharacterCreateWindow : EditorWindow
{
    GameObject model;
    string parent = CharacterAssetLayout.DefaultParent;
    string id = "NewCharacter";
    string error;

    /// <summary>打开空角色创建窗口，不读取当前选中角色的配置。</summary>
    public static void Open() => GetWindow<CharacterCreateWindow>(true, "创建空白角色").Show();
    void OnGUI()
    {
        id = EditorGUILayout.TextField("角色 ID", id);
        parent = EditorGUILayout.TextField("角色父目录", parent);
        model = (GameObject)EditorGUILayout.ObjectField("模型（可稍后配置）", model, typeof(GameObject), false);
        EditorGUILayout.HelpBox("ID 使用英文字母开头的字母、数字或下划线（最多 64 位）。创建全新配置，不复制选中角色内容。", MessageType.Info);
        string path = null;
        try { path = CharacterAssetLayout.CharacterFolder(parent, id); }
        catch (Exception e) { EditorGUILayout.HelpBox(e.Message, MessageType.Warning); }
        if (path != null)
        {
            EditorGUILayout.LabelField("创建路径", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(path + "/\n"
                + "  " + id + "_Character.asset\n"
                + "  Config/" + id + "_Config.asset\n"
                + "  Graphs/" + id + "_Default_Graph.asset\n"
                + "  Locomotion/" + id + "_Default_Locomotion.asset\n"
                + "  Actions/\n  Reactions/", GUILayout.Height(115));
        }
        EditorGUILayout.HelpBox("这是待填写草稿：需补齐模型、移动动画、动作与入口，完成烘焙和校验后再接入出战配置。", MessageType.Info);
        using (new EditorGUI.DisabledScope(path == null))
            if (GUILayout.Button("创建空白角色"))
                try
                {
                    var result = CharacterAuthoringService.CreateCharacter(id, parent, model);
                    Selection.activeObject = result; CharacterAuthoringWindow.Open(result); Close();
                }
                catch (Exception e) { error = e.Message; }
        if (error != null) EditorGUILayout.HelpBox(error, MessageType.Error);
    }
}
