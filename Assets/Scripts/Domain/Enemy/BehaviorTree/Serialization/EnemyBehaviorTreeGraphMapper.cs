using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>嵌套 NodeDef ↔ 扁平图记录；供 GraphView 编辑后写回运行真源。</summary>
public static class EnemyBehaviorTreeGraphMapper
{
    /// <summary>扁平节点记录：父子 guid + 同级序 + 节点定义引用。</summary>
    [Serializable]
    public sealed class FlatRecord
    {
        public string guid;
        public string parentGuid;
        public int siblingIndex;
        [SerializeReference] public EnemyBehaviorNodeDef node;
    }

    /// <summary>为树补齐稳定 Guid 与空调试名（短类型名）；遇环则中止该分支。</summary>
    public static void EnsureStableIds(EnemyBehaviorNodeDef root)
    {
        if (root == null)
            return;

        var seenGuids = new HashSet<string>();
        var visiting = new HashSet<EnemyBehaviorNodeDef>();
        WalkEnsure(root, seenGuids, visiting);
    }

    /// <summary>深度优先扁平化；会先 EnsureStableIds。</summary>
    public static List<FlatRecord> Flatten(EnemyBehaviorNodeDef root)
    {
        var records = new List<FlatRecord>();
        if (root == null)
            return records;

        EnsureStableIds(root);
        FlattenVisit(root, parentGuid: null, siblingIndex: 0, records, new HashSet<EnemyBehaviorNodeDef>());
        return records;
    }

    /// <summary>按 parentGuid / siblingIndex 重建嵌套树（写回 children / child）。</summary>
    public static EnemyBehaviorNodeDef Rebuild(IReadOnlyList<FlatRecord> records)
    {
        if (records == null || records.Count == 0)
            return null;

        FlatRecord rootRecord = null;
        var byParent = new Dictionary<string, List<FlatRecord>>();

        for (int i = 0; i < records.Count; i++)
        {
            FlatRecord record = records[i];
            if (record?.node == null || string.IsNullOrEmpty(record.guid))
                continue;

            if (string.IsNullOrEmpty(record.parentGuid))
            {
                if (rootRecord == null)
                    rootRecord = record;
                continue;
            }

            if (!byParent.TryGetValue(record.parentGuid, out List<FlatRecord> list))
            {
                list = new List<FlatRecord>();
                byParent[record.parentGuid] = list;
            }

            list.Add(record);
        }

        if (rootRecord == null)
            return null;

        foreach (KeyValuePair<string, List<FlatRecord>> pair in byParent)
            pair.Value.Sort((a, b) => a.siblingIndex.CompareTo(b.siblingIndex));

        WireChildren(rootRecord.node, rootRecord.guid, byParent);
        return rootRecord.node;
    }

    /// <summary>收集树中全部 NodeGuid。</summary>
    public static HashSet<string> CollectGuids(EnemyBehaviorNodeDef root)
    {
        var set = new HashSet<string>();
        if (root == null)
            return set;

        WalkGuids(root, set, new HashSet<EnemyBehaviorNodeDef>());
        return set;
    }

    /// <summary>为缺失布局条目生成自上而下树坐标（UE 风格），并剪除孤儿。</summary>
    public static void SyncLayout(EnemyBehaviorGraphLayout layout, EnemyBehaviorNodeDef root)
    {
        if (layout == null || root == null)
            return;

        EnsureStableIds(root);
        List<FlatRecord> flat = Flatten(root);
        HashSet<string> live = CollectGuids(root);
        layout.PruneOrphans(live);

        Dictionary<string, Vector2> computed = ComputeTopDownPositions(flat);
        foreach (KeyValuePair<string, Vector2> pair in computed)
        {
            if (!layout.TryGetNode(pair.Key, out _))
                layout.SetNode(pair.Key, pair.Value);
        }
    }

    /// <summary>
    /// 计算 UE 风格自上而下坐标：根在上，子在下，同级从左到右。
    /// </summary>
    public static Dictionary<string, Vector2> ComputeTopDownPositions(IReadOnlyList<FlatRecord> flat)
    {
        var result = new Dictionary<string, Vector2>();
        if (flat == null || flat.Count == 0)
            return result;

        var byParent = new Dictionary<string, List<FlatRecord>>();
        FlatRecord root = null;
        for (int i = 0; i < flat.Count; i++)
        {
            FlatRecord record = flat[i];
            if (record == null || string.IsNullOrEmpty(record.guid))
                continue;

            if (string.IsNullOrEmpty(record.parentGuid))
            {
                if (root == null)
                    root = record;
                continue;
            }

            if (!byParent.TryGetValue(record.parentGuid, out List<FlatRecord> list))
            {
                list = new List<FlatRecord>();
                byParent[record.parentGuid] = list;
            }

            list.Add(record);
        }

        if (root == null)
            return result;

        foreach (KeyValuePair<string, List<FlatRecord>> pair in byParent)
            pair.Value.Sort((a, b) => a.siblingIndex.CompareTo(b.siblingIndex));

        const float xGap = 200f;
        const float yGap = 120f;
        const float originX = 80f;
        const float originY = 40f;
        int leafCursor = 0;

        float Place(string guid, int depth)
        {
            byParent.TryGetValue(guid, out List<FlatRecord> children);
            if (children == null || children.Count == 0)
            {
                float x = originX + leafCursor * xGap;
                leafCursor++;
                result[guid] = new Vector2(x, originY + depth * yGap);
                return x;
            }

            float sum = 0f;
            for (int i = 0; i < children.Count; i++)
                sum += Place(children[i].guid, depth + 1);

            float centerX = sum / children.Count;
            result[guid] = new Vector2(centerX, originY + depth * yGap);
            return centerX;
        }

        Place(root.guid, 0);
        return result;
    }

    /// <summary>类型短名，作为未命名节点的默认 NodeName。</summary>
    public static string DefaultNodeName(EnemyBehaviorNodeDef def)
    {
        if (def == null)
            return "Node";

        string name = def.GetType().Name;
        if (name.EndsWith("ConditionDef", StringComparison.Ordinal))
            return name.Substring(0, name.Length - "ConditionDef".Length);
        if (name.EndsWith("ActionDef", StringComparison.Ordinal))
            return name.Substring(0, name.Length - "ActionDef".Length);
        if (name.EndsWith("NodeDef", StringComparison.Ordinal))
            return name.Substring(0, name.Length - "NodeDef".Length);
        if (name.EndsWith("Def", StringComparison.Ordinal))
            return name.Substring(0, name.Length - "Def".Length);
        return name;
    }

    static void WalkEnsure(
        EnemyBehaviorNodeDef node,
        HashSet<string> seenGuids,
        HashSet<EnemyBehaviorNodeDef> visiting)
    {
        if (node == null || !visiting.Add(node))
            return;

        if (string.IsNullOrEmpty(node.NodeGuid) || !seenGuids.Add(node.NodeGuid))
        {
            // 空或冲突 guid：重新分配
            string guid;
            do
                guid = Guid.NewGuid().ToString("N");
            while (!seenGuids.Add(guid));
            node.NodeGuid = guid;
        }

        if (string.IsNullOrEmpty(node.NodeName))
            node.NodeName = DefaultNodeName(node);

        if (TryGetChildren(node, out List<EnemyBehaviorNodeDef> children))
        {
            for (int i = 0; i < children.Count; i++)
                WalkEnsure(children[i], seenGuids, visiting);
            visiting.Remove(node);
            return;
        }

        if (TryGetSingleChild(node, out EnemyBehaviorNodeDef child))
            WalkEnsure(child, seenGuids, visiting);

        visiting.Remove(node);
    }

    static void FlattenVisit(
        EnemyBehaviorNodeDef node,
        string parentGuid,
        int siblingIndex,
        List<FlatRecord> records,
        HashSet<EnemyBehaviorNodeDef> visiting)
    {
        if (node == null || !visiting.Add(node))
            return;

        records.Add(new FlatRecord
        {
            guid = node.NodeGuid,
            parentGuid = parentGuid ?? string.Empty,
            siblingIndex = siblingIndex,
            node = node,
        });

        if (TryGetChildren(node, out List<EnemyBehaviorNodeDef> children))
        {
            for (int i = 0; i < children.Count; i++)
                FlattenVisit(children[i], node.NodeGuid, i, records, visiting);
            visiting.Remove(node);
            return;
        }

        if (TryGetSingleChild(node, out EnemyBehaviorNodeDef child) && child != null)
            FlattenVisit(child, node.NodeGuid, 0, records, visiting);

        visiting.Remove(node);
    }

    static void WireChildren(
        EnemyBehaviorNodeDef node,
        string guid,
        Dictionary<string, List<FlatRecord>> byParent)
    {
        if (node == null)
            return;

        byParent.TryGetValue(guid, out List<FlatRecord> kids);
        kids ??= new List<FlatRecord>();

        if (node is SelectorNodeDef selector)
        {
            selector.children = ToDefList(kids);
            for (int i = 0; i < kids.Count; i++)
                WireChildren(kids[i].node, kids[i].guid, byParent);
            return;
        }

        if (node is SequenceNodeDef sequence)
        {
            sequence.children = ToDefList(kids);
            for (int i = 0; i < kids.Count; i++)
                WireChildren(kids[i].node, kids[i].guid, byParent);
            return;
        }

        if (node is InverterNodeDef inverter)
        {
            inverter.child = kids.Count > 0 ? kids[0].node : null;
            if (inverter.child != null)
                WireChildren(inverter.child, kids[0].guid, byParent);
            return;
        }

        if (node is SucceederNodeDef succeeder)
        {
            succeeder.child = kids.Count > 0 ? kids[0].node : null;
            if (succeeder.child != null)
                WireChildren(succeeder.child, kids[0].guid, byParent);
            return;
        }

        if (node is CooldownGateNodeDef gate)
        {
            gate.child = kids.Count > 0 ? kids[0].node : null;
            if (gate.child != null)
                WireChildren(gate.child, kids[0].guid, byParent);
            return;
        }

        if (node is AggroGateNodeDef aggro)
        {
            aggro.child = kids.Count > 0 ? kids[0].node : null;
            if (aggro.child != null)
                WireChildren(aggro.child, kids[0].guid, byParent);
            return;
        }

        if (node is EnemyBehaviorConditionNodeDef condition)
        {
            condition.child = kids.Count > 0 ? kids[0].node : null;
            if (condition.child != null)
                WireChildren(condition.child, kids[0].guid, byParent);
        }
    }

    static List<EnemyBehaviorNodeDef> ToDefList(List<FlatRecord> kids)
    {
        var list = new List<EnemyBehaviorNodeDef>(kids.Count);
        for (int i = 0; i < kids.Count; i++)
            list.Add(kids[i].node);
        return list;
    }

    static void WalkGuids(
        EnemyBehaviorNodeDef node,
        HashSet<string> set,
        HashSet<EnemyBehaviorNodeDef> visiting)
    {
        if (node == null || !visiting.Add(node))
            return;
        if (!string.IsNullOrEmpty(node.NodeGuid))
            set.Add(node.NodeGuid);

        if (TryGetChildren(node, out List<EnemyBehaviorNodeDef> children))
        {
            for (int i = 0; i < children.Count; i++)
                WalkGuids(children[i], set, visiting);
            visiting.Remove(node);
            return;
        }

        if (TryGetSingleChild(node, out EnemyBehaviorNodeDef child))
            WalkGuids(child, set, visiting);

        visiting.Remove(node);
    }

    /// <summary>读取复合节点子列表（可写同一引用）。</summary>
    public static bool TryGetChildren(EnemyBehaviorNodeDef node, out List<EnemyBehaviorNodeDef> children)
    {
        switch (node)
        {
            case SelectorNodeDef selector:
                children = selector.children ??= new List<EnemyBehaviorNodeDef>();
                return true;
            case SequenceNodeDef sequence:
                children = sequence.children ??= new List<EnemyBehaviorNodeDef>();
                return true;
            default:
                children = null;
                return false;
        }
    }

    /// <summary>读取单子装饰节点（含 UE 风格条件装饰）。</summary>
    public static bool TryGetSingleChild(EnemyBehaviorNodeDef node, out EnemyBehaviorNodeDef child)
    {
        switch (node)
        {
            case InverterNodeDef inverter:
                child = inverter.child;
                return true;
            case SucceederNodeDef succeeder:
                child = succeeder.child;
                return true;
            case CooldownGateNodeDef gate:
                child = gate.child;
                return true;
            case AggroGateNodeDef aggro:
                child = aggro.child;
                return true;
            case EnemyBehaviorConditionNodeDef condition:
                child = condition.child;
                return true;
            default:
                child = null;
                return false;
        }
    }
}
