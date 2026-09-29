using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>DistanceLessEqual 条件装饰定义。</summary>
[Serializable]
public sealed class DistanceLessEqualConditionDef : EnemyBehaviorConditionNodeDef
{
    [SerializeField] float distance = 2f;

    /// <summary>距离上限（米）。</summary>
    public float Distance
    {
        get => distance;
        set => distance = value;
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new DistanceLessEqualCondition(distance, BuildChild()));
}
