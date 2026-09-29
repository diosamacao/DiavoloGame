using System;
using UnityEngine;

/// <summary>条件装饰：存在目标。</summary>
public sealed class HasTargetCondition : ConditionalDecoratorNode
{
    /// <summary>创建 HasTarget 装饰。</summary>
    public HasTargetCondition(IBehaviorNode child) : base(child)
    {
    }

    /// <inheritdoc />
    protected override bool Evaluate(EnemyBlackboard blackboard) =>
        blackboard != null && blackboard.HasTarget;
}
