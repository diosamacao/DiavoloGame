/// <summary>单次构帧策略：跳过未变 Update、节拍、预算与全量恢复。不含 ACT 字段。</summary>
public readonly struct ReplicationBuildOptions
{
    /// <summary>兼容既有单测：每 Tick 都可发，不限预算，仍跳过字节级未变 Update。</summary>
    public static ReplicationBuildOptions Compatible { get; } = new(
        skipUnchanged: true,
        maxUpdateBytes: 0,
        snapshotIntervalTicks: 1,
        preferredEntity: default,
        forceFull: false);

    /// <summary>生产默认：30Hz 刷新、1200 字节 Update 预算、Owner 优先。</summary>
    public static ReplicationBuildOptions Compact { get; } = new(
        skipUnchanged: true,
        maxUpdateBytes: 1200,
        snapshotIntervalTicks: 2,
        preferredEntity: default,
        forceFull: false);

    /// <summary>创建构帧选项；interval 小于 1 时按 1 处理。</summary>
    public ReplicationBuildOptions(
        bool skipUnchanged,
        int maxUpdateBytes,
        int snapshotIntervalTicks,
        NetEntityId preferredEntity,
        bool forceFull)
    {
        SkipUnchanged = skipUnchanged;
        MaxUpdateBytes = maxUpdateBytes < 0 ? 0 : maxUpdateBytes;
        SnapshotIntervalTicks = snapshotIntervalTicks < 1 ? 1 : snapshotIntervalTicks;
        PreferredEntity = preferredEntity;
        ForceFull = forceFull;
    }

    /// <summary>载荷字节与上次已发送相同时不进 Update。</summary>
    public bool SkipUnchanged { get; }

    /// <summary>本帧 Update 载荷预算；0 表示不限制。Spawn/Despawn 不受此限。</summary>
    public int MaxUpdateBytes { get; }

    /// <summary>非优先实体的刷新间隔（逻辑 Tick）。Owner / ForceFull 不受此限。</summary>
    public int SnapshotIntervalTicks { get; }

    /// <summary>预算内优先发送的实体，通常是 Owner。</summary>
    public NetEntityId PreferredEntity { get; }

    /// <summary>忽略未变与节拍，把当前 full set 全部作为 Update 或 Spawn 发出。</summary>
    public bool ForceFull { get; }

    /// <summary>指定优先实体，其余选项不变。</summary>
    public ReplicationBuildOptions WithPreferred(NetEntityId preferredEntity) =>
        new(SkipUnchanged, MaxUpdateBytes, SnapshotIntervalTicks, preferredEntity, ForceFull);

    /// <summary>打开或关闭全量恢复。</summary>
    public ReplicationBuildOptions WithForceFull(bool forceFull) =>
        new(SkipUnchanged, MaxUpdateBytes, SnapshotIntervalTicks, PreferredEntity, forceFull);
}
