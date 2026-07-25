using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Graph Editor 顺序节点组；子节点按数组顺序自动生成普通 Cancel 链，
/// 每个子节点仍保留独立输入，PerfectCancel 作为组级特殊派生出口。
/// </summary>
[Serializable]
public sealed class ActionGraphNodeGroup
{
    [SerializeField] string groupId = string.Empty;
    [SerializeField] string displayName = "Action Group";
    [SerializeField] string[] childNodeIds = Array.Empty<string>();
    [SerializeField] Vector2 editorPosition = Vector2.zero;

    /// <summary>图内唯一组 Id。</summary>
    public string GroupId => groupId;

    /// <summary>组节点显示名。</summary>
    public string DisplayName => string.IsNullOrEmpty(displayName) ? groupId : displayName;

    /// <summary>按普通 Cancel 自动衔接的有序子节点。</summary>
    public IReadOnlyList<string> ChildNodeIds => childNodeIds ?? Array.Empty<string>();

    /// <summary>折叠组节点的编辑器坐标。</summary>
    public Vector2 EditorPosition => editorPosition;
}
