using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>左侧 ActionDefinition 资产列表：搜索、创建入口与选择。</summary>
public sealed class ActionListPanel
{
    string _search = string.Empty;
    Vector2 _scroll;
    readonly List<ActionDefinition> _actions = new();

    /// <summary>刷新项目中全部 ActionDefinition 资产。</summary>
    public void Refresh()
    {
        _actions.Clear();
        string[] guids = AssetDatabase.FindAssets("t:ActionDefinition");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ActionDefinition action = AssetDatabase.LoadAssetAtPath<ActionDefinition>(path);
            if (action != null)
                _actions.Add(action);
        }

        _actions.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
    }

    /// <summary>
    /// 绘制列表；返回用户新选中的资产。
    /// onRequestCreate：点击 Create 时打开独立创建面板。
    /// </summary>
    public ActionDefinition Draw(Rect rect, ActionDefinition current, System.Action onRequestCreate)
    {
        GUILayout.BeginArea(rect);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create", GUILayout.Width(70f)))
                onRequestCreate?.Invoke();

            if (GUILayout.Button("Refresh", GUILayout.Width(70f)))
                Refresh();
        }

        EditorGUILayout.Space(2f);
        _search = EditorGUILayout.TextField("Search", _search);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < _actions.Count; i++)
        {
            ActionDefinition action = _actions[i];
            if (action == null || !PassesFilter(action))
                continue;

            bool selected = action == current;
            GUIStyle style = selected ? EditorStyles.selectionRect : EditorStyles.label;
            string label = string.IsNullOrEmpty(action.DisplayName) ? action.name : action.DisplayName;
            if (GUILayout.Toggle(selected, label, style))
                current = action;
        }

        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
        return current;
    }

    bool PassesFilter(ActionDefinition action)
    {
        if (string.IsNullOrEmpty(_search))
            return true;

        string key = _search.Trim();
        return action.DisplayName.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0
            || action.Id.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0
            || action.name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
