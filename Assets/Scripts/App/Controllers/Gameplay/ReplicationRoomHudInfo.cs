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
        int healthMilli)
    {
        Active = active;
        Role = role;
        Status = status ?? string.Empty;
        AuthorityFrame = authorityFrame;
        RttMs = rttMs;
        HealthMilli = healthMilli;
    }

    /// <summary>房间控制器是否已启动。</summary>
    public bool Active { get; }

    /// <summary>本机角色。</summary>
    public ReplicationRole Role { get; }

    /// <summary>Listening / Joined / Rejected 等短状态。</summary>
    public string Status { get; }

    /// <summary>最近权威帧；客机为最近收到的 Tick。</summary>
    public long AuthorityFrame { get; }

    /// <summary>客机 RTT 毫秒；Host 无对端时为 -1。</summary>
    public int RttMs { get; }

    /// <summary>本机最近生命毫值；未知为 -1。</summary>
    public int HealthMilli { get; }
}
