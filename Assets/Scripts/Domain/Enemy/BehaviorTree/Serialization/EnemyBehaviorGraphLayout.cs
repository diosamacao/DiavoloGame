using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>行为树 Graph 布局元数据；丢失不影响 customRoot 逻辑树。</summary>
[Serializable]
public sealed class EnemyBehaviorGraphLayout
{
    [SerializeField] List<EnemyBehaviorGraphNodeLayout> nodes = new List<EnemyBehaviorGraphNodeLayout>();
    [SerializeField] List<EnemyBehaviorGraphStickyNote> stickyNotes = new List<EnemyBehaviorGraphStickyNote>();

    /// <summary>节点坐标与折叠（按 NodeGuid 索引）。</summary>
    public List<EnemyBehaviorGraphNodeLayout> Nodes =>
        nodes ??= new List<EnemyBehaviorGraphNodeLayout>();

    /// <summary>画布注释（不参与运行）。</summary>
    public List<EnemyBehaviorGraphStickyNote> StickyNotes =>
        stickyNotes ??= new List<EnemyBehaviorGraphStickyNote>();

    /// <summary>按 Guid 查找布局；没有则返回 false。</summary>
    public bool TryGetNode(string nodeGuid, out EnemyBehaviorGraphNodeLayout layout)
    {
        layout = null;
        if (string.IsNullOrEmpty(nodeGuid) || nodes == null)
            return false;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null && nodes[i].nodeGuid == nodeGuid)
            {
                layout = nodes[i];
                return true;
            }
        }

        return false;
    }

    /// <summary>写入或更新节点布局。</summary>
    public void SetNode(string nodeGuid, Vector2 position, bool collapsed = false)
    {
        if (string.IsNullOrEmpty(nodeGuid))
            return;

        if (TryGetNode(nodeGuid, out EnemyBehaviorGraphNodeLayout existing))
        {
            existing.position = position;
            existing.collapsed = collapsed;
            return;
        }

        Nodes.Add(new EnemyBehaviorGraphNodeLayout
        {
            nodeGuid = nodeGuid,
            position = position,
            collapsed = collapsed,
        });
    }

    /// <summary>移除树中已不存在的布局条目（孤儿 guid）。</summary>
    public int PruneOrphans(HashSet<string> liveGuids)
    {
        if (nodes == null || nodes.Count == 0)
            return 0;

        int removed = nodes.RemoveAll(
            n => n == null || string.IsNullOrEmpty(n.nodeGuid) || liveGuids == null || !liveGuids.Contains(n.nodeGuid));
        return removed;
    }
}

/// <summary>单个节点在 Graph 画布上的布局。</summary>
[Serializable]
public sealed class EnemyBehaviorGraphNodeLayout
{
    public string nodeGuid;
    public Vector2 position;
    public bool collapsed;
}

/// <summary>Graph 画布便签（仅编辑器）。</summary>
[Serializable]
public sealed class EnemyBehaviorGraphStickyNote
{
    public string text;
    public Vector2 position;
    public Vector2 size = new Vector2(180f, 80f);
}
