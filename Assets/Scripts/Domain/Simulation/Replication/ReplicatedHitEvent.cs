using System;

/// <summary>
/// 权威命中边沿。Key 供去重；ActionId 与毫米落点供客机播受击 Cue，不参与伤害结算。
/// </summary>
public readonly struct ReplicatedHitEvent : IEquatable<ReplicatedHitEvent>
{
    /// <summary>仅键与帧；表现字段为 0。供旧测试与无落点路径。</summary>
    public ReplicatedHitEvent(long frame, SimHitKey key)
        : this(frame, key, actionId: 0, 0, 0, 0, 0, 0)
    {
    }

    /// <summary>创建带受击表现落点的命中事件。</summary>
    public ReplicatedHitEvent(
        long frame,
        SimHitKey key,
        int actionId,
        int hitXMm,
        int hitYMm,
        int hitZMm,
        int dirXMm,
        int dirZMm)
    {
        Frame = frame;
        Key = key;
        ActionId = actionId < 0 ? 0 : actionId;
        HitXMm = hitXMm;
        HitYMm = hitYMm;
        HitZMm = hitZMm;
        DirXMm = dirXMm;
        DirZMm = dirZMm;
    }

    /// <summary>命中所属权威逻辑帧。</summary>
    public long Frame { get; }

    /// <summary>稳定命中键。</summary>
    public SimHitKey Key { get; }

    /// <summary>攻击者当时招式的 Catalog Id；Host 在进入帧级应用载荷前补齐。</summary>
    public int ActionId { get; }

    /// <summary>受击 Cue 落点 X（毫米）。</summary>
    public int HitXMm { get; }

    /// <summary>受击 Cue 落点 Y（毫米）。</summary>
    public int HitYMm { get; }

    /// <summary>受击 Cue 落点 Z（毫米）。</summary>
    public int HitZMm { get; }

    /// <summary>水平命中方向 X（毫米，约单位向量×1000）。</summary>
    public int DirXMm { get; }

    /// <summary>水平命中方向 Z（毫米）。</summary>
    public int DirZMm { get; }

    /// <summary>补写 Catalog ActionId 并保留落点；Host 打包应用载荷时调用。</summary>
    public ReplicatedHitEvent WithActionId(int actionId) =>
        new(Frame, Key, actionId, HitXMm, HitYMm, HitZMm, DirXMm, DirZMm);

    /// <summary>比较键、招式 Id 与落点。</summary>
    public bool Equals(ReplicatedHitEvent other) =>
        Frame == other.Frame
        && Key.Equals(other.Key)
        && ActionId == other.ActionId
        && HitXMm == other.HitXMm
        && HitYMm == other.HitYMm
        && HitZMm == other.HitZMm
        && DirXMm == other.DirXMm
        && DirZMm == other.DirZMm;

    /// <inheritdoc />
    public override bool Equals(object obj) =>
        obj is ReplicatedHitEvent other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (Frame.GetHashCode() * 397) ^ Key.GetHashCode();
        }
    }
}
