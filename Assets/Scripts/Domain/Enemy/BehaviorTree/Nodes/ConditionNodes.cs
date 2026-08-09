using UnityEngine;

/// <summary>条件：存在目标。</summary>
public sealed class HasTargetCondition : IBehaviorNode
{
    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard) =>
        blackboard != null && blackboard.HasTarget ? BehaviorStatus.Success : BehaviorStatus.Failure;

    /// <inheritdoc />
    public void Reset()
    {
    }
}

/// <summary>条件：处于仇恨滞回（IsAggroed）。</summary>
public sealed class InCombatAggroCondition : IBehaviorNode
{
    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard) =>
        blackboard != null && blackboard.IsAggroed ? BehaviorStatus.Success : BehaviorStatus.Failure;

    /// <inheritdoc />
    public void Reset()
    {
    }
}

/// <summary>条件：水平距离 ≤ 攻击半径。</summary>
public sealed class InAttackRangeCondition : IBehaviorNode
{
    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard?.Profile == null || !blackboard.HasTarget)
            return BehaviorStatus.Failure;
        return blackboard.PlanarDistance <= blackboard.Profile.AttackRange
            ? BehaviorStatus.Success
            : BehaviorStatus.Failure;
    }

    /// <inheritdoc />
    public void Reset()
    {
    }
}

/// <summary>条件：角色处于指定状态。</summary>
public sealed class IsCharacterStateCondition : IBehaviorNode
{
    readonly CharacterStateType _expected;

    /// <summary>创建状态条件。</summary>
    public IsCharacterStateCondition(CharacterStateType expected)
    {
        _expected = expected;
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard) =>
        blackboard != null && blackboard.CharacterState == _expected
            ? BehaviorStatus.Success
            : BehaviorStatus.Failure;

    /// <inheritdoc />
    public void Reset()
    {
    }
}

/// <summary>条件：指定冷却 id 就绪（basic_attack 另需无 AttackConfirmPending）。</summary>
public sealed class CooldownReadyCondition : IBehaviorNode
{
    readonly string _cooldownId;

    /// <summary>创建冷却就绪条件。</summary>
    public CooldownReadyCondition(string cooldownId)
    {
        _cooldownId = string.IsNullOrEmpty(cooldownId)
            ? EnemyCooldownIds.BasicAttack
            : cooldownId;
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard?.Cooldowns == null)
            return BehaviorStatus.Failure;
        if (!blackboard.Cooldowns.IsReady(_cooldownId))
            return BehaviorStatus.Failure;

        // 攻击确认期内禁止再次判定 basic_attack 就绪，避免每帧 Pulse
        if (_cooldownId == EnemyCooldownIds.BasicAttack && blackboard.AttackConfirmPending)
            return BehaviorStatus.Failure;

        return BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset()
    {
    }
}

/// <summary>条件：水平距离 ≤ 指定值（米）。</summary>
public sealed class DistanceLessEqualCondition : IBehaviorNode
{
    readonly float _distance;

    /// <summary>创建距离上限条件。</summary>
    public DistanceLessEqualCondition(float distance)
    {
        _distance = Mathf.Max(0f, distance);
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard) =>
        blackboard != null && blackboard.HasTarget && blackboard.PlanarDistance <= _distance
            ? BehaviorStatus.Success
            : BehaviorStatus.Failure;

    /// <inheritdoc />
    public void Reset()
    {
    }
}

/// <summary>条件：水平距离 &gt; 指定值（米）。</summary>
public sealed class DistanceGreaterCondition : IBehaviorNode
{
    readonly float _distance;

    /// <summary>创建距离下限条件（严格大于）。</summary>
    public DistanceGreaterCondition(float distance)
    {
        _distance = Mathf.Max(0f, distance);
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard) =>
        blackboard != null && blackboard.HasTarget && blackboard.PlanarDistance > _distance
            ? BehaviorStatus.Success
            : BehaviorStatus.Failure;

    /// <inheritdoc />
    public void Reset()
    {
    }
}
