using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>InAttackRange 条件装饰定义；距离在节点上，不读 Profile。</summary>
[Serializable]
public sealed class InAttackRangeConditionDef : EnemyBehaviorConditionNodeDef
{
    [SerializeField] float distance = 2f;

    /// <summary>攻击距离上限（米）。</summary>
    public float Distance
    {
        get => distance;
        set => distance = Mathf.Max(0f, value);
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() => Wrap(new InAttackRangeCondition(distance, BuildChild()));
}
