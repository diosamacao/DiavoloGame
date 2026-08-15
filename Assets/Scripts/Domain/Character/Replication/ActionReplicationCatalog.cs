using System;
using System.Collections.Generic;

/// <summary>
/// ActionDefinition ↔ 复制 actionId。Id 由资产名稳定哈希，跨进程同名同 Id。
/// </summary>
public sealed class ActionReplicationCatalog
{
    readonly Dictionary<ActionDefinition, int> _toId = new();
    readonly Dictionary<int, ActionDefinition> _fromId = new();

    /// <summary>已登记的动作条数（不含 Id=0）。</summary>
    public int Count => _toId.Count;

    /// <summary>按角色配置预填 Graph 节点与受击反应，保证 Host/Client 同名同 Id。</summary>
    public void Prefill(CharacterConfig config)
    {
        if (config == null)
            return;

        var actions = new List<ActionDefinition>();
        CombatModeProfile profile = config.CombatProfile;
        if (profile != null)
        {
            IReadOnlyList<CombatModeEntry> entries = profile.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                ActionGraph graph = entries[i].ActionGraph;
                if (graph == null)
                    continue;

                IReadOnlyList<ActionGraphNode> nodes = graph.Nodes;
                for (int n = 0; n < nodes.Count; n++)
                {
                    ActionDefinition action = nodes[n] != null ? nodes[n].Action : null;
                    if (action != null)
                        actions.Add(action);
                }
            }
        }

        config.Combat.Reactions?.CollectActions(actions);
        Prefill(actions);
    }

    /// <summary>按名称排序后登记，使哈希碰撞探测在两端顺序一致。</summary>
    public void Prefill(IEnumerable<ActionDefinition> actions)
    {
        if (actions == null)
            return;

        var unique = new List<ActionDefinition>();
        var seen = new HashSet<ActionDefinition>();
        foreach (ActionDefinition action in actions)
        {
            if (action == null || !seen.Add(action))
                continue;
            unique.Add(action);
        }

        unique.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        for (int i = 0; i < unique.Count; i++)
            GetOrAdd(unique[i]);
    }

    /// <summary>登记或复用动作 Id；null 返回 0。同名资产跨实例映射到同一 Id。</summary>
    public int GetOrAdd(ActionDefinition action)
    {
        if (action == null)
            return 0;

        if (_toId.TryGetValue(action, out int existing))
            return existing;

        int id = ComputeStableId(action.name);
        while (_fromId.TryGetValue(id, out ActionDefinition mapped)
               && mapped != action
               && !string.Equals(mapped.name, action.name, StringComparison.Ordinal))
        {
            id = id == int.MaxValue ? 1 : id + 1;
        }

        _toId[action] = id;
        _fromId[id] = action;
        return id;
    }

    /// <summary>按复制 Id 取回本进程动作资产；0 或未登记返回 false。</summary>
    public bool TryGet(int actionId, out ActionDefinition action)
    {
        if (actionId <= 0)
        {
            action = null;
            return false;
        }

        return _fromId.TryGetValue(actionId, out action);
    }

    /// <summary>资产名 FNV-1a；空名回退类型名，避免 CreateInstance 得到 0。</summary>
    public static int ComputeStableId(string actionName)
    {
        if (string.IsNullOrEmpty(actionName))
            actionName = nameof(ActionDefinition);

        unchecked
        {
            int hash = (int)2166136261u;
            for (int i = 0; i < actionName.Length; i++)
                hash = (hash ^ actionName[i]) * 16777619;
            if (hash == int.MinValue)
                hash = 1;
            int id = Math.Abs(hash);
            return id == 0 ? 1 : id;
        }
    }
}
