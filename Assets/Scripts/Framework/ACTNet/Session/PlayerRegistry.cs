using System;
using System.Collections.Generic;

/// <summary>为远端连接分配稳定且不重复的 Session PlayerId。</summary>
public sealed class PlayerRegistry
{
    readonly Dictionary<NetConnectionId, NetPlayerId> _playerByConnection = new();
    int _nextPlayerValue;

    /// <summary>创建指定玩家 Id 起点的注册表。</summary>
    public PlayerRegistry(int firstPlayerId)
    {
        if (firstPlayerId < 1)
            throw new ArgumentOutOfRangeException(nameof(firstPlayerId));
        _nextPlayerValue = firstPlayerId;
    }

    /// <summary>当前已分配玩家数量。</summary>
    public int Count => _playerByConnection.Count;

    /// <summary>为连接分配唯一玩家 Id；重复调用返回原分配。</summary>
    public NetPlayerId Reserve(NetConnectionId connectionId)
    {
        if (!connectionId.IsValid)
            throw new ArgumentException("连接 Id 无效。", nameof(connectionId));
        if (_playerByConnection.TryGetValue(connectionId, out NetPlayerId existing))
            return existing;

        var created = new NetPlayerId(_nextPlayerValue++);
        _playerByConnection.Add(connectionId, created);
        return created;
    }

    /// <summary>释放连接对应玩家身份。</summary>
    public bool Release(NetConnectionId connectionId, out NetPlayerId playerId)
    {
        if (!_playerByConnection.TryGetValue(connectionId, out playerId))
        {
            playerId = NetPlayerId.Invalid;
            return false;
        }

        _playerByConnection.Remove(connectionId);
        return true;
    }
}
