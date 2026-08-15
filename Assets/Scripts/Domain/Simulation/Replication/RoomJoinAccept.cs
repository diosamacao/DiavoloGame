/// <summary>入房成功：分配客机身份并给出当前权威帧，供预测时钟起步。</summary>
public readonly struct RoomJoinAccept
{
    /// <summary>创建入房成功回包。</summary>
    public RoomJoinAccept(
        int assignedPlayerId,
        int assignedActorId,
        int hostActorId,
        int contentVersion,
        long authorityFrame)
    {
        AssignedPlayerId = assignedPlayerId;
        AssignedActorId = assignedActorId;
        HostActorId = hostActorId;
        ContentVersion = contentVersion;
        AuthorityFrame = authorityFrame;
    }

    /// <summary>房间内玩家编号，写入 ClientCommand.SenderPlayerId。</summary>
    public int AssignedPlayerId { get; }

    /// <summary>客机在权威世界的 SimActorId.Value。</summary>
    public int AssignedActorId { get; }

    /// <summary>Host 本地玩家 SimActorId.Value，供客机认队友。</summary>
    public int HostActorId { get; }

    /// <summary>权威当时的内容版本。</summary>
    public int ContentVersion { get; }

    /// <summary>握手时权威已完成的逻辑帧。</summary>
    public long AuthorityFrame { get; }
}
