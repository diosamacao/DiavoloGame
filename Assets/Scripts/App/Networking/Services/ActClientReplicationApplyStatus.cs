/// <summary>客户端处理 V2 生命周期或快照后的房间决策。</summary>
public enum ActClientReplicationApplyStatus
{
    /// <summary>消息已应用。</summary>
    Applied = 0,
    /// <summary>快照正在等待生命周期屏障。</summary>
    Buffered = 1,
    /// <summary>协议冲突并已请求恢复。</summary>
    Rejected = 2,
    /// <summary>Owner 生命周期已结束。</summary>
    OwnerDespawned = 3,
}
