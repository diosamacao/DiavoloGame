using System;

/// <summary>V2 可靠有序实体生命周期批。</summary>
public sealed class ReplicationLifecycle
{
    readonly SpawnRecord[] _spawns;
    readonly DespawnRecord[] _despawns;

    /// <summary>创建一个生命周期序列批。</summary>
    public ReplicationLifecycle(long lifecycleSequence, NetTick tick, ushort batchIndex, ushort batchCount, SpawnRecord[] spawns, DespawnRecord[] despawns)
    {
        if (lifecycleSequence < 1) throw new ArgumentOutOfRangeException(nameof(lifecycleSequence));
        if (!tick.IsValid) throw new ArgumentException("Tick 必须有效。", nameof(tick));
        if (batchCount < 1 || batchIndex >= batchCount) throw new ArgumentOutOfRangeException(nameof(batchIndex));
        LifecycleSequence = lifecycleSequence;
        Tick = tick;
        BatchIndex = batchIndex;
        BatchCount = batchCount;
        _spawns = spawns == null ? throw new ArgumentNullException(nameof(spawns)) : (SpawnRecord[])spawns.Clone();
        _despawns = despawns == null ? throw new ArgumentNullException(nameof(despawns)) : (DespawnRecord[])despawns.Clone();
    }

    /// <summary>连接内严格递增的生命周期序列。</summary>
    public long LifecycleSequence { get; }
    /// <summary>产生本批的权威 Tick。</summary>
    public NetTick Tick { get; }
    /// <summary>本 Tick 生命周期批索引。</summary>
    public ushort BatchIndex { get; }
    /// <summary>本 Tick 生命周期批总数。</summary>
    public ushort BatchCount { get; }
    /// <summary>返回 Spawn 记录副本。</summary>
    public SpawnRecord[] Spawns => (SpawnRecord[])_spawns.Clone();
    /// <summary>返回 Despawn 记录副本。</summary>
    public DespawnRecord[] Despawns => (DespawnRecord[])_despawns.Clone();
    internal SpawnRecord[] SpawnBuffer => _spawns;
    internal DespawnRecord[] DespawnBuffer => _despawns;
}
