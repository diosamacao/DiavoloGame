using UnityEngine;

/// <summary>帧末权威结算完成后的只读表现结果。</summary>
public readonly struct ResolvedCombatHit
{
    /// <summary>创建可安全发布到 App Event 的命中结果。</summary>
    public ResolvedCombatHit(
        ActionHitContext context,
        Transform targetTransform,
        Vector3 hitDirection)
    {
        Context = context;
        TargetTransform = targetTransform;
        HitDirection = hitDirection;
    }

    /// <summary>已成功结算的命中上下文。</summary>
    public ActionHitContext Context { get; }

    /// <summary>受击目标表现根。</summary>
    public Transform TargetTransform { get; }

    /// <summary>攻击者指向受击者的水平表现方向。</summary>
    public Vector3 HitDirection { get; }
}
