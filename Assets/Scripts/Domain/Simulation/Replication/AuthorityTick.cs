using System;

/// <summary>权威一帧下行：按 SimActorId 排序的角色快照 + 可选命中/生成边沿。</summary>
public sealed class AuthorityTick : IEquatable<AuthorityTick>
{
    static readonly ActorReplicationSnapshot[] EmptyActors = Array.Empty<ActorReplicationSnapshot>();
    static readonly ReplicatedHitEvent[] EmptyHits = Array.Empty<ReplicatedHitEvent>();
    static readonly SimActorId[] EmptyIds = Array.Empty<SimActorId>();

    /// <summary>创建 Tick；actors 会复制并按 ActorId 稳定排序。</summary>
    public AuthorityTick(
        long authorityFrame,
        ActorReplicationSnapshot[] actors,
        ReplicatedHitEvent[] hits = null,
        SimActorId[] spawns = null,
        SimActorId[] despawns = null)
    {
        if (authorityFrame < 0)
            throw new ArgumentOutOfRangeException(nameof(authorityFrame), "权威帧不能为负。");

        AuthorityFrame = authorityFrame;
        Actors = SortActors(actors);
        Hits = hits == null || hits.Length == 0 ? EmptyHits : (ReplicatedHitEvent[])hits.Clone();
        Spawns = spawns == null || spawns.Length == 0 ? EmptyIds : (SimActorId[])spawns.Clone();
        Despawns = despawns == null || despawns.Length == 0 ? EmptyIds : (SimActorId[])despawns.Clone();
    }

    /// <summary>权威逻辑帧号。</summary>
    public long AuthorityFrame { get; }

    /// <summary>已按 SimActorId 升序的角色快照。</summary>
    public ActorReplicationSnapshot[] Actors { get; }

    /// <summary>本帧命中边沿；可空。</summary>
    public ReplicatedHitEvent[] Hits { get; }

    /// <summary>本帧新生成的 Actor。</summary>
    public SimActorId[] Spawns { get; }

    /// <summary>本帧移除的 Actor。</summary>
    public SimActorId[] Despawns { get; }

    /// <summary>比较帧号与全部数组字段。</summary>
    public bool Equals(AuthorityTick other)
    {
        if (other == null)
            return false;
        if (AuthorityFrame != other.AuthorityFrame)
            return false;
        if (!ArraysEqual(Actors, other.Actors))
            return false;
        if (!ArraysEqual(Hits, other.Hits))
            return false;
        return ArraysEqual(Spawns, other.Spawns) && ArraysEqual(Despawns, other.Despawns);
    }

    /// <inheritdoc />
    public override bool Equals(object obj) => Equals(obj as AuthorityTick);

    /// <inheritdoc />
    public override int GetHashCode() => AuthorityFrame.GetHashCode();

    static ActorReplicationSnapshot[] SortActors(ActorReplicationSnapshot[] actors)
    {
        if (actors == null || actors.Length == 0)
            return EmptyActors;

        var copy = (ActorReplicationSnapshot[])actors.Clone();
        Array.Sort(copy, CompareActorId);
        return copy;
    }

    static int CompareActorId(ActorReplicationSnapshot left, ActorReplicationSnapshot right) =>
        left.ActorId.CompareTo(right.ActorId);

    static bool ArraysEqual<T>(T[] left, T[] right) where T : IEquatable<T>
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left == null || right == null || left.Length != right.Length)
            return false;
        for (int i = 0; i < left.Length; i++)
        {
            if (!left[i].Equals(right[i]))
                return false;
        }

        return true;
    }
}
