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

/// <summary>标识一次准备结果中的单包提交；只能交回创建它的服务端。</summary>
public readonly struct ReplicationPacketCommitToken : IEquatable<ReplicationPacketCommitToken>
{
    readonly ReplicationCommitTicket _ticket;

    /// <summary>创建只携带身份的外部令牌；不能提交到任何服务端。</summary>
    public ReplicationPacketCommitToken(Guid value)
    {
        Value = value == Guid.Empty ? throw new ArgumentException("Token 不能为空。", nameof(value)) : value;
        _ticket = null;
    }

    /// <summary>由准备结果绑定服务端与提交动作；票据不登记回服务端，保证 Prepare 纯读。</summary>
    internal ReplicationPacketCommitToken(Guid value, ReplicationCommitTicket ticket)
    {
        Value = value == Guid.Empty ? throw new ArgumentException("Token 不能为空。", nameof(value)) : value;
        _ticket = ticket ?? throw new ArgumentNullException(nameof(ticket));
    }

    /// <summary>令牌唯一值。</summary>
    public Guid Value { get; }
    internal ReplicationCommitTicket Ticket => _ticket;
    /// <inheritdoc />
    public bool Equals(ReplicationPacketCommitToken other) => Value.Equals(other.Value);
    /// <inheritdoc />
    public override bool Equals(object obj) => obj is ReplicationPacketCommitToken other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();
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

/// <summary>一次纯准备产生的生命周期与快照发送包。</summary>
public sealed class ReplicationTickDelta
{
    /// <summary>创建不可变准备结果。</summary>
    public ReplicationTickDelta(PreparedReplicationPacket[] packets) => Packets = packets ?? Array.Empty<PreparedReplicationPacket>();
    /// <summary>按生命周期在前、快照在后的稳定发送顺序。</summary>
    public PreparedReplicationPacket[] Packets { get; }
}

/// <summary>一个已编码但尚未提交的 V2 包。</summary>
public readonly struct PreparedReplicationPacket
{
    /// <summary>绑定类型、正文与提交令牌。</summary>
    public PreparedReplicationPacket(bool reliableLifecycle, byte[] body, ReplicationPacketCommitToken token)
    {
        ReliableLifecycle = reliableLifecycle;
        Body = body ?? throw new ArgumentNullException(nameof(body));
        Token = token;
    }
    /// <summary>true 表示走可靠有序生命周期通道。</summary>
    public bool ReliableLifecycle { get; }
    /// <summary>不含 Session/Mux 外壳的正文。</summary>
    public byte[] Body { get; }
    /// <summary>发送成功后提交，失败或背压时拒绝。</summary>
    public ReplicationPacketCommitToken Token { get; }
}
