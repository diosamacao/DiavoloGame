using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>左侧 ActionDefinition 列表：按所在文件夹分组折叠，支持搜索与创建入口。</summary>
public sealed class ActionListPanel
{
    const string ActionsRoot = "Assets/Data/Combat/Actions";

    string _search = string.Empty;
    Vector2 _scroll;
    readonly List<ActionDefinition> _actions = new();
    readonly List<FolderGroup> _groups = new();
    readonly Dictionary<string, bool> _folderExpanded = new();

    /// <summary>单个文件夹下的 Action 分组。</summary>
    sealed class FolderGroup
    {
        public string FolderPath;
        public string DisplayName;
        public readonly List<ActionDefinition> Actions = new();
    }

    /// <summary>刷新项目中全部 ActionDefinition 资产，并按父文件夹重建分组。</summary>
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

        RebuildGroups();
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

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("▾", "全部展开"), EditorStyles.miniButton, GUILayout.Width(22f)))
                SetAllExpanded(true);
            if (GUILayout.Button(new GUIContent("▸", "全部折叠"), EditorStyles.miniButton, GUILayout.Width(22f)))
                SetAllExpanded(false);
        }

        EditorGUILayout.Space(2f);
        _search = EditorGUILayout.TextField("Search", _search);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int g = 0; g < _groups.Count; g++)
        {
            FolderGroup group = _groups[g];
            int visibleCount = CountVisible(group);
            if (visibleCount == 0)
                continue;

            bool forceExpand = !string.IsNullOrWhiteSpace(_search);
            bool expanded = forceExpand || IsExpanded(group.FolderPath);
            string header = $"{group.DisplayName} ({visibleCount})";

            EditorGUI.BeginChangeCheck();
            expanded = EditorGUILayout.Foldout(expanded, header, true);
            if (EditorGUI.EndChangeCheck() && !forceExpand)
                _folderExpanded[group.FolderPath] = expanded;

            if (!expanded && !forceExpand)
                continue;

            for (int i = 0; i < group.Actions.Count; i++)
            {
                ActionDefinition action = group.Actions[i];
                if (action == null || !PassesFilter(action))
                    continue;

                bool selected = action == current;
                // 缩进表示隶属于当前文件夹分组。
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(12f);
                    GUIStyle style = selected ? EditorStyles.selectionRect : EditorStyles.label;
                    if (GUILayout.Toggle(selected, action.name, style))
                        current = action;
                }
            }
        }

        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
        return current;
    }

    void RebuildGroups()
    {
        _groups.Clear();
        var map = new Dictionary<string, FolderGroup>();

        for (int i = 0; i < _actions.Count; i++)
        {
            ActionDefinition action = _actions[i];
            if (action == null)
                continue;

            string assetPath = AssetDatabase.GetAssetPath(action);
            string folder = string.IsNullOrEmpty(assetPath)
                ? "(Unknown)"
                : Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? "(Unknown)";

            if (!map.TryGetValue(folder, out FolderGroup group))
            {
                group = new FolderGroup
                {
                    FolderPath = folder,
                    DisplayName = FormatFolderDisplayName(folder),
                };
                map.Add(folder, group);
                _groups.Add(group);
                if (!_folderExpanded.ContainsKey(folder))
                    _folderExpanded[folder] = true;
            }

            group.Actions.Add(action);
        }

        _groups.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
        for (int i = 0; i < _groups.Count; i++)
            _groups[i].Actions.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
    }

    /// <summary>优先显示相对 Actions 根的路径，便于按角色/子目录查找。</summary>
    static string FormatFolderDisplayName(string folder)
    {
        if (string.IsNullOrEmpty(folder))
            return "(Unknown)";

        string normalized = folder.Replace('\\', '/');
        if (normalized.StartsWith(ActionsRoot + "/"))
            return normalized.Substring(ActionsRoot.Length + 1);

        if (normalized.StartsWith("Assets/"))
            return normalized.Substring("Assets/".Length);

        return normalized;
    }

    bool IsExpanded(string folderPath) =>
        !_folderExpanded.TryGetValue(folderPath, out bool expanded) || expanded;

    void SetAllExpanded(bool expanded)
    {
        for (int i = 0; i < _groups.Count; i++)
            _folderExpanded[_groups[i].FolderPath] = expanded;
    }

    int CountVisible(FolderGroup group)
    {
        int count = 0;
        for (int i = 0; i < group.Actions.Count; i++)
        {
            if (group.Actions[i] != null && PassesFilter(group.Actions[i]))
                count++;
        }

        return count;
    }

    bool PassesFilter(ActionDefinition action)
    {
        if (string.IsNullOrEmpty(_search))
            return true;

        string key = _search.Trim();
        if (action.name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        // 搜索也匹配文件夹显示名，方便按角色目录过滤。
        string path = AssetDatabase.GetAssetPath(action);
        return !string.IsNullOrEmpty(path)
            && path.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
