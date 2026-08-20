/// <summary>调用层声明的消息交付语义；由 ChannelMuxTransport 执行可靠/丢旧，底层 UDP 仍是数据报。</summary>
public enum NetChannel : byte
{
    /// <summary>旧 UDP 线格式未携带通道头，接收侧暂时无法还原发送语义。</summary>
    Unspecified = 0,

    /// <summary>Join、Accept、Kick、Ready 等可靠有序控制流。</summary>
    ControlReliableOrdered = 1,

    /// <summary>最近输入帧冗余发送的不可靠命令流。</summary>
    CommandUnreliableRedundant = 2,

    /// <summary>只保留最新状态的不可靠时序快照流。</summary>
    SnapshotUnreliableSequenced = 3,

    /// <summary>不可由状态恢复的可靠有序事件流。</summary>
    EventReliableOrdered = 4,
}
