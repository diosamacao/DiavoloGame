using System;
using UnityEngine;

/// <summary>条件装饰：处于仇恨滞回（IsAggroed）。</summary>
public sealed class InCombatAggroCondition : ConditionalDecoratorNode
{
    /// <summary>创建 InCombatAggro 装饰。</summary>
    public InCombatAggroCondition(IBehaviorNode child) : base(child)
    {
    }

    /// <inheritdoc />
    protected override bool Evaluate(EnemyBlackboard blackboard) =>
        blackboard != null && blackboard.IsAggroed;
}
