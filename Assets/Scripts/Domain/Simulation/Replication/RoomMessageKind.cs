/// <summary>房间信封消息类型；战斗 Tick/Command 仍走 ReplicationCodec 正文。</summary>
public enum RoomMessageKind : byte
{
    /// <summary>客机请求入房。</summary>
    JoinRequest = 1,

    /// <summary>权威同意入房并分配玩家/Actor。</summary>
    JoinAccept = 2,

    /// <summary>权威拒绝入房。</summary>
    JoinReject = 3,

    /// <summary>保活；权威原样回显 SendTimeMs 供客机算 RTT。</summary>
    Heartbeat = 4,

    /// <summary>正文为 ReplicationCodec 上行命令。</summary>
    ClientCommand = 5,

    /// <summary>正文为 appliedFrameHint + AuthorityTick 字节。</summary>
    AuthorityTick = 6,

    /// <summary>权威剔除或房间结束。</summary>
    Kick = 7,
}
