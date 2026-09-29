using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>DistanceGreater 条件装饰定义。</summary>
[Serializable]
public sealed class DistanceGreaterConditionDef : EnemyBehaviorConditionNodeDef
{
    [SerializeField] float distance = 4f;

    /// <summary>距离下限（米，严格大于）。</summary>
    public float Distance
    {
        get => distance;
        set => distance = value;
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new DistanceGreaterCondition(distance, BuildChild()));
}
