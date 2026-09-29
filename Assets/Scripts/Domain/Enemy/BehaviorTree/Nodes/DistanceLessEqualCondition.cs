using System;
using UnityEngine;

/// <summary>条件装饰：水平距离 ≤ 指定值（米）。</summary>
public sealed class DistanceLessEqualCondition : ConditionalDecoratorNode
{
    readonly float _distance;

    /// <summary>创建距离上限条件装饰。</summary>
    public DistanceLessEqualCondition(float distance, IBehaviorNode child) : base(child)
    {
        _distance = Mathf.Max(0f, distance);
    }

    /// <inheritdoc />
    protected override bool Evaluate(EnemyBlackboard blackboard) =>
        blackboard != null && blackboard.HasTarget && blackboard.PlanarDistance <= _distance;
}
