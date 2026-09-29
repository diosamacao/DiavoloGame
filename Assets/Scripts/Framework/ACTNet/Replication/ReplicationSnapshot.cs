using System;

/// <summary>V2 不可靠时序完整状态批。</summary>
public sealed class ReplicationSnapshot
{
    readonly EntityRecord[] _updates;
    readonly byte[] _metadata;

    /// <summary>创建一个带生命周期屏障的快照批。</summary>
    public ReplicationSnapshot(NetTick tick, long requiredLifecycleSequence, ushort batchIndex, ushort batchCount, EntityRecord[] updates, byte[] metadata)
    {
        if (!tick.IsValid) throw new ArgumentException("Tick 必须有效。", nameof(tick));
        if (requiredLifecycleSequence < 0) throw new ArgumentOutOfRangeException(nameof(requiredLifecycleSequence));
        if (batchCount < 1 || batchIndex >= batchCount) throw new ArgumentOutOfRangeException(nameof(batchIndex));
        Tick = tick;
        RequiredLifecycleSequence = requiredLifecycleSequence;
        BatchIndex = batchIndex;
        BatchCount = batchCount;
        _updates = updates == null ? throw new ArgumentNullException(nameof(updates)) : (EntityRecord[])updates.Clone();
        _metadata = metadata == null ? throw new ArgumentNullException(nameof(metadata)) : Clone(metadata);
    }

    /// <summary>该批对应的权威模拟 Tick。</summary>
    public NetTick Tick { get; }
    /// <summary>应用前必须已提交到的生命周期序列。</summary>
    public long RequiredLifecycleSequence { get; }
    /// <summary>本 Tick 快照批索引。</summary>
    public ushort BatchIndex { get; }
    /// <summary>本 Tick 快照批总数。</summary>
    public ushort BatchCount { get; }
    /// <summary>返回完整状态记录副本。</summary>
    public EntityRecord[] Updates => (EntityRecord[])_updates.Clone();
    /// <summary>返回应用层 Meta 副本。</summary>
    public byte[] Metadata => Clone(_metadata);
    internal EntityRecord[] UpdateBuffer => _updates;
    internal byte[] MetadataBuffer => _metadata;

    static byte[] Clone(byte[] value)
    {
        if (value.Length == 0) return Array.Empty<byte>();
        var copy = new byte[value.Length];
        Buffer.BlockCopy(value, 0, copy, 0, value.Length);
        return copy;
    }
}

/// <summary>一次性提交票据；状态归准备结果所有，不写入 ReplicationServer。</summary>
internal sealed class ReplicationCommitTicket
{
    readonly ReplicationServer _owner;
    readonly Action<ReplicationServer> _apply;
    bool _finished;

    /// <summary>绑定唯一服务端与成功提交动作。</summary>
    public ReplicationCommitTicket(ReplicationServer owner, Action<ReplicationServer> apply)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
    }

    /// <summary>仅允许所属服务端结束一次；成功发送时执行提交动作。</summary>
    public void Finish(ReplicationServer owner, bool commit)
    {
        if (!ReferenceEquals(owner, _owner) || _finished)
            throw new InvalidOperationException("未知、外部或已结束的 replication commit token。");
        _finished = true;
        if (commit)
            _apply(owner);
    }
}
