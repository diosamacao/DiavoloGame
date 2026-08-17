/// <summary>房间调试 HUD 只读一行；由 Host/Client 每帧刷新。</summary>
public readonly struct ReplicationRoomHudInfo
{
    /// <summary>创建 HUD 快照。</summary>
    public ReplicationRoomHudInfo(
        bool active,
        ReplicationRole role,
        string status,
        long authorityFrame,
        int rttMs,
        int healthMilli,
        int tickBytes,
        int commandBytes,
        int proxyCount,
        int predictionPendingCount)
    {
        Active = active;
        Role = role;
        Status = status ?? string.Empty;
        AuthorityFrame = authorityFrame;
        RttMs = rttMs;
        HealthMilli = healthMilli;
        TickBytes = tickBytes;
        CommandBytes = commandBytes;
        ProxyCount = proxyCount;
        PredictionPendingCount = predictionPendingCount;
    }

    /// <summary>房间控制器是否已启动。</summary>
    public bool Active { get; }

    /// <summary>本机角色。</summary>
    public ReplicationRole Role { get; }

    /// <summary>Listening / Joined / Rejected 等短状态。</summary>
    public string Status { get; }

    /// <summary>最近权威帧；客机为最近成功应用的 ReplicationFrame.Tick。</summary>
    public long AuthorityFrame { get; }

    /// <summary>客机 RTT 毫秒；Host 无对端时为 -1。</summary>
    public int RttMs { get; }

    /// <summary>本机最近生命毫值；未知为 -1。</summary>
    public int HealthMilli { get; }

    /// <summary>最近一包完整 ReplicationFrame 房间载荷字节数；未知为 -1。</summary>
    public int TickBytes { get; }

    /// <summary>最近一包完整 ClientCommandBatch 房间载荷字节数；未知为 -1。</summary>
    public int CommandBytes { get; }

    /// <summary>Client 当前 RemoteCharacterProxy 数量；Host 为 -1。</summary>
    public int ProxyCount { get; }

    /// <summary>Client 当前 Locomotion 与 Action 待确认记录总数；Host 为 -1。</summary>
    public int PredictionPendingCount { get; }
}
