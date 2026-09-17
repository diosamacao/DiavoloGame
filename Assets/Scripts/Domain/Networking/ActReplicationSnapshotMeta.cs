using System;

/// <summary>V2 Snapshot 的 ACT 权威确认与阵容元数据；命中只走 ReplicationEvent。</summary>
public sealed class ActReplicationSnapshotMeta
{
    readonly SimActorId[] _partyActorIds;
    readonly int[] _partyFlags;

    /// <summary>创建不可变权威 Meta。</summary>
    public ActReplicationSnapshotMeta(long appliedHint, long lastAppliedHint, SimActorId[] partyActorIds, int[] partyFlags, int activeSlot, bool partyWiped)
    {
        _partyActorIds = partyActorIds == null ? Array.Empty<SimActorId>() : (SimActorId[])partyActorIds.Clone();
        _partyFlags = partyFlags == null ? Array.Empty<int>() : (int[])partyFlags.Clone();
        if (_partyActorIds.Length != _partyFlags.Length || _partyActorIds.Length > PartyLoadoutRules.MaxMembers)
            throw new ArgumentException("Party ids/flags 必须等长且不超过阵容上限。");
        if (!partyWiped && _partyActorIds.Length > 0 && (activeSlot < 0 || activeSlot >= _partyActorIds.Length))
            throw new ArgumentOutOfRangeException(nameof(activeSlot));
        AppliedClientFrameHint = appliedHint;
        LastAppliedClientFrameHint = lastAppliedHint;
        ActivePartySlot = activeSlot;
        PartyWiped = partyWiped;
    }

    /// <summary>本权威步采用的客户端 Hint。</summary>
    public long AppliedClientFrameHint { get; }
    /// <summary>累计确认到的最新客户端 Hint。</summary>
    public long LastAppliedClientFrameHint { get; }
    /// <summary>当前权威活动槽；队灭时可为 -1。</summary>
    public int ActivePartySlot { get; }
    /// <summary>权威队灭终态。</summary>
    public bool PartyWiped { get; }
    /// <summary>按槽返回权威 ActorId。</summary>
    public SimActorId[] PartyActorIds => (SimActorId[])_partyActorIds.Clone();
    /// <summary>按槽返回 Party flags。</summary>
    public int[] PartyFlags => (int[])_partyFlags.Clone();
    internal SimActorId[] PartyActorIdBuffer => _partyActorIds;
    internal int[] PartyFlagBuffer => _partyFlags;
}
