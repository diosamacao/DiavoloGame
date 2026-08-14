using System.Collections.Generic;

/// <summary>
/// 同进程 ActionDefinition ↔ 复制 actionId。Loopback 共用一份即可；NS5 再换成烘焙稳定 Id。
/// </summary>
public sealed class ActionReplicationCatalog
{
    readonly Dictionary<ActionDefinition, int> _toId = new();
    readonly Dictionary<int, ActionDefinition> _fromId = new();
    int _nextId = 1;

    /// <summary>已登记的动作条数（不含 Id=0）。</summary>
    public int Count => _toId.Count;

    /// <summary>登记或复用动作 Id；null 返回 0（无活动动作）。</summary>
    public int GetOrAdd(ActionDefinition action)
    {
        if (action == null)
            return 0;

        if (_toId.TryGetValue(action, out int existing))
            return existing;

        int id = _nextId++;
        _toId[action] = id;
        _fromId[id] = action;
        return id;
    }

    /// <summary>按复制 Id 取回同进程动作资产；0 或未登记返回 false。</summary>
    public bool TryGet(int actionId, out ActionDefinition action)
    {
        if (actionId <= 0)
        {
            action = null;
            return false;
        }

        return _fromId.TryGetValue(actionId, out action);
    }
}
