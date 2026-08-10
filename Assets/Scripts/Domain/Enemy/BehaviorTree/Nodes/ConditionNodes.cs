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
    public virtual void Reset() => _child.Reset();
}

/// <summary>距离滞回带模式（进/出双阈值语义）。</summary>
public enum DistanceBandMode
{
    /// <summary>过远带（追击）：未入则 d&gt;enter 进入；已入则 d&gt;exit 保持，d≤exit 且 dwell 满离开。</summary>
    OutsideFar = 0,

    /// <summary>过近带：未入则 d&lt;enter 进入；已入则 d&lt;exit 保持，d≥exit 且 dwell 满离开。</summary>
    OutsideNear = 1,

    /// <summary>区间带（对峙）：未入则 enter≤d≤exit 进入；已入则越界且 dwell 满离开。</summary>
    InsideBand = 2,
}

/// <summary>
/// 条件装饰：带滞回与最短驻留的距离带；状态在本实例上，Reset 清 latch/dwell。
/// Chase 用 OutsideFar（exit&lt;enter）；Strafe 用 InsideBand；Attack 勿套本节点。
/// </summary>
public sealed class DistanceBandCondition : ConditionalDecoratorNode
{
    readonly DistanceBandMode _mode;
    readonly float _enterDistance;
    readonly float _exitDistance;
    readonly int _minDwellFrames;

    bool _latched;
    int _dwellFrames;

    /// <summary>创建距离滞回装饰。</summary>
    public DistanceBandCondition(
        DistanceBandMode mode,
        float enterDistance,
        float exitDistance,
        int minDwellFrames,
        IBehaviorNode child) : base(child)
    {
        _mode = mode;
        _enterDistance = Mathf.Max(0f, enterDistance);
        _exitDistance = Mathf.Max(0f, exitDistance);
        _minDwellFrames = Mathf.Max(0, minDwellFrames);
    }

    /// <summary>当前是否已锁在带内（单测/调试）。</summary>
    public bool IsLatched => _latched;

    /// <summary>已驻留帧数（单测/调试）。</summary>
    public int DwellFrames => _dwellFrames;

    /// <inheritdoc />
    protected override bool Evaluate(EnemyBlackboard blackboard)
    {
        if (blackboard == null || !blackboard.HasTarget)
        {
            _latched = false;
            _dwellFrames = 0;
            return false;
        }

        float distance = blackboard.PlanarDistance;
        if (!_latched)
        {
            if (ShouldEnter(distance))
            {
                _latched = true;
                _dwellFrames = 0;
            }
        }
        else
        {
            // 已入带：累加驻留；满足离开阈值且 dwell 满才翻面
            _dwellFrames++;
            if (ShouldLeave(distance) && _dwellFrames >= _minDwellFrames)
            {
                _latched = false;
                _dwellFrames = 0;
            }
        }

        return _latched;
    }

    /// <inheritdoc />
    public override void Reset()
    {
        _latched = false;
        _dwellFrames = 0;
        base.Reset();
    }

    bool ShouldEnter(float distance)
    {
        switch (_mode)
        {
            case DistanceBandMode.OutsideFar:
                return distance > _enterDistance;
            case DistanceBandMode.OutsideNear:
                return distance < _enterDistance;
            case DistanceBandMode.InsideBand:
                return distance >= Mathf.Min(_enterDistance, _exitDistance)
                       && distance <= Mathf.Max(_enterDistance, _exitDistance);
            default:
                return false;
        }
    }

    bool ShouldLeave(float distance)
    {
        switch (_mode)
        {
            case DistanceBandMode.OutsideFar:
                // Chase：exit 通常 &lt; enter；贴近后离开追击支
                return distance <= _exitDistance;
            case DistanceBandMode.OutsideNear:
                return distance >= _exitDistance;
            case DistanceBandMode.InsideBand:
                float lo = Mathf.Min(_enterDistance, _exitDistance);
                float hi = Mathf.Max(_enterDistance, _exitDistance);
                return distance < lo || distance > hi;
            default:
                return true;
        }
    }
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
        if (!EnemyCooldownIds.IsGateReady(blackboard.Cooldowns, _cooldownId))
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
