using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>显示并编辑问题对应的序列化字段；不复制整套 Inspector。</summary>
public sealed class CharacterIssueFieldWindow : EditorWindow
{
    [SerializeField] Object asset;
    [SerializeField] string path;
    Vector2 scroll;
    /// <summary>打开准确字段路径；无效路径明确提示。</summary>
    public static void Open(Object asset, string path)
    {
        var window = GetWindow<CharacterIssueFieldWindow>("Content Field");
        window.asset = asset; window.path = path; window.Show();
    }
    void OnGUI()
    {
        if (asset == null) return;
        EditorGUILayout.LabelField(asset.name + " / " + path, EditorStyles.wordWrappedLabel);
        var so = new SerializedObject(asset); so.Update();
        var field = so.FindProperty(path);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (field != null) EditorGUILayout.PropertyField(field, true);
        else EditorGUILayout.HelpBox("字段已变化，请重新检查角色。", MessageType.Info);
        EditorGUILayout.EndScrollView();
        so.ApplyModifiedProperties();
    }
}
