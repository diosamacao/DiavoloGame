using System;
using UnityEngine;

/// <summary>条件装饰：指定冷却 id 就绪（basic_attack 另需无 AttackConfirmPending）。</summary>
public sealed class CooldownReadyCondition : ConditionalDecoratorNode
{
    readonly string _cooldownId;

    /// <summary>创建冷却就绪条件装饰。</summary>
    public CooldownReadyCondition(string cooldownId, IBehaviorNode child) : base(child)
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
        if (!EnemyCooldownIds.IsGateReady(blackboard.Cooldowns, _cooldownId))
            return false;

        // 攻击确认期内禁止再次判定 basic_attack 就绪，避免每帧重复请求
        if (_cooldownId == EnemyCooldownIds.BasicAttack && blackboard.AttackConfirmPending)
            return false;

        return true;
    }
}
