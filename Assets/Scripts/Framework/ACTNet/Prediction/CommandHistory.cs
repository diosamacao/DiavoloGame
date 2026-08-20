using System;
using System.Collections.Generic;

/// <summary>按 Tick 记录尚未确认的预测命令；Ack 后丢弃更早条目。</summary>
public sealed class CommandHistory<TCommand>
{
    readonly List<Entry> _entries = new(32);
    readonly int _maxPending;

    /// <summary>创建有上限的命令历史。</summary>
    public CommandHistory(int maxPending = 180)
    {
        if (maxPending < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPending));
        _maxPending = maxPending;
    }

    /// <summary>尚未 Ack 的命令条数。</summary>
    public int Count => _entries.Count;

    /// <summary>按 Tick 追加一条命令；超出上限丢掉最旧记录。</summary>
    public void Record(long tick, in TCommand command)
    {
        _entries.Add(new Entry(tick, command));
        if (_entries.Count > _maxPending)
            _entries.RemoveRange(0, _entries.Count - _maxPending);
    }

    /// <summary>丢弃 Tick ≤ ackTick 的命令。</summary>
    public void DropAcknowledged(long ackTick)
    {
        int keepFrom = 0;
        while (keepFrom < _entries.Count && _entries[keepFrom].Tick <= ackTick)
            keepFrom++;
        if (keepFrom > 0)
            _entries.RemoveRange(0, keepFrom);
    }

    /// <summary>枚举仍未确认的命令，供 Replay。</summary>
    public void ForEachUnacknowledged(Action<long, TCommand> visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));
        for (int i = 0; i < _entries.Count; i++)
            visitor(_entries[i].Tick, _entries[i].Command);
    }

    readonly struct Entry
    {
        public Entry(long tick, in TCommand command)
        {
            Tick = tick;
            Command = command;
        }

        public long Tick { get; }
        public TCommand Command { get; }
    }
}
