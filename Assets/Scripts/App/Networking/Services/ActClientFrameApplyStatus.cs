/// <summary>Client Gameplay 应用一帧复制数据后的房间决策。</summary>
public enum ActClientFrameApplyStatus
{
    /// <summary>帧已提交并执行 ACT 映射。</summary>
    Applied = 0,
    /// <summary>旧 Sequence 已安全丢弃。</summary>
    StaleSequence = 1,
    /// <summary>ReplicationClient 拒绝协议或实体操作。</summary>
    Rejected = 2,
    /// <summary>Owner 收到显式 Despawn，房间应结束。</summary>
    OwnerDespawned = 3,
}
