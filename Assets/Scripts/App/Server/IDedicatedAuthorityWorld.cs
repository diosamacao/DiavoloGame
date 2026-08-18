/// <summary>Dedicated 权威世界契约：Join 建 Actor、灌命令、外部时钟步进。</summary>
public interface IDedicatedAuthorityWorld : System.IDisposable
{
    /// <summary>按 Match 槽位创建 Headless Authority Actor；失败时调用方应 Reject。</summary>
    bool TryAcceptPlayer(in MatchPlayerSlot slot);

    /// <summary>把该连接的命令写入下一权威帧输入缓冲。</summary>
    void ApplyCommands(NetConnectionId connectionId, ClientCommand[] commands);

    /// <summary>只移除该连接的权威 Actor，不影响其他人。</summary>
    void RemovePlayer(NetConnectionId connectionId);

    /// <summary>用单调时间推进权威 World。</summary>
    void Advance(long nowMs);

    /// <summary>最近完成的逻辑帧；尚未步进时为 -1。</summary>
    long CurrentFrame { get; }
}
