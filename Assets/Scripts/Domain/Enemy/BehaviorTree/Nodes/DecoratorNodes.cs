using UnityEngine;

/// <summary>反转 Success/Failure；Running 透传。</summary>
public sealed class InverterNode : IBehaviorNode
{
    readonly IBehaviorNode _child;

    /// <summary>创建 Inverter。</summary>
    public InverterNode(IBehaviorNode child)
    {
        _child = child ?? throw new System.ArgumentNullException(nameof(child));
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        BehaviorStatus status = _child.Tick(blackboard);
        if (status == BehaviorStatus.Running)
            return BehaviorStatus.Running;
        return status == BehaviorStatus.Success ? BehaviorStatus.Failure : BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset() => _child.Reset();
}

/// <summary>子节点结束一律 Success（可选分支）。</summary>
public sealed class SucceederNode : IBehaviorNode
{
    readonly IBehaviorNode _child;

    /// <summary>创建 Succeeder。</summary>
    public SucceederNode(IBehaviorNode child)
    {
        _child = child ?? throw new System.ArgumentNullException(nameof(child));
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        BehaviorStatus status = _child.Tick(blackboard);
        return status == BehaviorStatus.Running ? BehaviorStatus.Running : BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset() => _child.Reset();
}

/// <summary>
/// 冷却门：表未就绪则 Failure；子节点 Success 时按节点 frames 写入冷却（招式 CD 真源）。
/// basic_attack 另阻塞 AttackConfirmPending，与 CooldownReady 一致。
/// </summary>
public sealed class CooldownGateNode : IBehaviorNode
{
    readonly string _cooldownId;
    readonly int _cooldownFrames;
    readonly IBehaviorNode _child;

    /// <summary>创建冷却门装饰。</summary>
    public CooldownGateNode(string cooldownId, int cooldownFrames, IBehaviorNode child)
    {
        _cooldownId = string.IsNullOrEmpty(cooldownId) ? EnemyCooldownIds.Dodge : cooldownId;
        _cooldownFrames = Mathf.Max(0, cooldownFrames);
        _child = child ?? throw new System.ArgumentNullException(nameof(child));
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard?.Cooldowns == null)
        {
            _child.Reset();
            return BehaviorStatus.Failure;
        }

        if (!blackboard.Cooldowns.IsReady(_cooldownId))
        {
            // 与条件装饰一致：门未开时 Abort Self
            _child.Reset();
            return BehaviorStatus.Failure;
        }

        // 起手确认期内禁止再进 basic_attack，避免每帧 Pulse
        if (_cooldownId == EnemyCooldownIds.BasicAttack && blackboard.AttackConfirmPending)
        {
            _child.Reset();
            return BehaviorStatus.Failure;
        }

        BehaviorStatus status = _child.Tick(blackboard);
        if (status == BehaviorStatus.Success && _cooldownFrames > 0)
            blackboard.Cooldowns.Set(_cooldownId, _cooldownFrames);
        return status;
    }

    /// <inheritdoc />
    public void Reset() => _child.Reset();
}

/// <summary>
/// 仇恨滞回服务装饰：每帧按 enter/exit 维护 IsAggroed，再 Tick 子树（不门控失败）。
/// </summary>
public sealed class AggroGateNode : IBehaviorNode
{
    readonly float _enterRadius;
    readonly float _exitRadius;
    readonly IBehaviorNode _child;

    /// <summary>创建仇恨滞回装饰；exit 至少等于 enter。</summary>
    public AggroGateNode(float enterRadius, float exitRadius, IBehaviorNode child)
    {
        _enterRadius = Mathf.Max(0f, enterRadius);
        _exitRadius = Mathf.Max(_enterRadius, exitRadius);
        _child = child ?? throw new System.ArgumentNullException(nameof(child));
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        UpdateAggro(blackboard);
        return _child.Tick(blackboard);
    }

    /// <inheritdoc />
    public void Reset() => _child.Reset();

    /// <summary>进 enter 置仇；已仇且距离 &gt; exit 脱战；无目标清旗。</summary>
    void UpdateAggro(EnemyBlackboard blackboard)
    {
        if (blackboard == null)
            return;

        if (!blackboard.HasTarget)
        {
            blackboard.IsAggroed = false;
            return;
        }

        if (!blackboard.IsAggroed && blackboard.PlanarDistance <= _enterRadius)
            blackboard.IsAggroed = true;
        else if (blackboard.IsAggroed && blackboard.PlanarDistance > _exitRadius)
            blackboard.IsAggroed = false;
    }
}
