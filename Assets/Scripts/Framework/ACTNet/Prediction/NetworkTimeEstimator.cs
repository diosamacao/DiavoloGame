/// <summary>用 RTT / jitter / 权威 Tick 估计插值延迟与 Tick 偏移；不含 ACT 字段。</summary>
public sealed class NetworkTimeEstimator
{
    const int DefaultLogicHz = 60;
    const int MinDelayMs = 16;
    const int MaxDelayMs = 150;

    int _rttMs = -1;
    int _jitterMs = -1;

    /// <summary>最近 RTT；未知为 -1。</summary>
    public int RttMs => _rttMs;

    /// <summary>RFC3550 风格 jitter；未知为 -1。</summary>
    public int JitterMs => _jitterMs;

    /// <summary>权威 Tick 相对本地估计 Tick 的偏移；尚未观测为 0。</summary>
    public long TickOffset { get; private set; }

    /// <summary>估计的服务器时间偏移（本地 now - RTT/2）；未知为 0。</summary>
    public long ServerTimeOffsetMs { get; private set; }

    /// <summary>写入一次心跳 RTT，并更新 jitter。</summary>
    public void ObserveRtt(int rttMs)
    {
        if (rttMs < 0 || rttMs == _rttMs)
            return;

        if (_rttMs < 0)
        {
            _rttMs = rttMs;
            _jitterMs = 0;
            return;
        }

        int delta = rttMs - _rttMs;
        if (delta < 0)
            delta = -delta;
        _jitterMs = _jitterMs + ((delta - _jitterMs) >> 4);
        _rttMs = rttMs;
    }

    /// <summary>用权威 Tick 与本地时钟估计 Tick 偏移；logicHz 非法时按 60。</summary>
    public void ObserveAuthorityTick(long localNowMs, long authorityTick, int logicHz = DefaultLogicHz)
    {
        int hz = logicHz > 0 ? logicHz : DefaultLogicHz;
        if (localNowMs < 0 || authorityTick < 0)
            return;

        long localTick = localNowMs * hz / 1000L;
        TickOffset = authorityTick - localTick;
        if (_rttMs >= 0)
            ServerTimeOffsetMs = -(_rttMs / 2);
    }

    /// <summary>插值延迟毫秒：RTT/2 + jitter + 一格，钳在 16～150。</summary>
    public int InterpolationDelayMs
    {
        get
        {
            int oneWay = _rttMs > 0 ? _rttMs / 2 : 0;
            int jitter = _jitterMs > 0 ? _jitterMs : 0;
            int delay = oneWay + jitter + (1000 / DefaultLogicHz);
            if (delay < MinDelayMs)
                return MinDelayMs;
            return delay > MaxDelayMs ? MaxDelayMs : delay;
        }
    }

    /// <summary>插值延迟换算成逻辑 Tick，至少 1。</summary>
    public int InterpolationDelayTicks
    {
        get
        {
            int ticks = (InterpolationDelayMs * DefaultLogicHz + 999) / 1000;
            return ticks < 1 ? 1 : ticks;
        }
    }
}
