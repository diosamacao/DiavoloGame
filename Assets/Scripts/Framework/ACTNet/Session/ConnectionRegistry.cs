using System;
using System.Collections.Generic;

/// <summary>保存每条 Session 连接的玩家映射、Join 状态与最后活动时刻。</summary>
public sealed class ConnectionRegistry
{
    readonly Dictionary<NetConnectionId, Entry> _entries = new();

    /// <summary>当前已预留或完成 Join 的连接数。</summary>
    public int Count => _entries.Count;

    /// <summary>登记一条经过版本校验的连接。</summary>
    public void Add(NetConnectionId connectionId, NetPlayerId playerId, long nowMs)
    {
        if (!connectionId.IsValid)
            throw new ArgumentException("连接 Id 无效。", nameof(connectionId));
        if (!playerId.IsValid)
            throw new ArgumentException("玩家 Id 无效。", nameof(playerId));
        if (_entries.ContainsKey(connectionId))
            throw new InvalidOperationException($"连接已登记：{connectionId}。");

        _entries.Add(connectionId, new Entry(playerId, nowMs));
    }

    /// <summary>查询连接是否已登记。</summary>
    public bool Contains(NetConnectionId connectionId) => _entries.ContainsKey(connectionId);

    /// <summary>标记 Gameplay 已为连接完成实体分配。</summary>
    public void MarkJoined(NetConnectionId connectionId)
    {
        if (!_entries.TryGetValue(connectionId, out Entry entry))
            throw new InvalidOperationException($"连接未登记：{connectionId}。");
        entry.IsJoined = true;
    }

    /// <summary>查询连接是否已完成 Join。</summary>
    public bool IsJoined(NetConnectionId connectionId) =>
        _entries.TryGetValue(connectionId, out Entry entry) && entry.IsJoined;

    /// <summary>任意合法上行消息刷新连接活动时刻。</summary>
    public void Touch(NetConnectionId connectionId, long nowMs)
    {
        if (_entries.TryGetValue(connectionId, out Entry entry))
            entry.LastActivityMs = nowMs;
    }

    /// <summary>判断已登记连接是否达到空闲超时边界。</summary>
    public bool IsTimedOut(NetConnectionId connectionId, long nowMs, int timeoutMs) =>
        _entries.TryGetValue(connectionId, out Entry entry)
        && nowMs - entry.LastActivityMs >= timeoutMs;

    /// <summary>查询连接分配的玩家身份。</summary>
    public bool TryGetPlayer(NetConnectionId connectionId, out NetPlayerId playerId)
    {
        if (_entries.TryGetValue(connectionId, out Entry entry))
        {
            playerId = entry.PlayerId;
            return true;
        }

        playerId = NetPlayerId.Invalid;
        return false;
    }

    /// <summary>复制全部连接 Id，供超时扫描期间安全删除。</summary>
    public void CopyConnectionIds(List<NetConnectionId> destination)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));
        destination.Clear();
        foreach (NetConnectionId connectionId in _entries.Keys)
            destination.Add(connectionId);
    }

    /// <summary>移除连接并返回其玩家身份。</summary>
    public bool Remove(NetConnectionId connectionId, out NetPlayerId playerId)
    {
        if (!_entries.TryGetValue(connectionId, out Entry entry))
        {
            playerId = NetPlayerId.Invalid;
            return false;
        }

        playerId = entry.PlayerId;
        _entries.Remove(connectionId);
        return true;
    }

    sealed class Entry
    {
        public Entry(NetPlayerId playerId, long lastActivityMs)
        {
            PlayerId = playerId;
            LastActivityMs = lastActivityMs;
        }

        public NetPlayerId PlayerId { get; }
        public long LastActivityMs { get; set; }
        public bool IsJoined { get; set; }
    }
}
