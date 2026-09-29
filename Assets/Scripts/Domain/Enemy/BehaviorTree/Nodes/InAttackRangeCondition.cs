using System;
using UnityEngine;

/// <summary>条件装饰：水平距离 ≤ 节点配置的攻击半径（不读 Profile）。</summary>
public sealed class InAttackRangeCondition : ConditionalDecoratorNode
{
    readonly float _distance;

    /// <summary>创建 InAttackRange 装饰；distance 为米。</summary>
    public InAttackRangeCondition(float distance, IBehaviorNode child) : base(child)
    {
        _distance = Mathf.Max(0f, distance);
    }

    /// <inheritdoc />
    protected override bool Evaluate(EnemyBlackboard blackboard) =>
        blackboard != null && blackboard.HasTarget && blackboard.PlanarDistance <= _distance;
}
