using UnityEngine;

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
