using System;
using System.Collections.Generic;

/// <summary>房间身份与出生分配：PlayerId/Entity/Team/Spawn/Archetype，不碰套接字或 Host Root。</summary>
public sealed class MatchCoordinator
{
    const int DefaultTeamId = 1;
    const int SpawnStrideMm = 2000;

    readonly Dictionary<NetConnectionId, MatchPlayerSlot> _slots = new();
    readonly int _maxPlayers;
    readonly NetArchetypeId _playerArchetypeId;
    int _nextEntityValue = 1;

    /// <summary>创建指定容量与默认玩家原型的 Match。</summary>
    public MatchCoordinator(int maxPlayers, NetArchetypeId playerArchetypeId)
    {
        if (maxPlayers < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPlayers));
        _maxPlayers = maxPlayers;
        _playerArchetypeId = playerArchetypeId;
    }

    /// <summary>当前已接纳人数。</summary>
    public int PlayerCount => _slots.Count;

    /// <summary>房间远端容量。</summary>
    public int MaxPlayers => _maxPlayers;

    /// <summary>为已通过 Session 校验的请求分配槽位；满员返回 false。</summary>
    public bool TryAccept(in SessionPlayerRequest request, out MatchPlayerSlot slot)
    {
        slot = default;
        if (!request.ConnectionId.IsValid || !request.PlayerId.IsValid)
            return false;
        if (_slots.ContainsKey(request.ConnectionId))
            return false;
        if (_slots.Count >= _maxPlayers)
            return false;

        int spawnIndex = _slots.Count;
        var spawn = new MatchSpawnPose(
            spawnIndex * SpawnStrideMm,
            0,
            0,
            facingMilliDeg: 0);
        slot = new MatchPlayerSlot(
            request.ConnectionId,
            request.PlayerId,
            new NetEntityId(_nextEntityValue++),
            DefaultTeamId,
            in spawn,
            _playerArchetypeId);
        _slots.Add(request.ConnectionId, slot);
        return true;
    }

    /// <summary>按连接读取已接纳槽位。</summary>
    public bool TryGet(NetConnectionId connectionId, out MatchPlayerSlot slot) =>
        _slots.TryGetValue(connectionId, out slot);

    /// <summary>连接离开时释放槽位；不影响其余玩家。</summary>
    public bool Release(NetConnectionId connectionId) => _slots.Remove(connectionId);
}
