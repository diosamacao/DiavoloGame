/// <summary>Dedicated 权威世界契约：Join 建 Actor、灌命令、外部时钟步进、按连接构帧。</summary>
public interface IDedicatedAuthorityWorld : System.IDisposable
{
    /// <summary>按 Match 槽位创建 Headless Authority Actor；失败时调用方应 Reject。</summary>
    /// <param name="entityId">成功时为 World 分配的 SimulationId，必须写入 JoinAccept。</param>
    bool TryAcceptPlayer(in MatchPlayerSlot slot, out NetEntityId entityId);

    /// <summary>把该连接未应用命令合并进下一权威帧；下行 appliedHint 用本批第一条 Hint。</summary>
    void ApplyCommands(NetConnectionId connectionId, ClientCommand[] commands);

    /// <summary>只移除该连接的权威 Actor，不影响其他人。</summary>
    void RemovePlayer(NetConnectionId connectionId);

    /// <summary>用单调时间推进权威 World；步进内会排队本步 ReplicationFrame。</summary>
    void Advance(long nowMs);

    /// <summary>只读预览下一次 Advance 会步进几次；Listen 按此次数发命令，禁止按渲染帧预测。</summary>
    int PeekAdvanceSteps(long nowMs);

    /// <summary>权威追帧核插值比例；DriveFromExternalClock 时 SimulationHost 核不会自己走。</summary>
    float InterpolationAlpha { get; }

    /// <summary>为尚未下发过 Spawn 的新连接立刻编一帧；Join 同拍即可预测。</summary>
    void PublishImmediateReplication();

    /// <summary>取出本拍 AfterLogicStep 编好的下行正文；调用方负责发送后列表可复用。</summary>
    void DrainOutboundReplication(System.Collections.Generic.List<DedicatedReplicationSend> results);

    /// <summary>取出本拍权威命中事件；只含当前帧，禁止再带 W7 冗余窗口。</summary>
    void DrainOutboundEvents(System.Collections.Generic.List<DedicatedEventSend> results);

    /// <summary>最近完成的逻辑帧；尚未步进时为 -1。</summary>
    long CurrentFrame { get; }
}
