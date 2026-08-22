using System;

/// <summary>远端快照时间线：按 Tick 丢旧；取样时 to 取第一份不低于目标的样本，禁止回滚到更旧状态。</summary>
public sealed class SnapshotTimeline<TState>
{
    readonly Entry[] _entries;
    int _count;

    /// <summary>创建固定容量的快照时间线。</summary>
    public SnapshotTimeline(int capacity = 16)
    {
        if (capacity < 2)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _entries = new Entry[capacity];
    }

    /// <summary>已缓存的快照条数。</summary>
    public int Count => _count;

    /// <summary>最新成功压入的 Tick；空为 -1。</summary>
    public long LatestTick => _count == 0 ? -1 : _entries[_count - 1].Tick;

    /// <summary>最早仍缓存的 Tick；空为 -1。</summary>
    public long FirstTick => _count == 0 ? -1 : _entries[0].Tick;

    /// <summary>压入更新的快照；旧 Tick 或重复 Tick 返回 false 且不改缓存。</summary>
    public bool TryPush(long tick, in TState state)
    {
        if (tick < 0)
            return false;
        if (_count > 0 && tick <= _entries[_count - 1].Tick)
            return false;

        if (_count == _entries.Length)
        {
            Array.Copy(_entries, 1, _entries, 0, _entries.Length - 1);
            _count--;
        }

        _entries[_count++] = new Entry(tick, state);
        return true;
    }

    /// <summary>
    /// 按延迟 Tick 取样。delayTicks=0 取最新。
    /// to 是第一份 Tick ≥ 目标的样本，以便隔步快照仍能算出 0～1 的 alpha。
    /// </summary>
    public bool TrySample(int delayTicks, out TState from, out TState to, out float alpha) =>
        TrySample(delayTicks, 0f, out _, out _, out from, out to, out alpha);

    /// <summary>取样并返回 bracketing Tick，供业务层决定是否提交新快照。</summary>
    public bool TrySample(
        int delayTicks,
        out long fromTick,
        out long toTick,
        out TState from,
        out TState to,
        out float alpha) =>
        TrySample(delayTicks, 0f, out fromTick, out toTick, out from, out to, out alpha);

    /// <summary>
    /// 按延迟与本机逻辑步内插值比例取样。目标 = latest - delay + interpolationAlpha。
    /// </summary>
    public bool TrySample(
        int delayTicks,
        float interpolationAlpha,
        out long fromTick,
        out long toTick,
        out TState from,
        out TState to,
        out float alpha)
    {
        fromTick = -1;
        toTick = -1;
        from = default;
        to = default;
        alpha = 0f;
        if (_count == 0)
            return false;

        int delay = delayTicks < 0 ? 0 : delayTicks;
        float frac = interpolationAlpha;
        if (frac < 0f)
            frac = 0f;
        if (frac > 1f)
            frac = 1f;

        double target = _entries[_count - 1].Tick - delay + frac;
        return TrySampleAt(target, out fromTick, out toTick, out from, out to, out alpha);
    }

    /// <summary>按绝对播放头取样；to 是第一份 Tick ≥ 目标的样本。</summary>
    public bool TrySampleAt(
        double targetTick,
        out long fromTick,
        out long toTick,
        out TState from,
        out TState to,
        out float alpha)
    {
        fromTick = -1;
        toTick = -1;
        from = default;
        to = default;
        alpha = 0f;
        if (_count == 0)
            return false;

        double target = targetTick;
        double firstTick = _entries[0].Tick;
        double latestTick = _entries[_count - 1].Tick;
        if (target < firstTick)
            target = firstTick;
        if (target > latestTick)
            target = latestTick;

        int toIndex = _count - 1;
        for (int i = 0; i < _count; i++)
        {
            if (_entries[i].Tick >= target)
            {
                toIndex = i;
                break;
            }
        }

        int fromIndex = toIndex > 0 ? toIndex - 1 : toIndex;
        fromTick = _entries[fromIndex].Tick;
        toTick = _entries[toIndex].Tick;
        from = _entries[fromIndex].State;
        to = _entries[toIndex].State;
        if (fromIndex == toIndex)
        {
            // 只有一份或目标落在最早样本上：贴当前 Pose，避免 Render(0) 停在出生原点。
            alpha = 1f;
            return true;
        }

        long span = _entries[toIndex].Tick - _entries[fromIndex].Tick;
        alpha = span <= 0
            ? 1f
            : (float)((target - _entries[fromIndex].Tick) / span);
        if (alpha < 0f)
            alpha = 0f;
        if (alpha > 1f)
            alpha = 1f;
        return true;
    }

    /// <summary>清空缓存，供实体 Despawn。</summary>
    public void Clear() => _count = 0;

    struct Entry
    {
        public Entry(long tick, in TState state)
        {
            Tick = tick;
            State = state;
        }

        public long Tick { get; }
        public TState State { get; }
    }
}
