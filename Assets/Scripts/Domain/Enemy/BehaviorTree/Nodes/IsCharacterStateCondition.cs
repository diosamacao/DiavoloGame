using System;
using UnityEngine;

/// <summary>条件装饰：角色处于指定状态。</summary>
public sealed class IsCharacterStateCondition : ConditionalDecoratorNode
{
    readonly CharacterStateType _expected;

    /// <summary>创建状态条件装饰。</summary>
    public IsCharacterStateCondition(CharacterStateType expected, IBehaviorNode child) : base(child)
    {
        _expected = expected;
    }

    /// <inheritdoc />
    protected override bool Evaluate(EnemyBlackboard blackboard) =>
        blackboard != null && blackboard.CharacterState == _expected;
}
