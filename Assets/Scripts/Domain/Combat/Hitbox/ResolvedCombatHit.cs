using UnityEngine;

/// <summary>帧末权威结算完成后的只读表现结果。</summary>
public readonly struct ResolvedCombatHit
{
    /// <summary>创建可安全发布到 App Event 的命中结果。</summary>
    public ResolvedCombatHit(
        ActionHitContext context,
        Transform targetTransform,
        Vector3 hitDirection,
        Vector3 hitPoint,
        bool absorbedByPerfectDodge = false,
        SimHitKey key = default,
        HitReactionKind reactionKind = HitReactionKind.None)
    {
        Context = context;
        TargetTransform = targetTransform;
        HitDirection = hitDirection;
        HitPoint = hitPoint;
        AbsorbedByPerfectDodge = absorbedByPerfectDodge;
        Key = key;
        ReactionKind = reactionKind;
    }

    /// <summary>已成功结算的命中上下文。</summary>
    public ActionHitContext Context { get; }

    /// <summary>受击目标表现根。</summary>
    public Transform TargetTransform { get; }

    /// <summary>攻击者指向受击者的水平表现方向。</summary>
    public Vector3 HitDirection { get; }

    /// <summary>受击 Cue 落点（逻辑接触估计）。</summary>
    public Vector3 HitPoint { get; }

    /// <summary>是否为完美闪避吞伤（无扣血/受击 Reaction，表现侧勿播受击 Cue）。</summary>
    public bool AbsorbedByPerfectDodge { get; }

    /// <summary>权威命中键；供下行 ReplicatedHitEvent 去重，不参与表现方向。</summary>
    public SimHitKey Key { get; }

    /// <summary>Service 裁定后的受击档；供复制事件与 HUD。</summary>
    public HitReactionKind ReactionKind { get; }
}
