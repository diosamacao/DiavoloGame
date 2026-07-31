using System;

/// <summary>命中事件的纯模拟稳定键；排序与去重均禁止依赖 Unity InstanceId。</summary>
public readonly struct SimHitKey : IComparable<SimHitKey>, IEquatable<SimHitKey>
{
    /// <summary>构造一条命中的帧、会话、Hitbox 与双方身份。</summary>
    public SimHitKey(
        long frame,
        SimActorId attackerId,
        int actionInstanceId,
        int hitboxIndex,
        SimActorId targetId)
    {
        Frame = frame;
        AttackerId = attackerId;
        ActionInstanceId = actionInstanceId;
        HitboxIndex = hitboxIndex;
        TargetId = targetId;
    }

    /// <summary>命中所属 World 逻辑帧。</summary>
    public long Frame { get; }

    /// <summary>攻击者稳定模拟身份。</summary>
    public SimActorId AttackerId { get; }

    /// <summary>攻击者本次招式会话的单调编号。</summary>
    public int ActionInstanceId { get; }

    /// <summary>ActionTimeline 中 Hitbox 窗口的数组下标。</summary>
    public int HitboxIndex { get; }

    /// <summary>受击者稳定模拟身份。</summary>
    public SimActorId TargetId { get; }

    /// <summary>按 frame、attacker、hitbox、target、actionInstance 形成确定性字典序。</summary>
    public int CompareTo(SimHitKey other)
    {
        int comparison = Frame.CompareTo(other.Frame);
        if (comparison != 0)
            return comparison;

        comparison = AttackerId.CompareTo(other.AttackerId);
        if (comparison != 0)
            return comparison;

        comparison = HitboxIndex.CompareTo(other.HitboxIndex);
        if (comparison != 0)
            return comparison;

        comparison = TargetId.CompareTo(other.TargetId);
        return comparison != 0
            ? comparison
            : ActionInstanceId.CompareTo(other.ActionInstanceId);
    }

    /// <summary>比较所有稳定身份字段是否一致。</summary>
    public bool Equals(SimHitKey other) =>
        Frame == other.Frame
        && AttackerId == other.AttackerId
        && ActionInstanceId == other.ActionInstanceId
        && HitboxIndex == other.HitboxIndex
        && TargetId == other.TargetId;

    /// <summary>比较装箱后的稳定命中键。</summary>
    public override bool Equals(object obj) => obj is SimHitKey other && Equals(other);

    /// <summary>按固定字段顺序生成命中键哈希。</summary>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Frame.GetHashCode();
            hash = (hash * 397) ^ AttackerId.GetHashCode();
            hash = (hash * 397) ^ ActionInstanceId;
            hash = (hash * 397) ^ HitboxIndex;
            hash = (hash * 397) ^ TargetId.GetHashCode();
            return hash;
        }
    }
}
