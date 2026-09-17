/// <summary>ACT 房间应用消息类型；V2 生命周期与快照分轨。</summary>
public enum ActRoomMessageType : byte
{
    /// <summary>客户端冗余命令批。</summary>
    ClientCommand = 5,
    /// <summary>可靠有序 ReplicationLifecycle。</summary>
    ReplicationLifecycle = 6,
    /// <summary>对局结束控制消息。</summary>
    MatchEnd = 8,
    /// <summary>可靠命中事件。</summary>
    ReplicationEvent = 9,
    /// <summary>客户端单次全量恢复请求。</summary>
    ReplicationRecover = 10,
    /// <summary>不可靠时序 ReplicationSnapshot。</summary>
    ReplicationSnapshot = 11,
}
