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
/// 冷却门：表未就绪则 Failure 不 Tick 子节点；子节点 Success 时写入冷却。
/// basic_attack 仍建议由 Brain 确认后写入，避免与起手确认双权威。
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
            return BehaviorStatus.Failure;
        if (!blackboard.Cooldowns.IsReady(_cooldownId))
            return BehaviorStatus.Failure;

        BehaviorStatus status = _child.Tick(blackboard);
        if (status == BehaviorStatus.Success && _cooldownFrames > 0)
            blackboard.Cooldowns.Set(_cooldownId, _cooldownFrames);
        return status;
    }

    /// <inheritdoc />
    public void Reset() => _child.Reset();
}
