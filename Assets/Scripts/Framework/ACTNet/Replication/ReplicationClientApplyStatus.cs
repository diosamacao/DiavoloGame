/// <summary>描述 Client 对完整复制帧的接受、时序丢弃或验证拒绝状态。</summary>
public enum ReplicationClientApplyStatus
{
    Applied = 0,
    StaleSequence = 1,
    Rejected = 2,
}
