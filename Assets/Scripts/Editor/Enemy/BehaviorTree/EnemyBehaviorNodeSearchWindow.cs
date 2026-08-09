using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

/// <summary>右键/空格调色板：按分组创建行为树节点。</summary>
public sealed class EnemyBehaviorNodeSearchWindow : ScriptableObject, ISearchWindowProvider
{
    EnemyBehaviorGraphView _graphView;
    Texture2D _indent;

    /// <summary>绑定目标画布。</summary>
    public void Initialize(EnemyBehaviorGraphView graphView)
    {
        _graphView = graphView;
        if (_indent == null)
        {
            _indent = new Texture2D(1, 1);
            _indent.SetPixel(0, 0, Color.clear);
            _indent.Apply();
        }
    }

    /// <inheritdoc />
    public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
    {
        var tree = new List<SearchTreeEntry>
        {
            new SearchTreeGroupEntry(new GUIContent("Create Node"), 0),
        };

        EnemyBehaviorNodeCatalog.Group? current = null;
        for (int i = 0; i < EnemyBehaviorNodeCatalog.All.Count; i++)
        {
            EnemyBehaviorNodeCatalog.Entry entry = EnemyBehaviorNodeCatalog.All[i];
            if (current != entry.Group)
            {
                current = entry.Group;
                tree.Add(new SearchTreeGroupEntry(
                    new GUIContent(EnemyBehaviorNodeCatalog.GroupLabel(entry.Group)),
                    1));
            }

            tree.Add(new SearchTreeEntry(new GUIContent(entry.DisplayName))
            {
                level = 2,
                userData = entry.DefType,
            });
        }

        return tree;
    }

    /// <inheritdoc />
    public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
    {
        if (_graphView == null || searchTreeEntry.userData is not System.Type defType)
            return false;

        Vector2 screen = context.screenMousePosition;
        _graphView.CreateNodeAtScreen(defType, screen);
        return true;
    }
}
