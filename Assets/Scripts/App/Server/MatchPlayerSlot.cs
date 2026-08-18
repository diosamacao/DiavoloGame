/// <summary>Match 已接纳玩家的稳定槽位：身份、队伍、出生与原型。</summary>
public readonly struct MatchPlayerSlot
{
    /// <summary>创建已分配槽位。</summary>
    public MatchPlayerSlot(
        NetConnectionId connectionId,
        NetPlayerId playerId,
        NetEntityId entityId,
        int teamId,
        in MatchSpawnPose spawn,
        NetArchetypeId archetypeId)
    {
        ConnectionId = connectionId;
        PlayerId = playerId;
        EntityId = entityId;
        TeamId = teamId;
        Spawn = spawn;
        ArchetypeId = archetypeId;
    }

    /// <summary>对应 Transport 连接。</summary>
    public NetConnectionId ConnectionId { get; }

    /// <summary>Session 预留的玩家 Id。</summary>
    public NetPlayerId PlayerId { get; }

    /// <summary>Match 分配的权威实体 Id，不依赖 Host Actor。</summary>
    public NetEntityId EntityId { get; }

    /// <summary>队伍编号；PVE 默认同一队。</summary>
    public int TeamId { get; }

    /// <summary>该槽出生位姿。</summary>
    public MatchSpawnPose Spawn { get; }

    /// <summary>角色网络原型。</summary>
    public NetArchetypeId ArchetypeId { get; }
}
