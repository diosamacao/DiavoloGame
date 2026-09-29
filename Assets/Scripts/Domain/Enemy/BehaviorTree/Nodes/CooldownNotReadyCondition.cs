using System;
using UnityEngine;

/// <summary>条件装饰：冷却/失败重试占用中；供对峙支在 CD 期间放行。</summary>
public sealed class CooldownNotReadyCondition : ConditionalDecoratorNode
{
    readonly string _cooldownId;

    /// <summary>创建冷却占用条件装饰。</summary>
    public CooldownNotReadyCondition(string cooldownId, IBehaviorNode child) : base(child)
    {
        _cooldownId = string.IsNullOrEmpty(cooldownId)
            ? EnemyCooldownIds.BasicAttack
            : cooldownId;
    }

    /// <inheritdoc />
    protected override bool Evaluate(EnemyBlackboard blackboard)
    {
        if (blackboard?.Cooldowns == null)
            return false;
        if (blackboard.AttackConfirmPending && _cooldownId == EnemyCooldownIds.BasicAttack)
            return true;
        return !EnemyCooldownIds.IsGateReady(blackboard.Cooldowns, _cooldownId);
    }
}
