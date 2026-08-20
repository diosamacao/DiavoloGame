using System;

/// <summary>远端快照时间线：按 Tick 丢旧，按插值延迟取样，禁止回滚到更旧状态。</summary>
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
    /// 按延迟 Tick 取样。delayTicks=0 取最新；不足两份时 from=to、alpha=0。
    /// </summary>
    public bool TrySample(int delayTicks, out TState from, out TState to, out float alpha) =>
        TrySample(delayTicks, out _, out _, out from, out to, out alpha);

    /// <summary>取样并返回 bracketing Tick，供业务层决定是否提交新快照。</summary>
    public bool TrySample(
        int delayTicks,
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
        long targetTick = _entries[_count - 1].Tick - delay;
        if (targetTick < _entries[0].Tick)
            targetTick = _entries[0].Tick;

        int toIndex = 0;
        for (int i = 0; i < _count; i++)
        {
            if (_entries[i].Tick <= targetTick)
                toIndex = i;
        }

        int fromIndex = toIndex > 0 ? toIndex - 1 : toIndex;
        fromTick = _entries[fromIndex].Tick;
        toTick = _entries[toIndex].Tick;
        from = _entries[fromIndex].State;
        to = _entries[toIndex].State;
        if (fromIndex == toIndex)
        {
            alpha = 0f;
            return true;
        }

        long span = _entries[toIndex].Tick - _entries[fromIndex].Tick;
        alpha = span <= 0
            ? 1f
            : (float)(targetTick - _entries[fromIndex].Tick) / span;
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
