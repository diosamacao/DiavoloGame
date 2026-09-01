using System;

/// <summary>帧级应用载荷：applied hint、Owner 阵容身份与 Active 槽；命中生产路径编码为空数组。</summary>
public sealed class ActReplicationApplicationPayload
{
    readonly ReplicatedHitEvent[] _hits;
    readonly SimActorId[] _partyActorIds;

    /// <summary>创建不可变帧级载荷；null 命中数组按空数组处理。</summary>
    public ActReplicationApplicationPayload(
        long appliedClientFrameHint,
        ReplicatedHitEvent[] hits,
        SimActorId[] partyActorIds = null,
        int activePartySlot = -1,
        long lastAppliedClientFrameHint = 0)
    {
        AppliedClientFrameHint = appliedClientFrameHint;
        LastAppliedClientFrameHint = lastAppliedClientFrameHint;
        _hits = hits == null || hits.Length == 0
            ? Array.Empty<ReplicatedHitEvent>()
            : (ReplicatedHitEvent[])hits.Clone();
        _partyActorIds = partyActorIds == null || partyActorIds.Length == 0
            ? Array.Empty<SimActorId>()
            : (SimActorId[])partyActorIds.Clone();
        if (_partyActorIds.Length > PartyLoadoutRules.MaxMembers)
            throw new ArgumentOutOfRangeException(nameof(partyActorIds));
        if (_partyActorIds.Length > 0
            && (activePartySlot < 0 || activePartySlot >= _partyActorIds.Length))
        {
            throw new ArgumentOutOfRangeException(nameof(activePartySlot));
        }
        ActivePartySlot = activePartySlot;
    }

    /// <summary>本权威步真正采用的客户端 FrameHint；0 表示未采用新命令。</summary>
    public long AppliedClientFrameHint { get; }

    /// <summary>权威累计应用到的最新客户端 FrameHint；用于换人等离散预测确认。</summary>
    public long LastAppliedClientFrameHint { get; }

    /// <summary>返回本帧权威命中表现事件的独立副本。</summary>
    public ReplicatedHitEvent[] Hits => (ReplicatedHitEvent[])_hits.Clone();

    /// <summary>供同程序集 Codec 读取不可变命中缓冲，避免重复复制。</summary>
    internal ReplicatedHitEvent[] HitBuffer => _hits;

    /// <summary>按 PartyLoadout 槽序返回本连接拥有的稳定 ActorId；空槽为 Invalid。</summary>
    public SimActorId[] PartyActorIds => (SimActorId[])_partyActorIds.Clone();

    /// <summary>当前接收玩家输入的槽；无阵容元数据时为 -1。</summary>
    public int ActivePartySlot { get; }

    /// <summary>供同程序集 Codec 读取不可变槽身份缓冲。</summary>
    internal SimActorId[] PartyActorIdBuffer => _partyActorIds;
}
