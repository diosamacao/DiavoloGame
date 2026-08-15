/// <summary>按外部时钟判断房间对端是否超过空闲超时；可单测。</summary>
public sealed class RoomIdleTracker
{
    readonly int _timeoutMs;
    long _lastActivityMs;
    bool _started;

    /// <summary>使用指定超时毫秒创建；非法值回退默认 10 秒。</summary>
    public RoomIdleTracker(int timeoutMs = ReplicationRoomProtocol.IdleTimeoutMs)
    {
        _timeoutMs = timeoutMs > 0 ? timeoutMs : ReplicationRoomProtocol.IdleTimeoutMs;
    }

    /// <summary>超时阈值（毫秒）。</summary>
    public int TimeoutMs => _timeoutMs;

    /// <summary>是否已收到过至少一次活动。</summary>
    public bool HasActivity => _started;

    /// <summary>记录一次收包；nowMs 由调用方提供。</summary>
    public void Touch(long nowMs)
    {
        _lastActivityMs = nowMs;
        _started = true;
    }

    /// <summary>已开始计时且超过超时则视为掉线；尚未 Touch 时不超时。</summary>
    public bool IsTimedOut(long nowMs) =>
        _started && nowMs - _lastActivityMs >= _timeoutMs;
}
