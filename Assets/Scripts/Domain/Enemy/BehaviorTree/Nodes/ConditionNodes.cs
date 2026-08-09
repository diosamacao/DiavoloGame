using System;
using UnityEngine;

/// <summary>
/// UE 风格条件装饰器：先求值，失败则 Abort Self（Reset 子树）并 Failure；通过则 Tick 子节点。
/// </summary>
public abstract class ConditionalDecoratorNode : IBehaviorNode
{
    readonly IBehaviorNode _child;

    /// <summary>创建条件装饰；child 不可空。</summary>
    protected ConditionalDecoratorNode(IBehaviorNode child)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
    }

    /// <summary>本帧条件是否成立。</summary>
    protected abstract bool Evaluate(EnemyBlackboard blackboard);

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (!Evaluate(blackboard))
        {
            // Abort Self：条件翻面时清掉 Running 子进度，避免恢复后接着跑
            _child.Reset();
            return BehaviorStatus.Failure;
        }

        return _child.Tick(blackboard);
    }

    /// <inheritdoc />
    public void Reset() => _child.Reset();
}

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

/// <summary>条件装饰：水平距离 ≤ 攻击半径。</summary>
public sealed class InAttackRangeCondition : ConditionalDecoratorNode
{
    /// <summary>创建 InAttackRange 装饰。</summary>
    public InAttackRangeCondition(IBehaviorNode child) : base(child)
    {
    }

    /// <inheritdoc />
    protected override bool Evaluate(EnemyBlackboard blackboard)
    {
        if (blackboard?.Profile == null || !blackboard.HasTarget)
            return false;
        return blackboard.PlanarDistance <= blackboard.Profile.AttackRange;
    }
}

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
        if (!blackboard.Cooldowns.IsReady(_cooldownId))
            return false;

        // 攻击确认期内禁止再次判定 basic_attack 就绪，避免每帧 Pulse
        if (_cooldownId == EnemyCooldownIds.BasicAttack && blackboard.AttackConfirmPending)
            return false;

        return true;
    }
}

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

/// <summary>条件装饰：水平距离 &gt; 指定值（米）。</summary>
public sealed class DistanceGreaterCondition : ConditionalDecoratorNode
{
    readonly float _distance;

    /// <summary>创建距离下限条件装饰（严格大于）。</summary>
    public DistanceGreaterCondition(float distance, IBehaviorNode child) : base(child)
    {
        _distance = Mathf.Max(0f, distance);
    }

    /// <inheritdoc />
    protected override bool Evaluate(EnemyBlackboard blackboard) =>
        blackboard != null && blackboard.HasTarget && blackboard.PlanarDistance > _distance;
}
