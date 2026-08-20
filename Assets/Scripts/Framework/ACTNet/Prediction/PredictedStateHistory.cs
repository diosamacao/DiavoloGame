using System;
using System.Collections.Generic;

/// <summary>按 Tick 保存预测状态，供权威 Ack 对照与 Replay 后回写。</summary>
public sealed class PredictedStateHistory<TState>
{
    readonly List<Entry> _entries = new(32);
    readonly int _maxPending;

    /// <summary>创建有上限的预测状态历史。</summary>
    public PredictedStateHistory(int maxPending = 180)
    {
        if (maxPending < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPending));
        _maxPending = maxPending;
    }

    /// <summary>已记录的预测状态条数。</summary>
    public int Count => _entries.Count;

    /// <summary>记录该 Tick 的预测结果；同 Tick 覆盖。</summary>
    public void Capture(long tick, in TState state)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Tick != tick)
                continue;
            _entries[i] = new Entry(tick, state);
            return;
        }

        _entries.Add(new Entry(tick, state));
        if (_entries.Count > _maxPending)
            _entries.RemoveRange(0, _entries.Count - _maxPending);
    }

    /// <summary>读取指定 Tick 的预测状态。</summary>
    public bool TryGet(long tick, out TState state)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Tick != tick)
                continue;
            state = _entries[i].State;
            return true;
        }

        state = default;
        return false;
    }

    /// <summary>丢弃 Tick ≤ ackTick 的预测状态。</summary>
    public void DropAcknowledged(long ackTick)
    {
        int keepFrom = 0;
        while (keepFrom < _entries.Count && _entries[keepFrom].Tick <= ackTick)
            keepFrom++;
        if (keepFrom > 0)
            _entries.RemoveRange(0, keepFrom);
    }

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
